module Commands.Config

open System.IO
open FSharp.Data
open Partas.GitNet
open LibGit2Sharp
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
let defaultPath =
    Path.combine System.Environment.CurrentDirectory "gitnet.config.json"
let defaultForceGenerateSchema = false
let defaultGenerateSchema = false
type NewConfigSettings() =
    inherit CommandSettings()
    [<CommandOption("--force")>]
    member val forceGenerateSchema: bool = defaultForceGenerateSchema with get,set
    [<CommandOption("-p|--path <PATH>")>]
    member val path: string = defaultPath
        with get,set
    [<CommandOption("--generate-schema")>]
    member val generateSchema: bool = defaultGenerateSchema with get,set
    // override this.Validate() =
    //     match this.generateSchema,this.forceGenerateSchema,this.path with
    //     | true, false, path when Path.Exists(path) ->
    //         ValidationResult.Error($"A file already exists at {path}. Schema generation is conservative. To force, use the `--force` flag.")
    //     | _, _, "" ->
    //         ValidationResult.Error("No path is provided, or the system could not detect the current working directory. Use the `--path` flag.")
    //     | false, false, path when File.exists path ->
    //         ValidationResult.Error($"This will overwrite {Path.GetFileName path}. Use the `--force` flag to overwrite an existing file.")
    //     | _, _, _ -> ValidationResult.Success()
type NewConfig() =
    inherit Command<NewConfigSettings>()
    let newConfig = "{
    
}"
    let generateConfig= function
        | "" -> failwith "Cannot write to an empty path"
        | path when Path.isFile path ->
            File.writeString false path newConfig
        | path when Path.isDirectory path ->
            File.writeString
                false
                (Path.combine path "gitnet.config.json")
                newConfig
        | path -> path |> failwithf "Cannot detect the path type of %s"
    let generateSchema = function
        | "" -> failwith "Cannot write to an empty path"
        | path when Path.isFile path ->
            ConfigWrapper.schema
            |> _.ToJson()
            |> File.writeString false path
        | path when Path.isDirectory path ->
            ConfigWrapper.schema
            |> _.ToJson()
            |> File.writeString
                    false
                    (Path.combine path "gitnet.schema.json")
        | path -> path |> failwithf "Cannot detect the path type of %s"
    interface ICommandLimiter<NewConfigSettings>

    override _.Execute(_context, settings) =
        if
            (settings.forceGenerateSchema = defaultForceGenerateSchema
              && settings.path = defaultPath
              && settings.generateSchema = defaultGenerateSchema)
            |> not
        then
            settings.path
            |> if settings.generateSchema
                then generateSchema
                else generateConfig
            0
        else
        let mutable repoPath = System.Environment.CurrentDirectory
        repoPath <-
            Many [
                C "Project"
                V "path:"
            ]
            |> toMarkedUpString
            |> askSuggesting System.Environment.CurrentDirectory
        BL |> toConsole
        while
            try
                let path = LibGit2Sharp.FSharp.Repository.discover repoPath
                if LibGit2Sharp.FSharp.Repository.isValid path then
                    repoPath <- path
                    false
                else true
            with _ -> true
            do
            repoPath <-
                $"Could not find a repository associated with {repoPath}:"
                |> appendNewline
                |> askSuggesting repoPath
        match
            Many [
                P "Project"
                V "type:"
            ] |> toMarkedUpString
            |> chooseFrom [ "FSharp" ; "Other" ]
        with
        | "FSharp" ->
            let runtime = new GitNetRuntime({
                GitNetConfig.initFSharp with
                    RepositoryPath = repoPath
            })
            let discoveredProjects =
                let asyncFunc (context: StatusContext) =
                    async {
                    updateWithCustomSpinner
                        { Message = "Discovering Projects"
                          Spinner = Some Spinner.Known.Arc
                          Look = Some { calmLook with Color = Some Color.Blue } }
                        context
                    |> ignore
                    return
                        runtime.CrackRepo
                        |> Seq.choose(
                             CrackedProject.getFSharp
                          >> ValueOption.filter _.GitNetOptions.Scope.IsSome
                          >> ValueOption.map (
                                 _.ProjectFileName
                              >> Path.GetFileNameWithoutExtension
                                 )
                          >> ValueOption.toOption
                             )
                        |> Seq.toList
                    }
                let runner = Status.start "Discover FSharp Projects" asyncFunc
                runner
                |> Async.RunSynchronously
            let ignoreProjects =
                {|
                    ``FSharp Projects`` = discoveredProjects
                |}
                |> Dumpify.customDump ({Dumpify.defaultOptions with TypeNames = Some <| Dumpify.TypeNamingConfig(SimplifyAnonymousObjectNames = true)})
                |> ignore
                if "Would you like to ignore any of the discovered projects?\n" |> confirm then
                    "Select which projects you would like ignored\n"
                    |> chooseMultipleFrom discoveredProjects
                    |> Seq.toList
                else
                    []
            let path =
                edgy "Write config to:"
                |> askSuggesting "gitnet.config.json"
            {
                ConfigWrapper.GitNetConfig.Paths = Some {
                    Paths.Repository = Some repoPath
                    Paths.Output = None
                }
                FSharp = Some {
                    Ignored =
                        ignoreProjects |> List.toArray
                    AssemblyFiles = None
                    NamingResolution = None
                }
                Projects = None
                GithubUrl = None
                AutoBump = None
                ScopingStrategy = None
                Commits = None
            }
            |> FSharp.Data.Json.Serialize
            |> File.writeString
                false
                (Path.combine runtime.rootDir path)
            0
        | _ -> 1
            
            
// type ConfigSettings() =
//     inherit CommandSettings()
    // [<CommandArgument>]
