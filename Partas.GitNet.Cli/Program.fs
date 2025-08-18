open Spectre.Console.Cli
open Commands

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
        )
    app.Run(argv)
