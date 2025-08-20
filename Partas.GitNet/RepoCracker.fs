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

module Projects =
    let private findFsProjs (runtime: GitNetRuntime) =
        !! $"{runtime.rootDir |> String.trimSlash}/**/*.fsproj"
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
        extractElementValue "GitNetInitialVersion" >> ValueOption.map SemVersion.Parse
    let tryGetAutoBumpBranchName: MSBuildProject -> string voption =
        extractElementValue "GitNetAutoBumpBranchName"
    let tryGetAutoUpdateAssemblyFiles: MSBuildProject -> string voption = extractElementValue "GitNetAutoUpdateAssemblyFiles"
    let tryGetTitle project: string voption =
        extractElementValue "Title" project
        |> ValueOption.orElse (extractElementValue "PackageId" project)
module Config =
    let nameResolver (config: ProjectFSharpConfig option) (path: string) (msbuildProj: MSBuildProject): string voption =
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
        match config with
        | Some config ->
            config.NameResolution
            |> makeResolver
            |> _.Value
        | None ->
            FSharpNameResolution.Auto
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

module Path =
    /// Returns a function that will map any path relative to the provided root.
    let inline computeRelativeMapper (root: string): string -> string =
        fun path -> Path.GetRelativePath(root, path)

module CrackedProject =
    module GitNetOptions =
        open CrackedProject
        let create scope initialVersion autoBump epoch autobranchName =
            {
                Scope = scope
                InitialVersion = initialVersion
                AutoBump = autoBump
                Epoch = epoch
                AutoBumpBranchName = autobranchName
            }
        let inline tryParseSemver input =
            let success,result = SemVersion.TryParse input
            if success then Some result else None
        let inline computeConfigInitialVersionScopeMapper config: string option -> _ =
            let inline tryParseSemverFromDict (dictionary: IDictionary<_,_>) key =
                match dictionary.TryGetValue(key) with
                | true, str -> tryParseSemver str
                | _ -> None
            match config.InitialVersionStrategy with
            | ProjectInitialVersionStrategy.Simple str ->
                let initVers = tryParseSemver str
                fun _ -> initVers
            | ProjectInitialVersionStrategy.Mapping dictionary ->
                fun scope ->
                    scope
                    |> Option.bind
                           (tryParseSemverFromDict dictionary)
            | ProjectInitialVersionStrategy.MappingOrSimple(mapping, fallback) ->
                let fallbackVers = tryParseSemver fallback
                fun scope ->
                    scope
                    |> Option.bind
                        (tryParseSemverFromDict mapping)
                    |> Option.orElse fallbackVers
            | ProjectInitialVersionStrategy.None -> fun _ -> None
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
    let inline private computeProjectIgnorer config =
        match config.ProjectType with
        | ProjectType.FSharp (Some { IgnoredProjects = projects }) ->
            Some projects
        | _ -> None
        |> Option.map(fun projList ->
            fun input -> List.contains input projList)
    let inline private computeScopeAutoFactory (config: ProjectFSharpConfig option) proj projectPath =
        match config with
        | None ->
            Projects.tryGetScope proj
            |> ValueOption.orElse (nameResolver config projectPath proj)
            |> ValueOption.toOption
        | Some { OverrideExplicitScopes = true; AutoScoping = func } ->
            nameResolver config projectPath proj
            |> ValueOption.toOption
            |> Option.bind func
        | Some { AutoScoping = func } ->
            nameResolver config projectPath proj
            |> ValueOption.toOption
            |> Option.orElse (
                nameResolver config projectPath proj
                |> ValueOption.toOption
                )
            |> Option.bind func
    /// From the given config, it will precompute a function which determines the scope
    /// from a given project and projectPath
    let inline computeConfigScopeFactory config =
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
            computeScopeAutoFactory (config.ProjectType |> function ProjectType.FSharp proj -> proj | _ -> None)
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
        {
            ProjectDirectory = makeRelativeFromRepo absoluteProjectDirectory
            ProjectFileName = makeRelativeFromRepo path
            SourceFiles = Seq.toList sourceFiles
            AssemblyFile = assemblyFile
            GitNetOptions = GitNetOptions.create scope initialVersion autoBump epoch autoBumpBranchName
        }

type GitNetRuntime with
    member this.CrackRepo =
        match this.config.ProjectType with
        | ProjectType.FSharp _ ->
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
            |> Seq.map (makeCrackedProj >> CrackedProject.FSharp)
        | ProjectType.None projectNoneConfig ->
            projectNoneConfig.PathScopeMapping
            |> Seq.map (fun keyValue ->
                CrackedProject.NonFSharp {
                    ProjectDirectory = keyValue.Key
                    Scope = keyValue.Value
                })
    member this.WriteAssemblyFiles (versions: SepochSemver seq, ?stageFiles: bool) =
        let stageFiles = defaultArg stageFiles false
        this.CrackRepo
        |> Seq.choose(function
            | CrackedProject.FSharp proj -> Some proj
            | _ -> None)
        |> Seq.iter(function
            | { GitNetOptions = { Scope = None } } -> ()
            | { AssemblyFile = None } when this.config.AssemblyFiles.IsCreate |> not -> ()
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
                    this.StatAssemblyFile assemblyPath
                    with e ->
                        e
                        |> printfn "%A"
                | _ -> ()
            )
    member this.WriteAssemblyFiles (mapping: IDictionary<string, SepochSemver>, ?stageFiles) =
        this.WriteAssemblyFiles(mapping.Values, ?stageFiles = stageFiles)
    /// <summary>
    /// Convenience overload that accepts the output of <c>runtime.Run()</c> and
    /// uses the provided versions in the bump dictionary to update the assembly files
    /// before returning tuple for further processing.
    /// </summary>
    member this.WriteAssemblyFiles(mapping: IDictionary<string, SepochSemver> * string) =
        mapping
        |> fst
        |> _.Values
        |> this.WriteAssemblyFiles
        mapping
