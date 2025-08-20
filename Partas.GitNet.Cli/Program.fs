open Partas.GitNet.ConfigTypes.GitNetConfig
open Spectre.Console.Cli
open Commands
open FSharp.Data.JsonSchema
open NJsonSchema

[<EntryPoint>]
let main argv =
    let app = CommandApp()
    app.Configure(fun config ->
        config.AddCommand<Greet.Hello>("greet")
            .WithAlias("g")
            .WithDescription("Greets the user running the application.")
            |> ignore
        config.AddBranch("info", fun(branchConfig: IConfigurator<Info.InfoSettings>) ->
            branchConfig.SetDescription("Get information about the GitNet environment")
            branchConfig
                .SetDefaultCommand<Info.Info>()
            branchConfig
                .AddCommand<Info.Projects>("projects")
                .WithAlias("p")
                .WithDescription("Shows information about the projects in the GitNet path.")
                |> ignore
            branchConfig
                .AddCommand<Info.Commits>("commits")
                .WithAlias("c")
                .WithDescription("Compute the collection of tags and commits")
                |> ignore
            ) |> ignore
        config.AddBranch("config", fun(branchConfig: IConfigurator<Config.NewConfigSettings>) ->
            branchConfig.SetDescription "Create a config for GitNet cli"
            branchConfig.SetDefaultCommand<Config.NewConfig>()
            ) |> ignore
        config.AddBranch("run", fun(branchConfig: IConfigurator<GitNet.GitNetSettings>) ->
            branchConfig.SetDescription "Run GitNet"
            branchConfig.SetDefaultCommand<GitNet.GitNet>()
            ) |> ignore
        )

    let schema = typeof<ConfigWrapper.GitNetConfig> |> Generator.Create()
    schema
    |> _.ToJson()
    |> fun content -> System.IO.File.WriteAllText(
        System.IO.Path.Combine(__SOURCE_DIRECTORY__, "test.json"), content
        )

    app.Run(argv)
