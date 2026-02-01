[<AutoOpen>]
module Partas.GitNet.RunExtensions

open System.Collections.Frozen
open System.Collections.Generic
open FSharp.Formatting.Markdown
open LibGit2Sharp.FSharp
open Partas.GitNet.BuildHelpers
open Partas.GitNet.GitCollection
open Partas.GitNet.MarkdownWriter
open Partas.GitNet.Renderer.Render
open Partas.GitNet.RepoCracker
open Partas.Tools.SepochSemver

type RuntimeOutput = {
    Versions: FrozenDictionary<string, GitNetTag voption>
    Bumps: IDictionary<string, SepochSemver>
    Markdown: string
}

type GitNetRuntime with
    /// <summary>
    /// Writes the given string to the output path in the <c>GitNetConfig</c>.
    /// </summary>
    /// <param name="content">The content to write to the output path</param>
    member this.WriteToOutput(content: string) =
        this.config
        |> function
            | Patterns.OutputPath path ->
                let path = path ""
                Fake.IO.File.writeString false path content
                path
    /// <summary>
    /// Writes the given string to the output path in the <c>GitNetConfig</c> and stages the file for committing.
    /// </summary>
    /// <remarks>
    /// Pre GitNet 2.0.4, this would never fail. It will not fail if it fails to stage the file
    /// for committing. Use <c>TryWriteToOutputAndStage</c> if you want to handle this case with a Result DU.
    /// </remarks>
    /// <param name="content">The content to write to the output path.</param>
    member this.WriteToOutputAndStage(content: string) =
        let path = this.WriteToOutput content
        this.repo
        |> Repository.index
        |> fun idx ->
            Index.addFile (System.IO.Path.GetRelativePath(this.rootDir, path)) idx
            Index.write idx
    /// <summary>
    /// Writes the given string to the output path in the <c>GitNetConfig</c> and stages the file for committing.
    /// Returns a Result DU indicating whether the write succeeded or not.
    /// </summary>
    /// <param name="content">The content to write to the output path.</param>
    member this.TryWriteToOutputAndStage(content: string) =
        try
            this.WriteToOutputAndStage content
            |> Ok
        with e -> Error e
    /// <summary>
    /// Writes the given string to the output path in the <c>GitNetConfig</c> and commits the changes.
    /// Will fail if the output path is not staged for committing.
    /// </summary>
    /// <param name="content">The content to write to the output path.</param>
    member this.WriteToOutputAndCommit(content: string) =
        this.WriteToOutputAndStage content
        this.CommitChanges()
    member this.TryWriteToOutputAndCommit(content: string) =
        this.TryWriteToOutputAndStage content
        |> Result.bind (fun _ -> this.TryCommitChanges())
        
    member private this.DryRunImpl() =
        let tagCommitCollection = TagCommitCollection.load this
        let render = fromTagCommitCollection this tagCommitCollection
        let renderMarkup = writeRendering this render
        let projs =
            this.CrackRepo
            |> Seq.choose (_.GitNetOptions >> function
                    | { Scope = Some scope; InitialVersion = Some initVersion } ->
                        KeyValuePair(scope, initVersion)
                        |> Some
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
                    | BumpType.Patch, GitNetTag.SepochTag(sepochSemver = semver) ->
                        scope,
                        semver
                        |> SepochSemver.bumpPatch
                        |> Some
                    | BumpType.Minor, GitNetTag.SepochTag(sepochSemver = semver) ->
                        scope,
                        semver
                        |> SepochSemver.bumpMinor
                        |> Some
                    | BumpType.Major, GitNetTag.SepochTag(sepochSemver = semver) ->
                        scope,
                        semver
                        |> SepochSemver.bumpMajor
                        |> Some
                        
                    | BumpType.Epoch s, GitNetTag.SepochTag(sepochSemver = semver) ->
                        scope,
                        s |> SepochSemver.bumpEpoch semver
                        |> Some
                    | _, GitNetTag.SemVerTag(semver = semver) ->
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
        let versions = render.Versions |> this.AddToCacheRuns
        bumps,renderMarkup, versions
    member this.DryRun() =
        let bumps,content,versions = this.DryRunImpl()
        let markdownString =
            content.Document
            |> Markdown.ToMd
        {
            Markdown = markdownString
            Bumps = bumps
            Versions = versions
        }
    /// <summary>
    /// Executes a run.
    /// </summary>
    /// <param name="bumps"></param>
    /// <param name="content"></param>
    /// <param name="versions"></param>
    /// <param name="username"></param>
    /// <param name="email"></param>
    /// <param name="commit"></param>
    /// <param name="append"></param>
    /// <param name="stageFile"></param>
    member private this.RunImpl(bumps: IDictionary<string, SepochSemver>,content,versions, ?username: string, ?email: string, ?commit: bool, ?append: bool, ?stageFile: bool) =
        let commit = defaultArg commit true
        let stageFile = defaultArg stageFile true
        let username = defaultArg username "GitHub Action"
        let email = defaultArg email "41898282+github-actions[bot]@users.noreply.github.com"
        let matchesRepoBranch = this.repo |> Repository.head |> Branch.name |> (=)
        let taggedSemvers=
            this.CrackRepo
            |> Seq.choose(
                    CrackedProject.gitNetOptions
                    >> function
                        {
                            AutoBump = true
                            AutoBumpBranchName = branchName
                            Scope = scope
                        } when (
                             let branchNameIsFine = branchName
                                                    |> Option.map matchesRepoBranch
                                                    |> Option.defaultValue true
                             let bumpContainsScope =
                                 match scope with
                                 | Some key -> bumps.ContainsKey(key) 
                                 | _ -> false
                             branchNameIsFine && bumpContainsScope
                        ) -> Some bumps[scope.Value]
                        | _ -> None
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
        | AssemblyFileManagement.UpdateIfExists when taggedSemvers |> Array.isEmpty |> not ->
            this.WriteAssemblyFiles(taggedSemvers, stageFiles = true)
            if this.config.WriteVersionToProjects then
                this.VersionProjects(
                    taggedSemvers
                    |> Array.choose(function
                        | { Sepoch = sepoch } as semver when sepoch.GetScope.IsSome ->
                            Some (sepoch.GetScope.Value, semver)
                        | _ -> None
                            )
                    |> dict,
                    stageFile = stageFile
                    )
            if commit then
                this.CommitChanges(username, email, ?appendCommit = append)
                this.CommitTags(taggedSemvers)
            let result = this.DryRun()
            #if DEBUG
            (this.GetAssemblyFileStats, this.GetTagStats, this.GetVersionFileStats)
            |||> printfn "STATS:\nWritten %i assembly files\nWritten %i tags\nOverwritten %i project files"
            #endif
            result
        | AssemblyFileManagement.None when taggedSemvers |> Array.isEmpty |> not ->
            if this.config.WriteVersionToProjects then
                this.VersionProjects(
                    taggedSemvers
                    |> Array.choose(function
                        | { Sepoch = sepoch } as semver when sepoch.GetScope.IsSome ->
                            Some (sepoch.GetScope.Value, semver)
                        | _ -> None
                            )
                    |> dict,
                    stageFile = stageFile
                    )
                if commit then
                    this.CommitChanges(username, email, ?appendCommit = append)
            if commit then
                this.CommitTags(taggedSemvers)
            let result = this.DryRun()
            #if DEBUG
            (this.GetAssemblyFileStats, this.GetTagStats, this.GetVersionFileStats)
            |||> printfn "STATS:\nWritten %i assembly files\nWritten %i tags\nOverwritten %i project files"
            #endif
            result
        | _ ->
            {
                Bumps = bumps
                Markdown = content.Document |> Markdown.ToMd
                Versions = versions
            }
        |> fun ({ Markdown = markdown } as result) ->
            if commit then
                this.WriteToOutputAndCommit(markdown)
            else
                this.WriteToOutput(markdown)
                |> ignore
            result
    /// <summary>
    /// Run GitNet, versioning according to the provided config AutoBumps etc.
    /// </summary>
    /// <param name="username">Git username</param>
    /// <param name="email">Git email</param>
    /// <param name="commit"></param>
    /// <param name="appendCommit"></param>
    /// <param name="stageFile"></param>
    member this.Run(?username: string,?email: string, ?commit: bool, ?appendCommit: bool, ?stageFile: bool) =
        let bumps,content,versions = this.DryRunImpl()
        this.RunImpl(bumps,content,versions,?username = username, ?email = email, ?commit = commit, ?append = appendCommit, ?stageFile = stageFile)
    /// <summary>
    /// Remap a gitnet run with the planned bumps and current computed versions of the scopes.
    /// </summary>
    /// <param name="mapping">A mapping function which provides the bumps to execute; ie, the sepoch semvers to version.</param>
    /// <param name="username">Git username</param>
    /// <param name="email">Git email</param>
    /// <param name="commit"></param>
    /// <param name="appendCommit"></param>
    /// <param name="stageFile"></param>
    member this.Run(
            mapping: IDictionary<string, SepochSemver> -> FrozenDictionary<string, GitNetTag voption> -> IDictionary<string, SepochSemver>,
            ?username,
            ?email,
            ?commit: bool,
            ?appendCommit: bool,
            ?stageFile: bool
        ) =
        let bumps,content,versions = this.DryRunImpl()
        let bumps = mapping bumps versions
        this.RunImpl(bumps, content, versions, ?username = username, ?email = email, ?commit = commit, ?append = appendCommit, ?stageFile = stageFile)
