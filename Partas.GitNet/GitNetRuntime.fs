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
    let writeCommitMessage =
        match config.Output.Formatting with
        | MacroGroupType.EpochAnd(MacroGroupType.ScopePrefix prefixConfig)
        | MacroGroupType.ScopePrefix prefixConfig ->
            match prefixConfig with
            | ScopePrefixConfig.Templated stringOptionFunc ->
                fun scope message ->
                    scope |> stringOptionFunc
                    |> Option.map (sprintf " %s")
                    |> Option.defaultValue ""
                    |> fun v -> $"%s{v}%s{message}"
            | ScopePrefixConfig.SquareBrackets ->
                fun scope message ->
                    scope |> Option.map (sprintf "[%s] ")
                    |> Option.defaultValue ""
                    |> fun v -> $"{v}{message}"
            | ScopePrefixConfig.Parenthesis ->
                fun scope message ->
                    scope |> Option.map (sprintf "(%s) ")
                    |> Option.defaultValue ""
                    |> fun v -> $"{v}{message}"
            | ScopePrefixConfig.AngleBrackets ->
                fun scope message ->
                    scope |> Option.map (sprintf "<%s> ")
                    |> Option.defaultValue ""
                    |> fun v -> $"{v}{message}"
            | ScopePrefixConfig.Label ->
                // TODO
                fun _ message -> message
            | ScopePrefixConfig.None ->
                fun _ message -> message
        | _ ->
            fun _ message -> message
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
    member internal this.WriteCommitToMarkdown scope commit =
        writeCommitMessage scope commit
    member this.CommitChanges(?username,?email,?message: string, ?date: DateTimeOffset, ?appendCommit: bool) =
        let appendCommit =
            if commitHasBeenMade then
                defaultArg appendCommit true
            else false
        let username = defaultArg username "GitHub Action"
        let email = defaultArg email "41898282+github-actions[bot]@users.noreply.github.com"
        let date = defaultArg date DateTimeOffset.Now
        let message = defaultArg message "[skip ci]\n\nGitNet auto file update."
        let signature = Signature(name = username, email = email, ``when`` = date)
        try
        this
            .repo
            .Commit(message, signature, signature, CommitOptions(AllowEmptyCommit = false, AmendPreviousCommit = appendCommit))
        |> ignore
        commitHasBeenMade <- true
        with e ->
            e
            |> printfn "Error while committing changes: %A"
    member this.CommitTags(tags: SepochSemver seq) =
        tags
        |> Seq.iter (fun sepochSemver ->
            try
            let sepochSemver = sepochSemver.ToString()
            Repository.applyTag
                sepochSemver
                this.repo
            |> ignore
            cacheTag sepochSemver
            with
            | :? NameConflictException as e ->
                printfn $"Duplicate tag %A{sepochSemver}:\n%A{e}"
            )
        
    member this.CategoriseCommits(commits: GitNetCommit seq) =
        let config = this.config.Output
        commits
        |> Seq.map (GitNetCommit.parsed >> config.ComputeGroupMatcher)
        |> Seq.zip commits
    member internal this.StatAssemblyFile = cacheAssemblyFile
    member internal this.StatVersionFile = cacheVersionFile
    member internal this.StatTag = cacheTag
    member internal this.AddToCacheRuns = cacheRun
    member this.GetLastRun() = getLastCacheRun()
    member this.GetRuns() = getCacheRuns()
    
module Runtime =
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
