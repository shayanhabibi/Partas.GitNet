[<AutoOpen>]
module Partas.GitNet.RunExtensions
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
    member this.WriteToOutput(content: string) =
        this.config
        |> function
            | Patterns.OutputPath path ->
                let path = path ""
                Fake.IO.File.writeString false path content
                path
    member this.WriteToOutputAndStage(content: string) =
        let path = this.WriteToOutput content
        try
        this.repo
        |> Repository.index
        |> Index.addFile (System.IO.Path.GetRelativePath(this.rootDir, path))
        with e ->
            e
            |> printfn "%A"
    member this.WriteToOutputAndCommit(content: string) =
        this.WriteToOutputAndStage content
        this.CommitChanges()
        
    member private this.DryRunImpl() =
        let tagCommitCollection = TagCommitCollection.load this
        let render = fromTagCommitCollection this tagCommitCollection
        let renderMarkup = writeRendering this render
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
            match projs.TryGetValue(scope) with
            | true, semver -> Some { Sepoch = Sepoch.Scope scope; SemVer = semver }
            | _ -> None

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
        bumps,renderMarkup
    member this.DryRun() =
        let bumps,content = this.DryRunImpl()
        let markdownString =
            content.Document
            |> Markdown.ToMd
        bumps,markdownString
    /// Acts upon 'autobump' and other settings
    member this.Run(?username: string,?email: string) =
        let username = defaultArg username "GitHub Action"
        let email = defaultArg email "41898282+github-actions[bot]@users.noreply.github.com"
        let bumps,content = this.DryRunImpl()
        let matchesRepoBranch = this.repo |> Repository.head |> Branch.name |> (=)
        let autoTaggedSemvers =
            this.CrackRepo
            |> Seq.choose(
                CrackedProject.getFSharp
                >> ValueOption.bind (
                    CrackedProject.FSharp.gitNetOptions
                    >> function
                        {
                            AutoBump = true
                            AutoBumpBranchName = branchName
                            Scope = scope
                        } when (branchName.IsNone
                             || branchName.Value |> matchesRepoBranch)
                             && bumps.ContainsKey(scope.Value) ->
                               ValueSome bumps[scope.Value]
                        | _ -> ValueNone
                    )
                >> ValueOption.toOption
                )
            |> Seq.toArray
        
        #if DEBUG
        match this.config.AssemblyFiles with
        | AssemblyFileManagement.Create when this.GetAssemblyFileStats > 0 ->
            this.GetAssemblyFileStats
            |> printfn "STATS:\nWritten %i assembly files"
        | _ -> ()
        #endif
        
        match this.config.AssemblyFiles with
        | AssemblyFileManagement.Create
        | AssemblyFileManagement.UpdateIfExists when autoTaggedSemvers |> Array.isEmpty |> not ->
            this.WriteAssemblyFiles(autoTaggedSemvers)
            this.VersionProjects(
                autoTaggedSemvers
                |> Array.choose(function
                    | { Sepoch = sepoch } as semver when sepoch.GetScope.IsSome ->
                        Some (sepoch.GetScope.Value, semver)
                    | _ -> None
                        )
                |> dict
                , true
                )
            this.CommitChanges(username, email)
            this.CommitTags(autoTaggedSemvers)
            let result = this.DryRun()
            #if DEBUG
            (this.GetAssemblyFileStats, this.GetTagStats, this.GetVersionFileStats)
            |||> printfn "STATS:\nWritten %i assembly files\nWritten %i tags\nOverwritten %i project files"
            #endif
            result
        | AssemblyFileManagement.None when autoTaggedSemvers |> Array.isEmpty |> not ->
            this.CommitTags(autoTaggedSemvers)
            let result = this.DryRun()
            #if DEBUG
            (this.GetAssemblyFileStats, this.GetTagStats, this.GetVersionFileStats)
            |||> printfn "STATS:\nWritten %i assembly files\nWritten %i tags\nOverwritten %i project files"
            #endif
            result
        | _ ->
            bumps, content.Document |> Markdown.ToMd
        |> function
            | bumps,content ->
                this.WriteToOutputAndCommit(content) |> ignore
                bumps,content
open FSharp.Data
// module Program =
//     [<EntryPoint>]
//     let main args =
//         // let path = @"C:\Users\shaya\RiderProjects\Partas.Solid.Primitives\"
//         // let path = @"C:/Users/shaya/riderprojects/partas.gitnet/tests/partas.gitnet.tests/partas.solid.testground/"
//         // let path = @"C:/Users/shaya/riderprojects/FullPerla/"
//         let path = @"C:/Users/shaya/riderprojects/oxpecker.solid.jitbox/"
//         {
//             GitNetConfig.init true with
//                 RepositoryPath = path
//                 Output.Ignore =
//                     IgnoreCommit.SkipCi ::
//                     Defaults.ignoreCommits
//                 AssemblyFiles = AssemblyFileManagement.Create
//                 // Bump.DefaultBumpStrategy = ForceBumpStrategy.All
//         }
//         |> fun config ->
//             let runtime = new GitNetRuntime(config)
//             // let bumps,content = runtime.DryRun()
//             // runtime.WriteAssemblyFiles(bumps)
//             // runtime.StageVersionProjects(bumps)
//             // runtime.CommitChanges("GitHub Actions", "noreply@github.com")
//             let bumps,content = runtime.Run()
//             content
//             |> printfn "%A"
//             bumps
//             |> printfn "%A"
//             
//         0
