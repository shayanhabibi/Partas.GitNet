[<AutoOpen>]
module Partas.GitNet.GitNetRuntime

open System
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
    do
        if
            _repo |> Repository.info
            |> RepositoryInformation.isBare
        then
            failwith "GitNet is not compatible with Bare repos yet. Please raise an issue https://github.com/shayanhabibi/Partas.GitNet"
    let githubRemote =
        _repo |> Repository.network
        |> Network.remotes
        |> Seq.tryFind(
            Remote.url
            >> _.Contains("github.com")
            )
        |> Option.map GitHubRemote
    let dir =
        _repo |> Repository.info
        |> RepositoryInformation.workingDirectory
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
    interface System.IDisposable with
        member this.Dispose() =
            Repository.dispose _repo
            
    member this.Disposals = disposals
    member internal this.WriteCommitToMarkdown scope commit =
        writeCommitMessage scope commit
    member this.CommitChanges(username,email,?message: string, ?date: DateTimeOffset) =
        let date = defaultArg date DateTimeOffset.Now
        let message = defaultArg message "[skip ci]\n\nGitNet auto file update."
        let signature = Signature(name = username, email = email, ``when`` = date)
        try
        this
            .repo
            .Commit(message, signature, signature, CommitOptions(AllowEmptyCommit = false))
        |> ignore        
        with e ->
            e
            |> printfn "Error while committing changes: %A"
    member this.CommitTags(tags: SepochSemver seq) =
        tags
        |> Seq.iter (fun sepochSemver ->
            
            try
            Repository.applyTag
                (sepochSemver.ToString())
                this.repo
            |> ignore
            with
            | :? NameConflictException as e ->
                printfn $"Duplicate tag %A{sepochSemver}:\n%A{e}"
            )
        
    member this.CategoriseCommits(commits: GitNetCommit seq) =
        let config = this.config.Output
        commits
        |> Seq.map (GitNetCommit.parsed >> config.ComputeGroupMatcher)
        |> Seq.zip commits

// let mutable private cachedRuntime = Unchecked.defaultof<GitNetRuntime>
// let mutable private _savedRuntime = false
// type Cache =
//     static member load =
//         if _savedRuntime then
//             cachedRuntime
//         else
//             failwith "You must save a runtime before loading it from the cache"
//     static member tryLoad =
//         if _savedRuntime then
//             Some cachedRuntime
//         else None
//     static member save runtime =
//         _savedRuntime <- true
//         cachedRuntime <- runtime

module Footer =
    let getValueForKey key = function
        | ParsedCommit.Unconventional _ -> ValueNone
        | ParsedCommit.Breaking { Footers = footers }
        | ParsedCommit.Conventional { Footers = footers } ->
            footers
            |> List.find ((fun x -> x :> IFooter) >> _.Key >> (=) key)
            |> fun footer -> footer :> IFooter |> _.Value
            |> ValueSome
    let containsKey key = getValueForKey key >> _.IsSome

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
    let footerContainsEpoch (footers: Footer list) =
        footers
        |> List.tryPick (function
            | Footer(key,value) when
                runtime.config.Bump.Mapping.Epoch
                |> List.map _.Value
                |> List.contains key -> Some value
            | _ -> None)
    function
    | Conventional { Footers = footers } | Breaking { Footers = footers }
        when footerContainsEpoch footers |> _.IsSome ->
        footerContainsEpoch footers
        |> _.Value
        |> BumpType.Epoch
        |> ValueSome
    | Breaking _ ->
        BumpType.Major |> ValueSome
    | value ->
        let matcher = runtime.config.Bump.Mapping.ComputeBumpMatcher
        value |> matcher
        |> ValueOption.ofOption
