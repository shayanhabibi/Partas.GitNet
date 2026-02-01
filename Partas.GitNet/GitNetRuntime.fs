[<AutoOpen>]
module Partas.GitNet.GitNetRuntime

open System
open System.Collections.Frozen
open System.Collections.Generic
open LibGit2Sharp
open Partas.Tools.SepochSemver
open Partas.ConventionalCommits
open LibGit2Sharp.FSharp

/// Runtime for a GitNetConfig; contains resources that are repeatedly
/// shared between functions. Not intended for side effects. Just to
/// prevent boiler plate parameters being passed around.
type GitNetRuntime(config: GitNetConfig) =
    let _repo = Repository.load config.RepositoryPath
    let _cachedRuns = ResizeArray<FrozenDictionary<string, GitNetTag voption>>()
    let cacheRun: (string * GitNetTag voption) array -> _ = Array.map KeyValuePair >> _.ToFrozenDictionary() >> fun input -> _cachedRuns.Add input; input
    let getLastCacheRun () = _cachedRuns |> Seq.last
    let getCacheRuns() = _cachedRuns |> Seq.toArray
    let githubRemote =
        _repo |> Repository.network
        |> Network.remotes
        |> Seq.tryFind(
            Remote.url
            >> _.Contains("github.com")
            )
        |> Option.map GitHubRemote
    let cachedStats =
        {|
            AssemblyFiles = HashSet<string>()
            Versions = HashSet<string>()
            Tags = HashSet<string>()
        |}
    let cacheAssemblyFile = cachedStats.AssemblyFiles.Add >> ignore
    let cacheVersionFile = cachedStats.Versions.Add >> ignore
    let cacheTag = cachedStats.Tags.Add >> ignore
    
    let dir =
        _repo
        |> Repository.info
        |> RepositoryInformation.tryWorkingDirectory
        |> ValueOption.bind(fun path ->
            path
            |> String.filter ((<>) '/')
            |> String.IsNullOrWhiteSpace
            |> function true -> ValueNone | _ -> ValueSome path
            )
        |> ValueOption.defaultValue config.RepositoryPath
    let disposals = ResizeArray<unit -> unit>()
    let mutable commitHasBeenMade = false
    member val repo = _repo with get
    member val diff = _repo |> Repository.diff with get
    member val githubUrlFactory =
        match config.Network.Github.Url with
        | Auto ->
            githubRemote |> Option.map GitHub.UrlFactory
        | Manual s ->
            GitHub.UrlFactory s
            |> Some
        with get
    member val rootDir = dir with get
    member val config = config with get
    member this.GetAssemblyFileStats = cachedStats.AssemblyFiles.Count
    member this.GetVersionFileStats = cachedStats.Versions.Count
    member this.GetTagStats = cachedStats.Tags.Count
    interface System.IDisposable with
        member this.Dispose() =
            Repository.dispose _repo
    member this.Disposals = disposals
    member inline private this.StageFilesImpl(files: string list) =
        let index = this.repo |> Repository.index
        files
        |> List.map (fun file ->
            try
                index |> Index.addFile file
                Ok file
            with e ->
                Error (file, e)
            )
    /// <summary>
    /// Stages the given files for committing.
    /// </summary>
    /// <remarks>
    /// Pre GitNet 2.0.4, this would never raise an exception, and would instead log this to console and continue.
    /// This behaviour has been changed to raise an exception. Use <c>TryStageFiles</c> if you want to catch the exception
    /// </remarks>
    /// <param name="files"></param>
    member this.StageFiles(files: string list) =
        let index = this.repo |> Repository.index
        this.StageFilesImpl files
        |> function
            | results when results |> List.forall _.IsOk |> not ->
                results
                |> List.iter(function Ok _ -> () | Error (file, e) -> printfn $"Failed to stage file %s{file}:\n %A{e}")
                failwith $"Failed to stage files %A{files}"
            | _ ->
                index |> Index.write
    /// <summary>
    /// Stages the given files for committing.
    /// Returns an error if either one of the files fails to stage or the index cannot be written.
    /// </summary>
    /// <param name="files"></param>
    member this.TryStageFiles(files: string list) =
        let index = this.repo |> Repository.index
        this.StageFilesImpl files
        |> function
            | results when results |> List.forall _.IsOk |> not ->
                results
                |> List.choose (function Ok _ -> None | Error (file, e) -> Some (file, e))
                |> Choice1Of2 
                |> Error
            | _ ->
                try
                    index |> Index.write
                    Ok files
                with e ->
                    Choice2Of2 e
                    |> Error
                    
    /// <summary>
    /// Commits staged files.
    /// </summary>
    /// <remarks>
    /// Pre GitNet 2.0.4, this would never raise an exception, and would instead log this to console and continue.
    /// This behaviour has been changed to raise an exception. Use <c>TryCommitChanges</c> if you want to catch the exception
    /// with a Result DU.
    /// </remarks>
    /// <param name="username">Git username</param>
    /// <param name="email">Git email</param>
    /// <param name="message">Message for commit</param>
    /// <param name="date">Date</param>
    /// <param name="appendCommit">Whether to append to last commit</param>
    /// <param name="writeIndex">Whether to write the index before committing. Default <c>true</c></param>
    member this.CommitChanges(?username,?email,?message: string, ?date: DateTimeOffset, ?appendCommit: bool, ?writeIndex) =
        let appendCommit = appendCommit |> Option.defaultValue commitHasBeenMade
        let username = defaultArg username "GitHub Action"
        let email = defaultArg email "41898282+github-actions[bot]@users.noreply.github.com"
        let date = defaultArg date DateTimeOffset.Now
        let message = defaultArg message "[skip ci]\n\nGitNet auto file update."
        let writeIndex = defaultArg writeIndex true
        let signature = Signature(name = username, email = email, ``when`` = date)
        if writeIndex then this.repo.Index.Write()
        this
            .repo
            .Commit(message, signature, signature, CommitOptions(AllowEmptyCommit = false, AmendPreviousCommit = appendCommit))
        |> ignore
        commitHasBeenMade <- true
    /// <summary>
    /// Commits staged files.
    /// Returns an error if the commit fails.
    /// </summary>
    /// <param name="username">Git username</param>
    /// <param name="email">Git email</param>
    /// <param name="message">Message for commit</param>
    /// <param name="date">Date</param>
    /// <param name="appendCommit">Whether to append to last commit</param>
    /// <param name="writeIndex">Whether to write the index before committing. Default <c>true</c></param>
    member this.TryCommitChanges(?username, ?email, ?message: string, ?date: DateTimeOffset, ?appendCommit: bool, ?writeIndex) =
        try
            this.CommitChanges(
                ?username = username,
                ?email = email,
                ?message = message,
                ?date = date,
                ?appendCommit = appendCommit,
                ?writeIndex = writeIndex
                )
            |> Ok
        with e -> Error e
    member inline private this.CommitTagsImpl(tags: SepochSemver seq) =
        tags
        |> Seq.map (fun sepochSemver ->
            try
            let semverString = sepochSemver.ToString()
            Repository.applyTag
                semverString
                this.repo
            |> ignore
            cacheTag semverString
            Ok sepochSemver
            with
            | e -> Error (sepochSemver, e)
            )
        |> Seq.toList
    /// <summary>
    /// Tags the current head of the repository with the given semvers.
    /// </summary>
    /// <remarks>
    /// Tags must still be pushed to the repository to have effect.
    /// </remarks>
    /// <param name="tags"></param>
    member this.CommitTags(tags: SepochSemver seq) =
        this.CommitTagsImpl tags
        |> List.iter(function
            | Error e ->
                let tag = fst e
                match snd e with
                | :? NameConflictException as e ->
                    printfn $"Duplicate tag %A{tag}:\n%A{e}"
                | e -> raise e
            | Ok _ -> ()
            )
    /// <summary>
    /// Tags the current head of the repository with the given semvers.
    /// </summary>
    /// <remarks>
    /// Tags must still be pushed to the repository to have effect.
    /// </remarks>
    /// <param name="tags"></param>
    member this.TryCommitTags(tags: SepochSemver seq) = this.CommitTagsImpl tags
        
    member internal this.CategoriseCommits(commits: GitNetCommit seq) =
        let matcher = this.config.Output.ComputeGroupMatcher
        commits
        |> Seq.map (GitNetCommit.parsed >> matcher)
        |> Seq.zip commits
    member internal this.StatAssemblyFile = cacheAssemblyFile
    member internal this.StatVersionFile = cacheVersionFile
    member internal this.StatTag = cacheTag
    member internal this.AddToCacheRuns = cacheRun
    /// <summary>
    /// A Run/DryRun computes the versions of scopes from the git history of commits and tags.
    /// This computes are cached and can be utilised by users.
    /// </summary>
    member this.GetLastRun() = getLastCacheRun()
    /// <summary>
    /// A Run/DryRun computes the versions of scopes from the git history of commits and tags.
    /// This computes are cached and can be utilised by users.
    /// </summary>
    member this.GetRuns() = getCacheRuns()
    
module internal Runtime =
    let computeEpochFooterMatcher (runtime: GitNetRuntime): Footer -> bool =
        let runtimeEpochMatches: string -> bool = fun value ->
            runtime.config.Bump.Mapping.Epoch
            |> List.map _.Value
            |> List.contains value
        function
        | Footer(key,_) ->
            runtimeEpochMatches key
        | BreakingChange _ ->
            runtimeEpochMatches Spec.BreakingChangeKey
    let computeEpochFooterPicker (runtime: GitNetRuntime) =
        let matcher = computeEpochFooterMatcher runtime
        fun footers -> footers |> Seq.tryFind matcher
    let computeEpochValuePicker runtime =
        computeEpochFooterPicker runtime >> Option.map(
            function
                | Footer(_,value) -> value
                | BreakingChange(value) -> value
                )
    let computeParsedCommitBumpMatcher (runtime: GitNetRuntime) =
        runtime.config.Bump.Mapping.ComputeBumpMatcher
/// <summary>
/// Checks the <c>ParsedCommit</c> against the <c>GitNetConfig</c> to
/// determine if the commit would indicate a bump, and if so, of what type.
/// </summary>
/// <remarks>
/// <para>Automatically returns <c>ValueNone</c> for UnconventionalCommits currently.</para>
/// <para>Checks in order of precedence:<br/>
/// <c>Epoch > Major > Minor > Patch</c></para>
/// </remarks>
/// <param name="runtime">The provider for the values to match against the bump types.</param>
let willBump (runtime: GitNetRuntime) =
    let epochMatcher = Runtime.computeEpochFooterMatcher runtime
    function
    | Conventional { Footers = footers } | Breaking { Footers = footers }
        when footers |> List.exists epochMatcher ->
        let epochPicker = Runtime.computeEpochValuePicker runtime
        footers
        |> epochPicker
        |> Option.get
        |> BumpType.Epoch
        |> ValueSome
    | Breaking _ ->
        BumpType.Major
        |> ValueSome
    | value ->
        let parsedCommitMatcher = Runtime.computeParsedCommitBumpMatcher runtime
        value
        |> parsedCommitMatcher
        |> ValueOption.ofOption
