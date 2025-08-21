module Partas.GitNet.Renderer

open System.Collections.Frozen
open System.Collections.Generic
open Fake.Core
open LibGit2Sharp.FSharp
open Partas.ConventionalCommits
open Partas.GitNet
open Partas.GitNet.GitCollection
open Partas.GitNet.GitCollection.TagCommitCollection
open Partas.GitNet.GitHelpers
open Partas.GitNet.Types

(*
Two phase collection to primitive records for writing.

PreRenderPhase ->
    Assembling the TagCommitCollection into an array that groups
    scopes to tags, and tags to commits.
    The tags and commits have been resolved to their GitNet prefix types.
RenderPhase ->
    Uses the output from the PreRenderPhase to 'render' the collection into
    records of strings.
    Ideally, there should be no optional types, these should be changed to
    DUs. This way we are explicit with the writer, and will not have to pass
    anything except the config item to it.
    The writing phase can then process these string records into the final
    output.
*)

/// <summary>
/// Module containing the types and functions relating to the PreRenderPhase.
/// </summary>
/// <remarks>
/// <para>This phase reflects the manipulation and collection of the <c>TagCommitCollection</c>
/// into their resolved <c>GitNet</c> types, with scopes matched to their collection of tags, and the
/// tags being matched to their collection of commits for that scope.</para>
/// </remarks>
module private PreRenderPhase =
    /// <summary>
    /// Represents the special state of the first and last tag when resolving the
    /// commits between the tags, as the first tag has no precursor, and the last tag
    /// is used to determine the unreleased commits.
    /// </summary>
    type TagPairPosition =
        /// <summary>
        /// The first Tag of the collection.
        /// This should be compared to the first commit to collect the commits for
        /// the first tag.
        /// </summary>
        | FirstTag of TagShaKey
        /// <summary>
        /// A pair of tags in the collection that are used as the upper and lower bounds
        /// when collecting the commits for the upper boundary tag using Gits Diffing ops.
        /// </summary>
        | PairTag of TagShaKey * TagShaKey
        /// <summary>
        /// The last tag in the collection that is used to collect the unreleased commits
        /// since the aforementioned tag.
        /// </summary>
        | LastTag of TagShaKey
        member inline this.PermuteToFirstTag =
            match this with
            | PairTag(tag,_) -> FirstTag tag
            | _ -> invalidOp ""
        member inline this.PermuteToLastTag =
            match this with
            | PairTag(_, tag) -> LastTag tag
            | _ -> invalidOp ""
    /// <summary>
    /// Functions and helpers operating on the TagCommitCollection.
    /// </summary>
    module TagCommitCollection =
        /// <summary>
        /// Functions and helpers operating on the TagCommitCollection relating
        /// to the Scope
        /// </summary>
        module Scope =
            let keys = _.Scopes >> Seq.map _.Key
            let dir = _.Scopes.GetValueRefOrNullRef
        /// <summary>
        /// Functions and helpers operating on the TagCommitCollection relating
        /// to Tags
        /// </summary>
        module Tags =
            let orderedShas = _.TagCollection.OrderedKeys
            let get sha = _.TagCollection.KeyDictionary[sha]
            let tryScope collection= collection.TagCollection.KeyDictionary.GetValueRefOrNullRef >> GitNetTag.getScope
            let scope collection = tryScope collection >> _.Value
            let (|IsUnscoped|_|) collection =
                let isScoped tag = collection |> get tag |> _.IsSemVerTag |> not
                function
                | tag when isScoped tag -> ValueSome ()
                | _ -> ValueNone
            let (|IsScoped|_|) collection = function
                | tag ->
                    tag
                    |> tryScope collection
                    |> ValueOption.map Scope
            
        /// <summary>
        /// Functions and helpers operating on the TagCommitCollection relating
        /// to Commits
        /// </summary>
        module Commits =
            let scopes sha = _.CommitScopes[sha]
            let containsScopeOrLookup collection (runtime: GitNetRuntime) scope sha =
                try (scopes sha >> _.Contains(scope)) collection
                with :? KeyNotFoundException ->
                    runtime.repo
                    |> Repository.lookup sha.Value
                    |> ValueOption.bind GitObject.tryPeel<Commit>
                    |> ValueOption.map (
                        runtime.DiffCommitPaths [Scope.dir collection scope]
                        >> fun changes ->
                            seq {
                                changes.Added
                                changes.Modified
                                changes.Copied
                                changes.Deleted
                                changes.Renamed
                            }
                            |> Seq.collect (Seq.collect (TreeEntryChanges.path >> pathTraversal))
                        )
                    |> ValueOption.exists(Seq.contains (Scope.dir collection scope))
            let get collection (runtime: GitNetRuntime) sha =
                try
                collection.CommitCollection.Get sha
                with _ ->
                    Repository.lookup sha.Value runtime.repo
                    |> ValueOption.bind GitObject.tryPeel<LibGit2Sharp.Commit>
                    |> _.Value
    
    /// <summary>
    /// Performs the pre render collection on a <c>TagCommitCollection</c>.
    /// </summary>
    /// <param name="runtime">The repository, used for lookups in exceptional circumstances
    /// where a commit is not registered in the collection due to it being a parent.</param>
    /// <param name="collection">The <c>TagCommitCollection</c></param>
    let fromTagCommitCollection (runtime: GitNetRuntime) (collection: TagCommitCollection) =
        let (|Scoped|) =
            collection.TagCollection.KeyDictionary.GetValueRefOrNullRef
            >> GitNetTag.getScope
        collection
        |> TagCommitCollection.Scope.keys
        |> Seq.map (fun scope ->
            scope,
            collection
            |> TagCommitCollection.Tags.orderedShas
            |> Array.filter (function
                | tagSha when collection.TagCollection.KeyDictionary.GetValueRefOrNullRef tagSha |> _.IsSemVerTag ->
                    true
                | Scoped tagScope when tagScope |> ValueOption.contains scope.Value ->
                    true
                | _ -> false
                )
            |> function
                | [||] ->
                    [|
                        ValueNone,
                        collection.CommitCollection.OrderedKeys
                        |> Array.rev
                        |> Array.filter
                            (TagCommitCollection.Commits.containsScopeOrLookup collection runtime scope)
                        |> Array.map (
                            TagCommitCollection.Commits.get collection runtime
                            >> GitNetCommit.create FSharp.ConventionalCommit.parse
                            )
                    |]
                | arr ->
                arr |> function
                | [| tag |] ->
                    [|
                        TagPairPosition.FirstTag tag
                        TagPairPosition.LastTag tag
                    |]
                | arr ->
                    arr
                    |> Array.pairwise
                    |> Array.map TagPairPosition.PairTag
                    |> fun pairs ->
                        pairs
                        |> Array.insertAt
                               pairs.Length
                               (pairs |> Array.last |> _.PermuteToLastTag)
                        |> Array.insertAt
                               0
                               (pairs |> Array.head |> _.PermuteToFirstTag)
                |> Array.map (function
                    | PairTag(fstTag,sndTag) ->
                        ValueSome sndTag,
                        getCommitsBetween fstTag sndTag collection
                    | LastTag tag ->
                        ValueNone,
                        getUnreleasedCommits tag collection
                    | FirstTag tag ->
                        ValueSome tag,
                        // TODO - will always result in nil atm.
                        getCommitsBetween tag tag collection
                    >> fun (tag, commits) ->
                        tag
                        |> ValueOption.map (fun tag ->
                            collection
                            |> TagCommitCollection.Tags.get tag)
                        ,
                        commits
                        |> Seq.filter (TagCommitCollection.Commits.containsScopeOrLookup collection runtime scope)
                        |> Seq.map (TagCommitCollection.Commits.get collection runtime >> function
                            | commit -> { ParsedCommit = FSharp.ConventionalCommit.parse commit.Message
                                          Ref = commit })
                        |> Seq.toArray
                    )
                |> Array.rev
                )

/// <summary>
/// Functions, types and helpers for the <c>Render</c> Phase.
/// </summary>
/// <remarks>
/// This phase is responsible for rendering the computed <c>GitNet</c> tree
/// into primitive string records/DUs for the writer to then process into MD.
/// </remarks>
module Render =
    /// <summary>
    /// A rendered commit, for a tag, of a scope.
    /// </summary>
    type Commit = {
        Message: string
        CommitSha: string
        CommitAuthor: string
        Scope: string voption
    }
    /// <summary>
    /// A rendered tag, of a scope.
    /// </summary>
    type Tag = {
        TagName: string
        TagUrl: string voption
        TagDate: string voption
        Commits: FrozenDictionary<CommitGroup,Commit list>
    }
    /// <summary>
    /// A rendered scope, of a repository.
    /// </summary>
    type Scope = {
        ScopeName: string voption
        ScopeTags: Tag list
        ScopeUnreleasedUrl: string voption
        ScopeUnreleased: Commit list
    }
    type BumpResult =
        | NoHistory of BumpType
        | WithHistory of bump: BumpType * history: GitNetTag

    /// <summary>
    /// DU which indicates a failure in the rendering phase.
    /// </summary>
    type private Error =
        /// <summary>
        /// The rendering phase should only receive one unreleased tag
        /// per scope.
        /// </summary>
        | MoreThanOneUnreleasedTag
    /// <summary>
    /// Functions relating to Rendering of commits.
    /// </summary>
    module Commit =
        open LibGit2Sharp.FSharp
        let fromGitNetCommit ( { Ref = gitCommit } as commit ) =
            {
                Message =
                    commit
                    |> GitNetCommit.subject
                CommitSha =
                    Commit.sha gitCommit
                CommitAuthor =
                    gitCommit
                    |> Commit.author
                    |> Signature.name
                Scope = ValueNone
            }
        let fromGitNetCommitWithScope scope = fromGitNetCommit >> fun comm ->
            { comm with Scope = scope }
        
    /// <summary>
    /// Functions relating to rendering of Tags
    /// </summary>
    module Tag =
        let fromGitNetTagWithCommits (runtime: GitNetRuntime) commits (tag: GitNetTag) (refTag: GitNetTag)=
            {
                TagName =
                    tag
                    |> GitNetTag.toSemverString
                TagUrl =
                    runtime.githubUrlFactory
                    |> match tag = refTag with
                       | true -> Option.map _.Create(tag |> GitNetTag.Git.commit)
                       | false -> Option.map _.Create(refTag,tag)
                    |> Option.toValueOption
                TagDate =
                    try
                    tag
                    |> GitNetTag.Git.commit
                    |> Commit.committer
                    |> Signature.date
                    |> _.ToString(Spec.dateFormat)
                    |> ValueSome
                    with e ->
                        ValueNone
                Commits =
                    commits
                    |> Seq.filter (runtime.config.Output.ComputeCommitFilter(tag))
                    |> runtime.CategoriseCommits
                    |> fun commits -> query {
                        for commit,grouping in commits do
                        groupValBy commit (grouping |> Option.defaultValue runtime.config.Output.DefaultUnmatchedGroup)
                    }
                    |> _.ToFrozenDictionary(
                        _.Key,
                        Seq.map Commit.fromGitNetCommit >> Seq.toList)
                    
            }
    /// <summary>
    /// Helpers that determines if a <c>tag * commits</c> tuple is an unreleased collection.
    /// This is determined by whether the tag is a true value, or null.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="_commits"></param>
    let private isUnreleasedTagCommitTuple (tag: GitNetTag voption, _commits: GitNetCommit array) = tag.IsNone
    /// <summary>
    /// Tries to partition a collection of <c>tag * commit</c> tuples, and extract
    /// the unreleased commits.<br/>
    /// </summary>
    let private tryPartitionUnreleased =
        Array.partition isUnreleasedTagCommitTuple
        >> fun (unreleased, tagged) ->
            try
            let result = 
                unreleased
                |> Array.exactlyOne
                |> snd,
                tagged |> Array.map (fun (tag,commits) ->
                    tag.Value,commits)
            Ok result
            with
            | :? System.ArgumentException as e ->
                Error Error.MoreThanOneUnreleasedTag
    /// <summary>
    /// Partitions a collection of <c>tag * commit</c> tuples, and extracts the unreleased commits.
    /// This will always succeed.
    /// </summary>
    let private partitionUnreleased =
        Array.partition isUnreleasedTagCommitTuple
        >> fun (unreleased,tagged) ->
            unreleased
            |> Array.tryHead
            |> Option.map snd
            |> Option.defaultValue [||]
            ,
            tagged
            |> Array.map(fun (tag,arr) -> tag.Value,arr)
    /// <summary>
    /// Functions relating to the rendering of scopes.
    /// </summary>
    module Scope =
        let fromPreRenderedCollectionEntry config (scopeName: GitCollection.Scope, entries: (GitNetTag voption * GitNetCommit array) array) =
            let unreleasedCommits, taggedCommits =
                entries |> partitionUnreleased 
            {
                ScopeName =
                    scopeName.Value
                    |> ValueSome
                ScopeTags =
                    if taggedCommits |> Array.isEmpty
                    then []
                    else
                    taggedCommits
                    |> Array.rev
                    |> Array.insertAt 0 taggedCommits[0]
                    |> Array.pairwise
                    |> Array.map(
                        fun ((refTag,_), (tag, commits)) ->
                            Tag.fromGitNetTagWithCommits config commits tag refTag
                        )
                    |> Array.rev
                    |> Array.toList
                ScopeUnreleasedUrl =
                    taggedCommits
                    |> Array.tryHead
                    |> Option.bind (
                        fst
                        >> (
                            config.githubUrlFactory
                            |> Option.map (fun urlFactory ->
                                fun (tag: GitNetTag) ->
                                    urlFactory.Create(tag) |> Some
                                )
                            |> Option.defaultValue (fun _ -> None)
                        )
                        )
                    |> ValueOption.ofOption
                ScopeUnreleased =
                    unreleasedCommits
                    |> Array.filter (config.config.Output.ComputeCommitFilter())
                    |> Array.map Commit.fromGitNetCommit
                    |> Array.toList
            }
    let private auxResults runtime (collection: (GitCollection.Scope * (GitNetTag voption * GitNetCommit[])[]) seq) =
        let checkBumpInScope:
            GitCollection.Scope * (GitNetTag voption * GitNetCommit[])[] -> BumpType voption =
            snd
            >> Array.tryFind (fst >> _.IsNone)
            >> Option.bind (
                snd
                >> Array.map (GitNetCommit.parsed >> willBump runtime)
                >> Array.tryFind _.IsSome
                )
            >> Option.defaultValue ValueNone
        let getBumpInScope =
            fun (scope,commits) ->
                match (scope,commits) |> checkBumpInScope with
                | ValueSome bump ->
                    if commits.Length > 1 then
                        commits
                        |> Array.findIndex (fst >> _.IsNone)
                        |> (+) 1
                        |> fun idx -> Array.tryItem idx commits
                        |> Option.bind (fst >> ValueOption.map(fun tag -> WithHistory(bump,tag)) >> ValueOption.toOption)
                    else None
                    |> Option.defaultValue (NoHistory bump) 
                    |> fun bumpUnion ->
                        KeyValuePair(scope.Value,bumpUnion)
                        |> Some
                | _ -> None
        // determine bumps
        collection
        |> Seq.choose getBumpInScope
        |> _.ToFrozenDictionary()
    type Output = {
        Bumps: FrozenDictionary<string,BumpResult>
        Scopes: Scope array
    }
    /// <summary>
    /// Conducts the rendering of a <c>TagCommitCollection</c>.
    /// </summary>
    /// <param name="runtime">GitNetRuntime - Provides details as to the categorisation behaviour of commits in tags.</param>
    /// <param name="collection">TagCommitCollection - The collection to render.</param>
    let fromTagCommitCollection runtime collection =
        let collection' =
            PreRenderPhase.fromTagCommitCollection runtime collection
        let aux = auxResults runtime collection'

        {
            Bumps = aux
            Scopes =
                collection'
                |> Seq.map (Scope.fromPreRenderedCollectionEntry runtime)
                |> Seq.toArray
        }
    
    
