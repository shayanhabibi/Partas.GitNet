[<AutoOpen>]
module Partas.GitNet.RepoCracker

open System.Collections.Generic
open System.IO
open System.Xml.Linq
open System.Xml.XPath
open FsToolkit.ErrorHandling
open Fake.Core
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.DotNet
open Partas.Tools.SepochSemver
open Semver

module internal Projects =
    let private findFsProjs (runtime: GitNetRuntime) =
        !! $"**/*.fsproj"
        |> _.SetBaseDirectory(runtime.rootDir)
    let private extractElementsValues elementName: MSBuildProject -> string seq =
        _.Document
            .XPathSelectElements($"//{elementName}")
        >> Seq.map _.Value
    let private extractElementValue elementName: MSBuildProject -> string voption =
        _.Document
            .XPathSelectElement($"//{elementName}")
        >> ValueOption.ofObj
        >> ValueOption.map _.Value
    let private extractElementsAttributes elementName attributeName: MSBuildProject -> string seq =
        _.Document
            .XPathSelectElements($"//{elementName}[@{attributeName}]")
        >> Seq.map (_.XPathEvaluate($"string(@{attributeName})") >> unbox<string>)
    let findProjects: GitNetRuntime -> string seq = findFsProjs >> seq
    let loadProject = MSBuild.loadProject
    let findProjectsAndLoad: GitNetRuntime -> (MSBuildProject * string) seq =
        let makeResult path = loadProject path,path
        findProjects >> Seq.map makeResult
    let getSourceFiles: MSBuildProject -> string seq = extractElementsAttributes "Compile" "Include"
    let tryGetScope: MSBuildProject -> string voption = extractElementValue "GitNetScope"
    let tryGetEpoch: MSBuildProject -> string voption = extractElementValue "GitNetEpoch"
    let tryGetAutoBump: MSBuildProject -> bool voption =
        extractElementValue "GitNetAutoBump"
        >> ValueOption.map (fun inp ->
            let result = System.Boolean.TryParse(inp)
            if fst result then snd result else false)
    let tryGetInitialVersion: MSBuildProject -> SemVersion voption =
        extractElementValue "GitNetInitialVersion"
        >> ValueOption.map SemVersion.Parse
    let tryGetAutoBumpBranchName: MSBuildProject -> string voption =
        extractElementValue "GitNetAutoBumpBranchName"
    let tryGetVersion: MSBuildProject -> SemVersion voption = fun proj ->
        extractElementValue "Version" proj
        |> ValueOption.orElse (extractElementValue "PackageVersion" proj)
        |> ValueOption.map SemVersion.Parse
    let tryGetAutoUpdateAssemblyFiles: MSBuildProject -> string voption = extractElementValue "GitNetAutoUpdateAssemblyFiles"
    let tryGetTitle project: string voption =
        extractElementValue "Title" project
        |> ValueOption.orElse (extractElementValue "PackageId" project)
module internal Config =
    let nameResolver (config: ProjectConfig) (path: string) (msbuildProj: MSBuildProject): string voption =
        let makeResolver =
            let fileNameResolver =
                lazy
                ValueSome <| Path.GetFileNameWithoutExtension(path)
            let titleResolver =
                lazy
                Projects.tryGetTitle msbuildProj
            let directoryResolver =
                lazy
                Path.GetDirectoryName(path)
                |> ValueSome
            let autoResolver =
                lazy (titleResolver.Value |> ValueOption.orElse fileNameResolver.Value)
            function
            | FSharpNameResolution.Auto ->
                autoResolver
            | FSharpNameResolution.FileName ->
                fileNameResolver
            | FSharpNameResolution.Title ->
                titleResolver
            | FSharpNameResolution.Directory ->
                directoryResolver
        config.NameResolution
        |> makeResolver
        |> _.Value
    let findAssemblyFile: string seq -> string option =
        Seq.tryFind(Path.GetFileName >> function
                    | "AssemblyFile.fs"
                    | "AssemblyInfo.fs" -> true
                    | _ -> false)
    let createAssemblyInfoIntoProject assemblyFileRelativePath projectAbsolutePath =
        let assemblyFileAbsolutePath =
            Path.combine
                (Path.getDirectory projectAbsolutePath)
                assemblyFileRelativePath
        let proj = MSBuild.loadProject projectAbsolutePath
        proj.Document
        |> _.XPathSelectElements("//Compile[@Include]")
        |> function
            | elements when
                Projects.getSourceFiles proj
                |> findAssemblyFile
                |> Option.isNone ->
                elements
                |> Seq.tryLast
                |> Option.iter(fun lastCompileNode ->
                    AssemblyInfoFile.createFSharp assemblyFileAbsolutePath [
                        AssemblyInfo.InformationalVersion "0.0.0"
                    ]
                    let assemblyFileNode =
                        let topNode = XElement("Compile")
                        topNode.Add(XAttribute("Include", assemblyFileRelativePath))
                        topNode
                    lastCompileNode.AddAfterSelf assemblyFileNode
                    )
                proj.Save projectAbsolutePath
                assemblyFileAbsolutePath
            | _ -> assemblyFileAbsolutePath
    let tryCreateAssemblyInfoIntoProject assemblyFileRelativePath projectAbsolutePath =
        try
            createAssemblyInfoIntoProject assemblyFileRelativePath projectAbsolutePath
            |> ValueSome 
        with _ ->
            ValueNone

open Config

module internal Path =
    /// Returns a function that will map any path relative to the provided root.
    let inline computeRelativeMapper (root: string): string -> string =
        fun path -> Path.GetRelativePath(root, path)

module internal CrackedProject =
    module GitNetOptions =
        open CrackedProject

        let create scope initialVersion autoBump epoch autobranchName version =
            {
                Scope = scope
                InitialVersion = initialVersion
                AutoBump = autoBump
                Epoch = epoch
                AutoBumpBranchName = autobranchName
                Version = version
            }
        let inline tryParseSemver input =
            let success,result = SemVersion.TryParse input
            if success then Some result else None
        let inline computeConfigInitialVersionScopeMapper config: string option -> _ =
            let inline tryRetrieveSemverFromDict (dictionary: IDictionary<_,_>) key =
                match dictionary.TryGetValue(key) with
                | true, semver -> Some semver
                | _ -> None
            match config.InitialVersionStrategy with
            | ProjectInitialVersionStrategy.Simple semver ->
                fun _ -> Some semver
            | ProjectInitialVersionStrategy.Mapping dictionary ->
                fun scope ->
                    scope
                    |> Option.bind (tryRetrieveSemverFromDict dictionary)
            | ProjectInitialVersionStrategy.MappingOrSimple(mapping, fallback) ->
                let fallbackVers = Some fallback
                fun scope ->
                    scope
                    |> Option.bind
                        (tryRetrieveSemverFromDict mapping)
                    |> Option.orElse fallbackVers
        let getInitialVersionForProjectWithScope project (configInitialVersionMapper: string option -> SemVersion option) scope =
            project
            |> Projects.tryGetInitialVersion
            |> ValueOption.toOption
            |> Option.orElse
                (configInitialVersionMapper scope)
        let inline computeConfigAutoBumpScopeMatcher config: string option -> bool =
            match config.Bump.DefaultBumpStrategy with
            | ForceBumpStrategy.ForScopes l ->
                let listContains key = List.contains key l 
                fun scope ->
                    scope
                    |> Option.map listContains
                    |> Option.defaultValue false
            | ForceBumpStrategy.Auto
            | ForceBumpStrategy.All -> fun _ -> true
            | ForceBumpStrategy.None -> fun _ -> false
        let getShouldBumpForProjectWithScope project (configAutoBumpMapper: string option -> bool) scope =
            project
            |> Projects.tryGetAutoBump
            |> ValueOption.toOption
            |> Option.defaultValue
                   (configAutoBumpMapper scope)
    let inline private computeProjectIgnorer { Projects = { IgnoredProjects = projects } } =
        match projects with
        | [] -> None
        | _ -> Some(fun input -> List.contains input projects)
    let inline private computeScopeAutoFactory (config: ProjectConfig) proj projectPath =
        match config with
        | { OverrideExplicitScopes = true; AutoScoping = func } ->
            nameResolver config projectPath proj
            |> ValueOption.toOption
            |> Option.bind func
        | { AutoScoping = func } ->
            nameResolver config projectPath proj
            |> ValueOption.toOption
            |> Option.orElse (
                nameResolver config projectPath proj
                |> ValueOption.toOption
                )
            |> Option.bind func
    /// From the given config, it will precompute a function which determines the scope
    /// from a given project and projectPath
    let inline computeConfigScopeFactory (config: GitNetConfig) =
        let projectIgnorer =
            match computeProjectIgnorer config with
            | Some func ->
                fun (proj: MSBuildProject) (projectPath: string) handler ->
                    if
                        Path.GetFileNameWithoutExtension(projectPath)
                        |> func
                    then None
                    else handler proj projectPath
            | None ->
                fun proj projectPath handler ->
                    handler proj projectPath
        let autoHandler =
            computeScopeAutoFactory config.Projects
        match config.Scope with
        | ScopeStrategy.None ->
            fun _ _ ->
                None
        | ScopeStrategy.Auto ->
            fun proj projectPath ->
                projectIgnorer proj projectPath autoHandler
        | ScopeStrategy.Explicit ->
            fun proj projectPath ->
                fun proj _ ->
                    Projects.tryGetScope proj
                    |> ValueOption.toOption
                |> projectIgnorer proj projectPath 
    type private ScopeFactoryFunc = MSBuildProject -> string -> string option
    type private AutoBumpScopeFunc = string option -> bool
    type private InitialVersionScopeFunc = string option -> SemVersion option
    /// <remarks>
    /// The precomputed functions are overridden by project file configurations if present. We extract the relevant
    /// project file element, and if it is absent, we instead apply the config computed function.
    /// </remarks>
    /// <param name="config">Config used in request for <c>CrackedProject</c></param>
    /// <param name="scoper">Function pre-computed from config that maps a project file to the scope name.</param>
    /// <param name="autoBumper">Function pre-computed from config that determines whether to auto bump a scope.</param>
    /// <param name="initVersioner">Function pre-computed from config that determines the initial version of a scope.</param>
    /// <param name="repoDir">The repository base path</param>
    /// <param name="path">The path of the <c>.fsproj</c> file</param>
    let create
        (config: GitNetConfig)
        (scoper: ScopeFactoryFunc)
        (autoBumper: AutoBumpScopeFunc)
        (initVersioner: InitialVersionScopeFunc)
        (repoDir: string) (path: string) =
        let proj = MSBuild.loadProject path
        let absoluteProjectDirectory = Path.getDirectory path
        let relativeToRepo = Path.computeRelativeMapper repoDir
        let relativeFromProject = Path.combine absoluteProjectDirectory
        let makeRelativeFromRepo = relativeFromProject >> relativeToRepo >> String.replace "\\" "/"
        let sourceFiles = proj |> Projects.getSourceFiles
        let assemblyFile =
            match config.AssemblyFiles with
            | AssemblyFileManagement.Create
            | AssemblyFileManagement.UpdateIfExists ->
                findAssemblyFile sourceFiles
            | AssemblyFileManagement.None -> None
        let scope = scoper proj path
        let autoBump =
            (proj, autoBumper, scope)
            |||> GitNetOptions.getShouldBumpForProjectWithScope
        let epoch = proj |> Projects.tryGetEpoch |> ValueOption.toOption
        let initialVersion =
            (proj, initVersioner, scope)
            |||> GitNetOptions.getInitialVersionForProjectWithScope
        let autoBumpBranchName = Projects.tryGetAutoBumpBranchName proj |> ValueOption.toOption
        let version = proj |> Projects.tryGetVersion |> ValueOption.toOption
        {
            RepoRoot = repoDir
            ProjectDirectory = makeRelativeFromRepo absoluteProjectDirectory
            ProjectFileName = makeRelativeFromRepo path
            SourceFiles = Seq.toList sourceFiles
            AssemblyFile = assemblyFile
            GitNetOptions = GitNetOptions.create scope initialVersion autoBump epoch autoBumpBranchName version
        }

[<RequireQualifiedAccess>]
module CrackRepo =
    type Error =
        | NoScope
        | NoAssemblyFileFound
        | NoSepochSemver
        | Exn of exn
        

type GitNetRuntime with
    /// <summary>
    /// <para>Explores and provides the projects from the repository.</para>
    /// <para>The data is computed on demand, so care must be taken when writing to disk and subsequently recomputing
    /// the project information.</para>
    /// </summary>
    member this.CrackRepo =
        // Function that determines whether a scope should be bumped
        let autoBumper =
            this.config
            |> CrackedProject
                .GitNetOptions
                .computeConfigAutoBumpScopeMatcher
        // Function that determines a scopes initialVersion mapping if possible
        let initialVersioner =
            this.config
            |> CrackedProject
               .GitNetOptions
               .computeConfigInitialVersionScopeMapper
        // Function that determines the scope for a project
        let scoper =
            this.config
            |> CrackedProject.computeConfigScopeFactory
        // Function that creates a cracked project from the project path
        let makeCrackedProj =
            CrackedProject.create this.config scoper autoBumper initialVersioner this.rootDir
        Projects.findProjects this
        |> Seq.map makeCrackedProj 
                
    /// <summary>
    /// Given a sequence of <c>SepochSemver</c>'s, will generate <c>AssemblyFile</c>'s using the
    /// scope from the input to match against the project directory.
    /// </summary>
    /// <param name="versions">The sequence of <c>SepochSemver</c>'s to operate on. Scopeless Semvers will have no effect.</param>
    /// <param name="stageFiles">Whether to subsequently stage the generated files (Default <c>false</c>).</param>
    member this.WriteAssemblyFiles (versions: SepochSemver seq, ?stageFiles: bool) =
        let stageFiles = defaultArg stageFiles false
        this.CrackRepo
        |> Seq.map(function
            | { GitNetOptions = { Scope = None } } -> CrackRepo.Error.NoScope |> Error
            | { AssemblyFile = None } when this.config.AssemblyFiles.IsCreate |> not -> CrackRepo.Error.NoAssemblyFileFound |> Error
            | { ProjectDirectory = path
                AssemblyFile = assemblyFile
                GitNetOptions = { Scope = Some scop } } as proj ->
                match
                    versions
                    |> Seq.tryFind(
                        _.Sepoch.GetScope
                        >> ValueOption.exists((=) scop)
                        )
                with
                | Some sepochSemver ->
                    try
                    let assemblyPath =
                        match assemblyFile with
                        | Some assemblyFile -> 
                            Path.Combine(this.rootDir, path, assemblyFile)
                        | None ->
                            createAssemblyInfoIntoProject
                                "AssemblyInfo.fs"
                                (Path.combine this.rootDir proj.ProjectFileName)
                    let attrs =
                        if assemblyFile.IsSome then
                            AssemblyInfoFile.getAttributes assemblyPath
                        else
                            seq {}
                    attrs
                    |> Seq.filter (_.Name >> function
                        "AssemblyInformationalVersion"
                        | "AssemblyVersion"
                        | "AssemblyFileVersion" -> false
                        | _ -> true)
                    |> Seq.append [
                        AssemblyInfo.FileVersion <| sepochSemver.SemVer.ToString()
                        AssemblyInfo.Version <| sepochSemver.SemVer.ToString()
                        AssemblyInfo.InformationalVersion <| sepochSemver.SemVer.ToString()
                    ]
                    |> AssemblyInfoFile.createFSharp assemblyPath
                    if stageFiles then
                        this.repo.Index.Add (Path.GetRelativePath(this.rootDir, assemblyPath))
                        this.repo.Index.Write()
                    this.StatAssemblyFile assemblyPath
                    Ok()
                    with e ->
                        e
                        |> printfn "%A"
                        CrackRepo.Error.Exn e
                        |> Error
                | _ -> CrackRepo.Error.NoSepochSemver |> Error
            )
    /// <summary>
    /// Given a dictionary of <c>Scope</c>'s to <c>SepochSemver</c>'s, will generate <c>AssemblyFile</c>'s using the
    /// scope from the values to match against the project directory.
    /// </summary>
    /// <param name="mapping">The dict of <c>SepochSemver</c>'s to operate on. Scopeless Semvers will have no effect.
    /// Since this is an alias to <c>_.WriteAssemblyFiles</c> of type <c>SepochSemver seq</c>, the keys of the map
    /// have no effect.</param>
    /// <param name="stageFiles">Whether to subsequently stage the generated files (Default <c>false</c>).</param>
    member this.WriteAssemblyFiles (mapping: IDictionary<string, SepochSemver>, ?stageFiles) =
        this.WriteAssemblyFiles(mapping.Values, ?stageFiles = stageFiles)
    /// <summary>
    /// <para>Convenience overload that accepts the output of <c>runtime.Run()</c> and
    /// uses the provided versions in the bump dictionary to update the assembly files
    /// before returning tuple for further processing.</para>
    /// <para>Does not stage the files.</para>
    /// </summary>
    member this.WriteAssemblyFiles(mapping: IDictionary<string, SepochSemver> * string) =
        mapping
        |> fst
        |> _.Values
        |> this.WriteAssemblyFiles
        #if DEBUG
        |> Seq.iter(function
            | Error (CrackRepo.Exn e) -> printfn $"%A{e}"
            | Error e -> printfn $"%A{e}"
            | _ -> ()
            )
        #else
        |> ignore
        #endif
        mapping
