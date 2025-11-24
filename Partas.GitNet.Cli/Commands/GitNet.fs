module Commands.GitNet

open Partas.GitNet
open System.IO
open FSharp.Data
open Fake.Core
open Partas.GitNet
open LibGit2Sharp
open LibGit2Sharp.FSharp
open Partas.GitNet.Cli
open Partas.GitNet.Cli.Utils
open Partas.GitNet.GitCollection
open Partas.GitNet.MarkdownWriter
open Partas.GitNet.Renderer
open Partas.GitNet.RepoCracker
open Spectre.Console
open Spectre.Console.Cli
open SpectreCoff
open Output
open ConfigWrapper
open Fake.IO

let defaultOutputPath = Path.combine System.Environment.CurrentDirectory "RELEASE_NOTES.md"

type GitNetSettings() =
    inherit CommandSettings()
    let validateOutputPath (path: string) =
        try Path.GetExtension(path) |> ValueOption.ofObj
        with _ -> ValueNone
        |> ValueOption.map(fun _ -> path)
        |> ValueOption.defaultValue
            (Path.combine path "RELEASE_NOTES.md")
    let validateScopeStrategy = String.toLower >> function
        | null -> ValueNone |> Ok
        | "auto" | "a" -> ValueSome ScopeStrategy.Auto |> Ok
        | "explicit" | "expl" -> ValueSome ScopeStrategy.Explicit |> Ok
        | "none" | "null" -> ValueSome ScopeStrategy.None |> Ok
        | _ -> Error ()
    [<CommandOption("-b|--bump")>]
    member val bump: bool = false with get,set
    [<CommandOption("-o|--output <PATH>")>]
    member val outputPath: string = null with get,set
    [<CommandOption("-i|--ignore-project <PROJECT>")>]
    member val ignoreProjects: string[] = [||] with get,set
    [<CommandOption("-t|--target <PROJECT>")>]
    member val target: string
    [<CommandOption("--scope-strategy <STRAT>")>]
    member val rawScopeStrategy: string = null with get,set
    [<CommandArgument(0,"[PATH]")>]
    member val path: string = null with get,set
    member val scopeStrategy: ScopeStrategy voption = ValueNone with get,set
    override this.Validate() =
        if isNull this.outputPath |> not then
            this.outputPath <- validateOutputPath this.outputPath
        let path =
            if isNull this.path then
                System.Environment.CurrentDirectory
            else this.path
            |> Repository.discover
        if not <| Repository.isValid path then
            ValidationResult.Error("The current path is not in a git repository")
        elif this.rawScopeStrategy |> isNull then
            ValidationResult.Success()
        else
        match validateScopeStrategy this.rawScopeStrategy with
        | Error _ ->
            Many [ BL; BL
                   MarkupCD(Color.Red3_1, [Decoration.Bold], "Error:")
                   V "Invalid"
                   C "gitnet run"
                   V "option"
                   C "--scope-strategy"
                   V "value."
                   NL
                   V "Expected one of"
                   C "auto"
                   V "|"
                   C "explicit"
                   V "|"
                   C "none"
                   V "but was given"
                   P this.rawScopeStrategy
                   V "instead" ] |> toConsole
            ValidationResult.Error
                $"Invalid 'gitnet run' option '--scope-strategy' value."
        | Ok scopeStrategy when scopeStrategy.IsSome ->
            this.scopeStrategy <- scopeStrategy
            ValidationResult.Success()
        | Ok _ ->
            ValidationResult.Success()
open ConfigWrapper
type GitNet() =
    inherit Command<GitNetSettings>()
    interface ICommandLimiter<GitNetSettings>
    
    override this.Execute(_context, _settings) =
        let maybeConfig = ConfigSchema.find _settings.path
        match maybeConfig with
        | Some { ConfigWrapper.GitNetConfig.Paths = paths
                 FSharp = fsharp
                 Commits = commits
                 AutoBump = autoBump
                 GithubUrl = githubUrl
                 Projects = proj
                 ScopingStrategy = scopStrat } ->
            let scopeStrat =
                _settings.scopeStrategy
                |> ValueOption.toOption
                |> Option.orElse scopStrat
                |> Option.defaultValue Defaults.FSharp.scopeStrategy
            let autoBump =
                if _settings.bump then
                    Some ForceBumpStrategy.All
                else None
                |> Option.orElse autoBump
                |> Option.defaultValue Defaults.forceBumpStrategy
            let projectType =
                match fsharp,proj with
                | Some { FSharpConfig.Ignored = ignored
                         AssemblyFiles = assemblyFiles // preference F#
                         NamingResolution = namingResolution }, _ ->
                    let assemblyFiles =
                        assemblyFiles
                        |> Option.defaultValue
                            Defaults.FSharp.assemblyFileManagement
                    let namingResolution =
                        namingResolution
                        |> Option.defaultValue
                            Defaults.FSharp.fsharpNameResolution
                    let ignored = ignored
                    { ProjectFSharpConfig.init with
                          NameResolution = namingResolution
                          IgnoredProjects = ignored |> Array.toList }
                    |> Some
                    |> ProjectType.FSharp,
                    assemblyFiles
                | None, Some proj ->
                    ProjectType.None proj, Defaults.FSharp.assemblyFileManagement
                | _ -> ProjectFSharpConfig.init |> Some |> ProjectType.FSharp, Defaults.FSharp.assemblyFileManagement
            let ignoredCommits =
                commits
                |> Option.bind _.Ignored
                |> Option.defaultValue (Defaults.ignoreCommits |> List.toArray)
            let bumpMapping =
                commits
                |> Option.bind (
                    _.BumpMapping
                    >> Option.bind( fun mapping ->
                        let getDefaultFor func =
                            func Defaults.commitBumpTypeMapping
                        let epoch =
                            mapping.Epoch
                            |> Option.defaultValue (getDefaultFor _.Epoch)
                        let major =
                            mapping.Major
                            |> Option.defaultValue (getDefaultFor _.Major)
                        let minor =
                            mapping.Minor
                            |> Option.defaultValue(getDefaultFor _.Minor)
                        let patch =
                            mapping.Patch
                            |> Option.defaultValue(getDefaultFor _.Patch)
                        Some { CommitBumpTypeMapping.Epoch = epoch; Major = major; Minor = minor; Patch = patch }
                        )
                )
                |> Option.defaultValue Defaults.commitBumpTypeMapping
            let githubUrl =
                githubUrl
                |> Option.map GithubUrlStrategy.Manual
                |> Option.defaultValue Defaults.githubConfig.Url
            let repoPath =
                _settings.path
                |> Option.ofObj
                |> Option.orElse (paths |> Option.bind _.Repository)
                |> Option.defaultValue System.Environment.CurrentDirectory
                |> Repository.discover
            let outputPath =
                _settings.outputPath
                |> Option.ofObj
                |> Option.map OutputPathType.Simple
                |> Option.orElse (paths |> Option.bind _.Output |> Option.map OutputPathType.Simple)
                |> Option.defaultValue (Defaults.outputPathType defaultOutputPath)
            {
                GitNetConfig.init (fst projectType |> _.IsFSharp) with
                    RepositoryPath = repoPath
                    AssemblyFiles = snd projectType
                    ProjectType = fst projectType
                    Network = { Github = {
                        Url = githubUrl
                    } }
                    Bump.Mapping = bumpMapping
                    Bump.DefaultBumpStrategy = autoBump
                    Output.Path = outputPath
                    Output.Ignore = ignoredCommits |> Array.toList
                    Scope = scopeStrat
            }
        | None ->
            let scopeStrat =
                _settings.scopeStrategy
                |> ValueOption.defaultValue Defaults.FSharp.scopeStrategy
            let autoBump =
                if _settings.bump then
                    Some ForceBumpStrategy.All
                else None
                |> Option.defaultValue Defaults.forceBumpStrategy
            let projectType =
                Defaults.FSharp.projectType (Some Defaults.FSharp.projectFSharpConfig)
            let repoPath =
                _settings.path
                |> Option.ofObj
                |> Option.defaultValue System.Environment.CurrentDirectory
                |> Repository.discover
            let outputPath =
                _settings.outputPath
                |> Option.ofObj
                |> Option.map OutputPathType.Simple
                |> Option.defaultValue (Defaults.outputPathType defaultOutputPath)
            {
                GitNetConfig.init true with
                    RepositoryPath = repoPath
                    ProjectType = projectType
                    Bump.DefaultBumpStrategy = autoBump
                    Output.Path = outputPath
                    Scope = scopeStrat
            }
        |> fun config -> new GitNetRuntime(config)
        |> if _settings.bump then _.Run()
            else _.DryRun()
        |> fun (bumps,content) ->
            bumps
            |> function
                bumps when bumps.Count > 0 ->
                    bumps
                    |> Dumpify.dump
                    |> ignore
                | _ -> ()
            content
        |> File.writeString false defaultOutputPath
        0
