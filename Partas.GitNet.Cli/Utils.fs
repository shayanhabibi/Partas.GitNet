module Partas.GitNet.Cli.Utils

open LibGit2Sharp.FSharp
open Partas.GitNet
open SpectreCoff

let resolvePath =
    Repository.discover

module Option =
    let tap func = function
        | Some value ->
            func value
            Some value
        | None -> None

module ConfigFile =
    open Fake.IO.Globbing.Operators
    open Fake.IO
    let tryFind path =
        let historicalDir = System.Environment.CurrentDirectory
        try
            try
                let repoPath =
                    Repository.discover path
                    |> System.IO.Path.TrimEndingDirectorySeparator
                    |> System.IO.Directory.GetParent
                    |> _.ToString()
                System.Environment.CurrentDirectory <- repoPath
                (!! "**/gitnet.config.json")
                |> Seq.tryHead
            with
            | e ->
                e.Message
                |> edgy
                |> printMarkedUp
                None
        finally
            System.Environment.CurrentDirectory <- historicalDir
