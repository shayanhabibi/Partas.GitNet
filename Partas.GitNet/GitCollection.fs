/// Internal library and test suite usage only.
module Partas.GitNet.GitCollection

open System
open System.Collections.Frozen
open System.Collections.Generic
open LibGit2Sharp
open LibGit2Sharp.FSharp
open Partas.GitNet.GitHelpers
open Partas.GitNet.RepoCracker

[<Struct>]
type CommitSha = CommitSha of string
[<Struct>]
type TagSha = TagSha of string
// TagShas can be duplicates when derived from git tags, as
// there may be multiple tags on one commit.
// In this case, we combine the sha key with the tag name.
[<Struct>]
type TagShaKey =
    | Duplicate of sha: TagSha * tagName: string
    | Unique of sha: TagSha
[<Struct>]
type Scope = Scope of string

type CommitSha with member this.Value = let (CommitSha value) = this in value
type TagSha with member this.Value = let (TagSha value) = this in value
type Scope with member this.Value = let (Scope value) = this in value
type TagShaKey with
    member this.Value =
        match this with
        | Duplicate(sha,_)
        | Unique sha -> sha.Value

module TagShaKey =
    module Unique =
        let create = GitNetTag.Git.sha >> TagSha >> Unique
    module Duplicate =
        let create gitNetTag =
            (GitNetTag.Git.sha gitNetTag |> TagSha,
             GitNetTag.Git.name gitNetTag)
            |> Duplicate
    let tagSha: TagShaKey -> TagSha = function
        | Duplicate(sha,_) | Unique sha -> sha
    let sha: TagShaKey -> string =
        _.Value
    let name = function
        | Duplicate(_,name) -> ValueSome name
        | _ -> ValueNone
    
module Commit =
    let sha: Commit -> CommitSha = Commit.sha >> CommitSha
module GitNetTag =
    let getScope = GitNetTag.getScope >> ValueOption.map Scope
    module Git =
        let sha = GitNetTag.Git.sha >> TagSha

[<AutoOpen>]
module Helpers =
    let inline toFrozenSet<'a>: 'a seq -> 'a FrozenSet = _.ToFrozenSet()
    let inline toFrozenDictionary<'Key,'Value>: KeyValuePair<'Key, 'Value> seq -> FrozenDictionary<'Key,'Value> =
        _.ToFrozenDictionary()
    let inline findIndexes (item1: 'Item) (item2: 'Item) (collection: 'Item array) =
        let mutable idx1 = ValueNone
        let mutable idx2 = ValueNone
        let mutable idx = 0
        let length = collection.Length
        while idx < length && (idx1.IsNone || idx2.IsNone) do
            let current = &collection[idx]
            if current = item1 then
                idx1 <- ValueSome idx
            if current = item2 then
                idx2 <- ValueSome idx
            idx <- idx + 1
        idx1,idx2
    module Unsafe =
        let inline findIndexes item1 item2 collection =
            let item1,item2 = findIndexes item1 item2 collection
            item1.Value,item2.Value
    open System.Linq
    let pathTraversal (path: string) =
        path
        |> Seq.mapi (fun i -> function
            | '/' | '\\' when i > 0 -> ValueSome <| i - 1
            | _ -> ValueNone)
        |> Seq.filter _.IsSome
        |> Seq.map (ValueOption.get >> fun idx -> path[0..idx])
        |> _.Append(path)
        
type OrderedCollection<'Key, 'Value> = {
    OrderedKeys: 'Key[]
    KeyDictionary: FrozenDictionary<'Key, 'Value>
}

type OrderedCollection<'Key, 'Value> with
    member this.Get key = this.KeyDictionary[key]

module OrderedCollection =
    let inline private createImpl<'Source, 'Key, 'Item, 'Comparable when 'Comparable : comparison>
        ([<InlineIfLambda>] sortMethod: ('Source -> 'Comparable) -> 'Source seq -> 'Source seq)
        ([<InlineIfLambda>] keyFunc: 'Source -> 'Key)
        ([<InlineIfLambda>] itemFunc: 'Source -> 'Item)
        ([<InlineIfLambda>] sortBy: 'Source -> 'Comparable)
        (collection: 'Source seq) : OrderedCollection<'Key, 'Item> = {
        OrderedKeys =
            collection
            |> sortMethod sortBy
            |> Seq.map keyFunc
            |> Seq.toArray
        KeyDictionary =
            collection
            |> Seq.map (
                (fun item -> keyFunc item, itemFunc item)
                >> KeyValuePair
                )
            |> _.ToFrozenDictionary()
    }
    let create<'Source, 'Key, 'Item, 'Comparable when 'Comparable : comparison> keyFunc itemFunc sortBy collection = createImpl<'Source, 'Key, 'Item, 'Comparable> Seq.sortBy keyFunc itemFunc sortBy collection
    let createDesc<'Source, 'Key, 'Item, 'Comparable when 'Comparable : comparison> keyFunc itemFunc sortBy collection = createImpl<'Source, 'Key, 'Item, 'Comparable> Seq.sortByDescending keyFunc itemFunc sortBy collection
    let inline keyPairs<'Key, 'Value>: OrderedCollection<'Key,'Value> -> ('Key * 'Key) array = _.OrderedKeys >> Array.pairwise
    let inline get key (col: OrderedCollection<_, _>) = col.Get(key)
type TagCollection = OrderedCollection<TagShaKey, GitNetTag>
type CommitCollection = OrderedCollection<CommitSha, Commit>
module TagCollection =
    type GetCommitsResponse = {
        UnreleasedCommits: CommitSha FrozenSet
        TagCommits: FrozenDictionary<TagSha, CommitSha FrozenSet>
    }
    type private TagPositions =
        | First of TagShaKey
        | Middle of TagShaKey * TagShaKey
        | End of TagShaKey
    type private TagPositionCommits =
        | Unreleased of FrozenSet<CommitSha>
        | TagCommits of TagSha * FrozenSet<CommitSha>
    let private getCommitsForTagPosition (runtime: GitNetRuntime) (collection: TagCollection) = function
        | End tag ->
            collection.Get tag
            |> runtime.GetCommitsTo
            |> Seq.map Commit.sha
            |> toFrozenSet
            |> Unreleased
        | Middle (oldTag, newTag) ->
            let (>->) (appl1,appl2) func = (func appl1, func appl2)
            let key = newTag |> TagShaKey.tagSha
            let value =
                (oldTag, newTag)
                >-> collection.Get
                |> runtime.GetCommitsBetween
                |> Seq.map Commit.sha
                |> toFrozenSet
            (key,value)
            |> TagCommits
        | First tag ->
            collection.Get tag
            |> runtime.GetCommitsAfter
            |> Seq.map Commit.sha
            |> toFrozenSet
            |> fun value ->
                tag |> TagShaKey.tagSha,value
            |> TagCommits
    let private extractTagCommitPairsIntoPairs (tagCommits: TagPositionCommits array) =
        tagCommits
        |> Array.map (function
            Unreleased _ -> failwith "Should not be possible to receive unreleased commits within this function. Please submit an issue"
            | TagCommits(key,value) ->
                KeyValuePair(key,value)
            )
    let private getTagPositionsFromCollection (collection: TagCollection) =
        collection.OrderedKeys
        |> Array.distinctBy TagShaKey.sha
        |> function
        | [||] -> [||], None
        | [| tag |] -> [| First tag |], Some (End tag) 
        | tags ->
            let first = tags |> Array.head |> First
            let last = tags |> Array.last |> End |> Some
            [|
                first
                yield!
                    tags
                    |> Array.pairwise
                    |> Array.map Middle
            |], last
    let getCommits (runtime: GitNetRuntime) (tagCollection: TagCollection) =
        tagCollection
        |> getTagPositionsFromCollection
        |> function
        | positions, None ->
            {
                UnreleasedCommits = FrozenSet.Empty
                TagCommits =
                    positions
                    |> Array.map (getCommitsForTagPosition runtime tagCollection)
                    |> extractTagCommitPairsIntoPairs
                    |> toFrozenDictionary
            }
        | positions, Some unreleasedTag ->
            {
                UnreleasedCommits =
                    getCommitsForTagPosition runtime tagCollection unreleasedTag
                    |> function
                        | Unreleased commits -> commits
                        | _ -> failwith "Should not be able to receive unreleased commits in this function at this position. Please submit an issue"
                TagCommits =
                    positions
                    |> Array.map (getCommitsForTagPosition runtime tagCollection)
                    |> extractTagCommitPairsIntoPairs
                    |> toFrozenDictionary
            }
    let load (runtime: GitNetRuntime) =
        let allTags =
            Repository.tags runtime.repo
            |> Seq.map GitNetTag.create
        let distinctShaTags =
            allTags |> Seq.distinctBy GitNetTag.Git.sha
        let duplicateShaTags =
            allTags
            |> Seq.except distinctShaTags
        let tags =
            let distinctShaGitNetTags =
                distinctShaTags |> Seq.map (GitNetTag.Git.sha >> Unique)
                |> Seq.zip distinctShaTags
            let duplicateShaGitNetTags =
                duplicateShaTags |> Seq.map GitNetTag.Git.name
                |> Seq.zip (duplicateShaTags |> Seq.map GitNetTag.Git.sha)
                |> Seq.map Duplicate
                |> Seq.zip duplicateShaTags
            distinctShaGitNetTags
            |> Seq.append duplicateShaGitNetTags
        let sortedTags =
            tags
            |> Seq.sortBy (
                fst >> GitNetTag.git
                >> Tag.target
                >> GitObject.tryPeel<LibGit2Sharp.Commit>
                >> ValueOption.map (Commit.committer >> Signature.date)
                >> ValueOption.defaultValue DateTimeOffset.MaxValue
                )
            |> Seq.toArray
        {
            OrderedKeys = Array.map snd sortedTags
            KeyDictionary = sortedTags.ToFrozenDictionary(snd, fst)
        }
module CommitCollection =
    let load (runtime: GitNetRuntime)=
        OrderedCollection.create
            Commit.sha
            id
            (Commit.committer >> Signature.date)
            (Repository.commits runtime.repo)
type CommitTreeChangeCollection = FrozenDictionary<CommitSha, FrozenSet<string>>
module CommitTreeChangeCollection =
    let load (runtime: GitNetRuntime) (commits: CommitCollection) : CommitTreeChangeCollection =
        let keyPairs = OrderedCollection.keyPairs commits
        keyPairs
        |> Seq.map Choice1Of2
        // latest commit
        |> Seq.insertAt 0 (keyPairs |> Seq.head |> fst |> Choice2Of2)
        |> Seq.map(function
            | Choice2Of2 sha ->
                // latest commit
                let key = sha
                let value = sha |> commits.Get |> runtime.Diff
                key,value
            | Choice1Of2 (oldSha,newSha) ->
                let oldCommitTree =
                    commits.Get oldSha |> Commit.tree
                let newCommitTree =
                    commits.Get newSha |> Commit.tree
                let key = newSha
                let value = runtime.diff.Compare<TreeChanges>(oldCommitTree, newCommitTree)
                key,value
            >> fun (sha,changes) ->
                let key = sha
                let value =
                    seq { changes.Added; changes.Copied; changes.Deleted; changes.Modified }
                    |> Seq.collect (Seq.collect (TreeEntryChanges.path >> pathTraversal))
                    |> toFrozenSet
                key,value
            >> KeyValuePair.Create
                )
        |> toFrozenDictionary
type CommitScopeCollection = FrozenDictionary<CommitSha, Scope FrozenSet>
module CommitScopeCollection =
    let load (commitCollection: CommitTreeChangeCollection) (scopes: FrozenDictionary<Scope, string>) =
        let pathsDict = scopes.ToFrozenDictionary(_.Value,_.Key)
        let paths = scopes.Values
        commitCollection.ToFrozenDictionary(
            _.Key,
            (fun (keyValuePair: KeyValuePair<CommitSha,FrozenSet<string>>) -> keyValuePair.Value)
            >> fun treeChanges ->
                paths
                |> Seq.choose (fun path ->
                    if treeChanges.Contains path then
                        Some pathsDict[path]
                    else None
                    )
                |> toFrozenSet
            )
type TagCommitCollection =
    {
        TagCollection: TagCollection
        CommitCollection: CommitCollection
        TreeChangeCollection: FrozenDictionary<CommitSha, FrozenSet<string>>
        UntaggedCommits: FrozenSet<CommitSha>
        TagCommits: FrozenDictionary<TagSha, FrozenSet<CommitSha>>
        Scopes: FrozenDictionary<Scope, string>
        CommitScopes: FrozenDictionary<CommitSha, Scope FrozenSet>
    }

module TagCommitCollection =
    type ScopePathDictionary = FrozenDictionary<Scope, string>
    let load (runtime: GitNetRuntime) =
        let crackedRepos: CrackedProject seq = runtime.CrackRepo
        let scopes: ScopePathDictionary =
            crackedRepos
            |> Seq.choose (fun crackedProject ->
                    crackedProject.GitNetOptions.Scope |> function
                        | Some scope ->
                            KeyValuePair(Scope scope, crackedProject.ProjectDirectory)
                            |> Some
                        | _ -> None
                )
            |> toFrozenDictionary
        let tagCollection = TagCollection.load runtime
        let commitCollection = CommitCollection.load runtime
        let treeChangeCollection = CommitTreeChangeCollection.load runtime commitCollection
        let commitScopes = CommitScopeCollection.load treeChangeCollection scopes
        let getCommitsResponse = tagCollection |> TagCollection.getCommits runtime
        {
            TagCollection = tagCollection
            CommitCollection = commitCollection
            UntaggedCommits =
                getCommitsResponse.UnreleasedCommits
            TagCommits =
                getCommitsResponse.TagCommits
            TreeChangeCollection = treeChangeCollection
            Scopes = scopes
            CommitScopes = commitScopes
        }
    let inline private sliceTagsIndexImpl lowerIdx higherIdx collection=
        let inline tagCommitLookup sha = _.TagCommits[sha]
        let shas =
            collection.TagCollection.OrderedKeys[lowerIdx + 1..higherIdx].AsSpan()
        let shaCollection = HashSet(20)
        for sha in shas do
            shaCollection.UnionWith(tagCommitLookup (sha |> TagShaKey.tagSha) collection)
        shaCollection
    let getCommitsBetween tag1 tag2 collection =
        let idx1,idx2 = Unsafe.findIndexes tag1 tag2 collection.TagCollection.OrderedKeys
        sliceTagsIndexImpl idx1 idx2 collection
        |> toFrozenSet
    let getUnreleasedCommits latestTag collection =
        let idx1 =
            collection.TagCollection.OrderedKeys
            |> Array.findIndex ((=) latestTag)
        let idx2 =
            collection.TagCollection.OrderedKeys.Length
            |> (+) -1
        let result = sliceTagsIndexImpl idx1 idx2 collection
        result.UnionWith(collection.UntaggedCommits)
        result
        |> toFrozenSet
    let collectScopes (runtime: GitNetRuntime) collection =
        let (|Scoped|) =
            collection.TagCollection.KeyDictionary.GetValueRefOrNullRef
            >> GitNetTag.getScope
        seq {
            for scope in collection.Scopes |> Seq.map _.Key do
                scope,
                collection.TagCollection.OrderedKeys
                |> Array.filter (function
                    | tagSha when collection.TagCollection.KeyDictionary.GetValueRefOrNullRef tagSha |> _.IsSemVerTag ->
                        true
                    | Scoped tagScope when tagScope |> ValueOption.contains scope ->
                        true
                    | _ -> false
                    )
                |> Array.pairwise
                |> fun pairs ->
                    pairs
                    |> Array.map Choice1Of3
                    |> Array.insertAt pairs.Length (
                        pairs |> Array.last |> snd
                        |> Choice2Of3
                        )
                    |> Array.insertAt 0 (
                        pairs |> Array.head |> fst
                        |> Choice3Of3
                        )
                |> Array.map (function
                    | Choice1Of3 tags ->
                        snd tags |> ValueSome, getCommitsBetween (fst tags) (snd tags) collection
                    | Choice2Of3 tag ->
                        ValueNone,
                        getUnreleasedCommits tag collection
                    | Choice3Of3 tag ->
                        tag |> ValueSome, getCommitsBetween tag tag collection
                    >> fun (tag,commits) ->
                        tag |> ValueOption.map (fun tag -> collection.TagCollection.KeyDictionary[tag]),
                        commits
                        |> Seq.filter (fun commit ->
                            try
                            collection.CommitScopes[commit].Contains scope
                            with :? KeyNotFoundException ->
                                Repository.lookup commit.Value runtime.repo
                                |> ValueOption.bind GitObject.tryPeel<Commit>
                                |> ValueOption.map (
                                    runtime.DiffCommitPaths [collection.Scopes[scope]]
                                    >> fun changes ->
                                        seq {
                                            changes.Added
                                            changes.Modified
                                            changes.Copied
                                            changes.Deleted
                                            changes.Renamed
                                        }
                                        |> Seq.collect(Seq.collect (TreeEntryChanges.path >> pathTraversal))
                                    )
                                |> ValueOption.exists(Seq.contains collection.Scopes[scope])
                            )
                        |> Seq.toArray
                    )
        }
    
