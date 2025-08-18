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

type GitNetInfo = {
    ``Git Path``: string
    ``Root Directory``: string
    Projects: string[]
}

type InfoSettings() =
    inherit CommandSettings()
    [<CommandOption("-p|--path")>]
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
            GitNetConfig.init with
                RepositoryPath = settings.path
        })
        {
            ``Root Directory`` = runtime.rootDir
            ``Git Path`` = runtime.repo.Info.Path
            Projects = runtime.CrackRepo |> Seq.map _.ProjectFileName |> Seq.toArray
        }
        |> Dumpify.dump
        |> ignore
        0

type ``Project Info`` = CrackedProject
type ``Projects Info`` = ``Project Info``[]

type Projects() =
    inherit Command<InfoSettings>()
    interface ICommandLimiter<InfoSettings>
    override this.Execute(context, settings) =
        let runtime = new GitNetRuntime({
            GitNetConfig.init with
                RepositoryPath = settings.path
        })
        runtime.CrackRepo
        |> Seq.toArray : ``Projects Info``
        |> Dumpify.dump
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
            GitNetConfig.init with
                RepositoryPath = settings.path
        })
        TagCommitCollection.load runtime
        |> Render.fromTagCommitCollection runtime
        |> MarkdownWriter.writeRendering
        |> Dumpify.dump
        |> ignore
        0
