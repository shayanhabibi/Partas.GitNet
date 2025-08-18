module Partas.GitNet.Program

open System.Collections.Generic
open FSharp.Formatting.Markdown
open LibGit2Sharp.FSharp
open Partas.GitNet.BuildHelpers
open Partas.GitNet.GitCollection
open Partas.GitNet.MarkdownWriter
open Partas.GitNet.Renderer.Render
open Partas.GitNet.RepoCracker
open Partas.Tools.SepochSemver

type GitNetRuntime with
    member this.Run() =
        let tagCommitCollection = TagCommitCollection.load this
        let render = fromTagCommitCollection this tagCommitCollection
        let renderMarkup = writeRendering this render
        let markdownString =
            renderMarkup.Document
            |> Markdown.ToMd
        let projs =
            this.CrackRepo
            |> Seq.filter _.IsFSharp
            |> Seq.choose (function
                CrackedProject.FSharp proj ->
                    match proj.GitNetOptions with
                    | { Scope = Some scope; InitialVersion = Some initVersion } ->
                        KeyValuePair(scope, initVersion)
                        |> Some
                    | _ -> None
                | _ -> None
                    )
            |> toFrozenDictionary
        let initialVersionForScope (scope: string) =
            let success,initVers = projs.TryGetValue(scope)
            if success then
                {
                    Sepoch = Sepoch.Scope scope
                    SemVer = initVers
                }
                |> Some
            else
            match this.config.InitialVersionStrategy with
            | ProjectInitialVersionStrategy.Simple s ->
                try
                parseSepochSemver s
                |> Some
                with _ -> None
            | ProjectInitialVersionStrategy.None -> None
            | ProjectInitialVersionStrategy.Mapping dictionary ->
                let success,result = dictionary.TryGetValue(scope)
                if success then
                    try parseSepochSemver result |> Some
                    with _ -> None
                else None
            | ProjectInitialVersionStrategy.MappingOrSimple(mapping, fallback) ->
                try
                let success,result = mapping.TryGetValue(scope)
                if success
                then
                    try parseSepochSemver result |> Some
                    with _ ->
                        parseSepochSemver fallback |> Some
                else
                    parseSepochSemver fallback |> Some
                with _ -> None

        let bumps =
            renderMarkup.ScopeBumps
            |> Seq.map (fun keyVal ->
                let scope = keyVal.Key
                let bumpResult = keyVal.Value
                match bumpResult with
                | NoHistory _ ->
                    scope,initialVersionForScope scope
                | WithHistory(bump, history) ->
                    match bump,history with
                    | _, GitNetTag.GitTag value ->
                        failwithf $"A non-semver tag has leaked through, please raise an issue: %A{value.CanonicalName}"
                    | BumpType.Patch, GitNetTag.GitNetTag { SepochSemver = semver } ->
                        scope,
                        semver
                        |> SepochSemver.bumpPatch
                        |> Some
                    | BumpType.Minor, GitNetTag.GitNetTag { SepochSemver = semver } ->
                        scope,
                        semver
                        |> SepochSemver.bumpMinor
                        |> Some
                    | BumpType.Major, GitNetTag.GitNetTag { SepochSemver = semver } ->
                        scope,
                        semver
                        |> SepochSemver.bumpMajor
                        |> Some
                        
                    | BumpType.Epoch s, GitNetTag.GitNetTag { SepochSemver = semver } ->
                        scope,
                        s |> SepochSemver.bumpEpoch semver
                        |> Some
                    | _, GitNetTag.SemVerTag { Semver = semver } ->
                        scope,
                        {
                            Sepoch = Sepoch.Scope scope
                            SemVer = semver
                        }
                        |> match bump with
                           | BumpType.Patch ->
                               SepochSemver.bumpPatch
                           | BumpType.Minor ->
                               SepochSemver.bumpMinor
                           | BumpType.Major -> SepochSemver.bumpMajor
                           | BumpType.Epoch s -> fun semver -> SepochSemver.bumpEpoch semver s
                        |> Some
                |> function
                    | scope, None ->
                        failwithf $"Failed to attain an initial version for %A{scope}. Please ensure your initial versions are parseable, otherwise raise an issue."
                    | scope,Some nekVersion ->
                        scope, nekVersion
                )
            |> dict
        bumps,markdownString
        
open FSharp.Data
[<EntryPoint>]
let main args =
    // let path = @"C:\Users\shaya\RiderProjects\Partas.Solid.Primitives\"
    // let path = @"C:/Users/shaya/riderprojects/partas.gitnet/tests/partas.gitnet.tests/partas.solid.testground/"
    // let path = @"C:/Users/shaya/riderprojects/FullPerla/"
    let path = @"C:/Users/shaya/riderprojects/oxpecker.solid.jitbox/"
    {
        GitNetConfig.init true with
            RepositoryPath = path
            Output.Ignore =
                IgnoreCommit.SkipCi ::
                Defaults.ignoreCommits
    }
    |> fun config ->
        let runtime = new GitNetRuntime(config)
        let bumps,content = runtime.Run()
        // runtime.CommitTags(bumps.Values)
        // runtime.CommitChanges("shayanhabibi","shayanftw@gmail.com")
        content
        |> printfn "%A"
        bumps
        |> printfn "%A"
        runtime.WriteAssemblyFiles(bumps)
        runtime.StageVersionProjects(bumps)
        runtime.CommitChanges("GitHub Actions", "noreply@github.com")
        // let bumps,content = runtime.Run()
        // content
        // |> printfn "%A"
        // bumps
        // |> printfn "%A"
    0
