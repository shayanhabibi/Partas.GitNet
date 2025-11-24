module Build

open System.IO
open EasyBuild.FileSystemProvider
open Fake.Core
open Fake.IO

[<Literal>]
let private rootPath = __SOURCE_DIRECTORY__ + "/.."
type Files = AbsoluteFileSystem<rootPath>

module Ops =
    [<Literal>]
    let Clean = "Clean"
    [<Literal>]
    let Build = "Build"
    [<Literal>]
    let GitNet = "GitNet"
    [<Literal>]
    let Pack = "Pack"
    [<Literal>]
    let Publish = "Publish"
    [<Literal>]
    let GitPush = "GitPush"

module Args =
    let mutable local: bool = false
    let mutable apiKey: string option = None
    let mutable parallelise: bool = false
    let setArgs args =
        let containsArgs arg =
            args |> Array.contains arg
        let getArgValue arg =
            args
            |> Array.tryFindIndex ((=) arg)
            |> Option.map ((+) 1)
            |> Option.bind(fun idx ->
                Array.tryItem idx args)
        parallelise <- containsArgs "--parallel"
        apiKey <- getArgValue "--nuget-api-key"
        local <- containsArgs "--local"

let githubUsername = "GitHub Action"
let githubEmail = "41898282+github-actions[bot]@users.noreply.github.com"

// Credit SAFE STACK
let initializeContext () =
    let execContext = Context.FakeExecutionContext.Create false "build.fsx" []
    Context.setExecutionContext (Context.RuntimeContext.Fake execContext)

let createProcess exe args dir =
    CreateProcess.fromRawCommand exe args
    |> CreateProcess.withWorkingDirectory dir
    |> CreateProcess.ensureExitCode
    
let dotnet args dir = createProcess "dotnet" args dir

module Array =
    let inline iterMaybeParallel<'T>: ('T -> unit) -> 'T array -> unit = if Args.parallelise then Array.Parallel.iter else Array.iter

initializeContext()

open Partas.GitNet
open Partas.GitNet.RepoCracker

let projects = [|
    Files.``Partas.GitNet``.``Partas.GitNet.fsproj``
    Files.``Partas.Tools.SepochSemver``.``Partas.Tools.SepochSemver.fsproj``
    Files.``Partas.GitNet.Markdown``.``Partas.GitNet.Markdown.fsproj``
|]

let gitnetConfig = {
    GitNetConfig.initFSharp with
        RepositoryPath = Files.``.``
        Output.Ignore = Defaults.ignoreCommits @ [
            IgnoreCommit.SkipCi
        ]
        Bump.DefaultBumpStrategy = ForceBumpStrategy.All
        Projects =
            let ignoreProject: string -> string = Path.GetFileNameWithoutExtension
            {
                ProjectConfig.init with
                    IgnoredProjects = List.map ignoreProject [
                        Files.``Build.fsproj``
                        Files.``Partas.GitNet.Cli``.``Partas.GitNet.Cli.fsproj``
                        Files.Tests.``Partas.GitNet.Tests``.``Partas.GitNet.Tests.fsproj``
                        Files.Tests.``Partas.Tools.SepochSemver.Tests``.``Partas.Tools.SepochSemver.Tests.fsproj``
                    ]
            }
}
let runtime = lazy new GitNetRuntime(gitnetConfig)
let crackedProjects =
    lazy
        runtime.Value
        |> _.CrackRepo
        |> Seq.choose (
            function
                | { GitNetOptions = { Scope = Some _ } } as proj ->
                    Some proj
                | _ -> None)

open Fake.IO.Globbing.Operators
open Fake.Tools

Target.create Ops.Clean <| fun args ->
    task {
        crackedProjects.Value
        |> ignore
    } |> ignore
    !! "**/**/bin"
    -- "bin"
    ++ "temp/"
    |> Shell.cleanDirs
    
Target.create Ops.GitPush <| fun args ->
    if Args.local then
        [ $"config --local user.email \"{githubEmail}\""
          $"config --local user.user \"{githubUsername}\"" ]
        |> List.iter (Git.CommandHelper.directRunGitCommandAndFail Files.``.``)
    Git.Branches.push Files.``.``
    Git.CommandHelper.directRunGitCommandAndFail Files.``.`` "push --tags origin"
    
Target.create Ops.GitNet <| fun args ->
    let runtime = runtime.Value
    if Args.local then
        let bumps,content =
            runtime.DryRun() |> function { Bumps = bumps; Markdown = content } -> bumps,content
        bumps
        |> Seq.map (fun keyval ->
            (keyval.Key, keyval.Value.SemVer.ToString(), keyval.Value.ToString())
            |||> sprintf "Scope: %s | Next: %s | SepochSemver: %s")
        |> Trace.logItems "GitNet"
        runtime.WriteToOutput content
        |> ignore
    else
        runtime.Run(githubUsername, githubEmail)
        |> ignore

open Fake.DotNet
Target.create Ops.Build <| fun args ->
    projects
    |> Array.iterMaybeParallel (
        DotNet.build (fun p ->
            { p with
                Configuration = DotNet.BuildConfiguration.Release
                DotNet.BuildOptions.MSBuildParams.DisableInternalBinLog = true }
            ) 
        )
Target.create Ops.Pack <| fun args ->
    projects
    |> Array.iterMaybeParallel (
        DotNet.pack (fun p ->
            {p with
                NoRestore = true
                OutputPath = Some "bin"
                DotNet.PackOptions.MSBuildParams.DisableInternalBinLog = true })
        )

Target.create Ops.Publish <| fun args ->
    !! "bin/*.nupkg"
    |> Seq.toArray
    |> Array.iterMaybeParallel (
        DotNet.nugetPush (fun p ->
            { p with
                DotNet.NuGetPushOptions.PushParams.Source = Some "https://api.nuget.org/v3/index.json"
                DotNet.NuGetPushOptions.Common.CustomParams = Some "--skip-duplicate"
                DotNet.NuGetPushOptions.PushParams.ApiKey = Args.apiKey })
        )

open Fake.Core.TargetOperators

let dependencyMapping = [
    Ops.Clean
    ==> Ops.GitNet
    ==> Ops.Build
    ==> Ops.Pack
    ==> Ops.GitPush
    ==> Ops.Publish
]

[<EntryPoint>]
let main args =
    args |> Args.setArgs
    args[0] |> Target.runOrDefaultWithArguments
    0
