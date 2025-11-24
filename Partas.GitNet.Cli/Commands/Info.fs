module Commands.Info

open Partas.GitNet
open Partas.GitNet.Cli
open Partas.GitNet.Cli.Utils
open Partas.GitNet.GitCollection
open Partas.GitNet.MarkdownWriter
open Partas.GitNet.Renderer
open Partas.GitNet.RepoCracker
open Spectre.Console.Cli
open SpectreCoff
open Output

type ProjectInfo = {
    Scope: string
    Path: string
}
type GitNetInfo = {
    ``Git Path``: string
    ``Root Directory``: string
    Projects: ProjectInfo[]
}

type InfoSettings() =
    inherit CommandSettings()
    [<CommandOption("-i|--ignoreProject")>]
    member val ignoredProjects: string array = [||] with get,set
    [<CommandArgument(0,"[path]")>]
    member val path = System.Environment.CurrentDirectory with get,set
    override self.Validate() =
        if self.path = System.Environment.CurrentDirectory then
            try
                self.path <- resolvePath self.path
                Spectre.Console.ValidationResult.Success()
            with
            _ -> 
                Spectre.Console.ValidationResult.Error($"Git discovery along path %s{self.path} failed.")
        elif System.IO.Path.Exists(self.path) then
            Spectre.Console.ValidationResult.Success()
        else
            Spectre.Console.ValidationResult.Error($"Path doesn't exist: %s{self.path}")

type Info() =
    inherit Command<InfoSettings>()
    interface ICommandLimiter<InfoSettings>
    
    override this.Execute(context, settings) =
        let runtime = new GitNetRuntime({
            GitNetConfig.initFSharp with
                RepositoryPath = settings.path
                Projects =
                    {
                        Defaults.FSharp.projectFSharpConfig with
                            AutoScoping = function
                                | "Partas.GitNet" ->
                                    Some "Partas"
                                | _ -> None
                            IgnoredProjects = [
                                "Partas.Solid.Tests.Plugin"
                                "ScratchTests"
                                "Partas.Solid.Tests.Plugin"
                                "Partas.Solid.Tests.Core"
                                "Partas.Solid.FablePlugin"
                                "Partas.Solid"
                                "Partas.Solid.Tests"
                            ]
                    }
                    |> Some
                    |> ProjectType.FSharp
        })
        {
            ``Root Directory`` = runtime.rootDir
            ``Git Path`` = runtime.repo.Info.Path
            Projects =
                runtime.CrackRepo
                |> Seq.choose (
                    CrackedProject.getFSharp
                    >> ValueOption.filter _.GitNetOptions.Scope.IsSome
                    >> ValueOption.map (function
                        { CrackedProject.ProjectFileName = projectFileName
                          CrackedProject.GitNetOptions = { Scope = scope } } ->
                            {
                                Path = System.IO.Path.GetRelativePath(runtime.rootDir, projectFileName)
                                Scope = scope.Value
                            }
                        )
                    >> ValueOption.toOption
                    )
                |> Seq.toArray
        }
        |> Dumpify.customDump {
            Dumpify.defaultOptions with
                TypeNames = Dumpify.TypeNamingConfig(ShowTypeNames = false) |> Some
        }
        |> ignore
        0

type ``Project Info`` = CrackedProject
type ``Projects Info`` = ``Project Info``[]

type Projects() =
    inherit Command<InfoSettings>()
    interface ICommandLimiter<InfoSettings>
    override this.Execute(context, settings) =
        let runtime = new GitNetRuntime({
            GitNetConfig.initFSharp with
                RepositoryPath = settings.path
        })
        runtime.CrackRepo
        |> Seq.toArray : ``Projects Info``
        |> Dumpify.customDump {
            Dumpify.defaultOptions with
                TypeNames = Dumpify.TypeNamingConfig(ShowTypeNames = false) |> Some
        }
        |> ignore
        0

type CommitsSettings() =
    inherit InfoSettings()
    [<CommandOption("--scope")>]
    member val scope = false with get,set
    

type Commits() =
    inherit Command<CommitsSettings>()
    interface ICommandLimiter<InfoSettings>
    override this.Execute (context: CommandContext, settings): int =
        let runtime = new GitNetRuntime({
            GitNetConfig.initFSharp with
                RepositoryPath = settings.path
        })
        TagCommitCollection.load runtime
        |> Render.fromTagCommitCollection runtime
        |> MarkdownWriter.writeRendering runtime
        |> Dumpify.dump
        |> ignore
        0
