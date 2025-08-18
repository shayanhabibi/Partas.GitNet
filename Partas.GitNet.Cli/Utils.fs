module Partas.GitNet.Cli.Utils

open LibGit2Sharp.FSharp
open Partas.GitNet

let resolvePath =
    Repository.discover
