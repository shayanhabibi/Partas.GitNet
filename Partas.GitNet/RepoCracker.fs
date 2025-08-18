module Partas.GitNet.RepoCracker

open System.Collections.Generic
open System.IO
open System.Xml.XPath
open FsToolkit.ErrorHandling
open LibGit2Sharp
open LibGit2Sharp.FSharp
open Fake.Core
open Fake.IO
open Fake.IO.Globbing
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
    let private findProjects: GitNetRuntime -> string seq = findFsProjs >> seq
    let private loadProject = MSBuild.loadProject
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
type GitNetRuntime with
    member this.CrackRepo =
        let projectDirectory = Path.getDirectory
        let relativeToRepoFromProject projectDir projectRelativePath =
            let absolutePath = Path.combine projectDir projectRelativePath
            Path.GetRelativePath(this.rootDir, absolutePath)
        let makeCrackedProj (proj: MSBuildProject, path: string) =
            let projectDirectory = path |> projectDirectory
            let relativeToRepo = relativeToRepoFromProject projectDirectory
            let sourceFiles = proj |> Projects.getSourceFiles
            {
                ProjectDirectory =
                    projectDirectory
                    |> relativeToRepo
                    |> String.replace "\\" "/"
                ProjectFileName = path |> relativeToRepo
                SourceFiles = sourceFiles |> Seq.toList
                AssemblyFile =
                    match this.config.AssemblyFiles with
                    | AssemblyFileManagement.Create
                    | AssemblyFileManagement.UpdateIfExists ->
                        sourceFiles
                        |> Seq.tryFind (fun path ->
                            let file = Path.GetFileName path
                            file = "AssemblyFile.fs" || file = "AssemblyInfo.fs")
                    | AssemblyFileManagement.None -> None
                GitNetOptions =
                    let scope =
                        let checkIgnored =
                            this.config.ProjectType
                            |> function
                                | ProjectType.FSharp(Some { IgnoredProjects = projs }) ->
                                    Some projs
                                | _ -> None
                            |> Option.map(fun l ->
                                fun s -> l |> List.contains s
                                )
                        match this.config.Scope with
                        | ScopeStrategy.None -> None
                        | ScopeStrategy.Auto ->
                            let fsProjConfigOptions =
                                this.config.ProjectType
                                |> function
                                    | ProjectType.FSharp value -> value
                                    | _ -> failwith "unreachable"
                            match fsProjConfigOptions with
                            | None -> 
                                Projects.tryGetScope proj
                                |> ValueOption.toOption
                                |> Option.orElse
                                    ( Config.nameResolver fsProjConfigOptions path proj
                                      |> ValueOption.toOption )
                            | Some { OverrideExplicitScopes = true; AutoScoping = func } ->
                                Config.nameResolver fsProjConfigOptions path proj
                                |> ValueOption.toOption
                                |> Option.bind func
                            | Some { AutoScoping = func } ->
                                Projects.tryGetScope proj
                                |> ValueOption.toOption
                                |> Option.orElse
                                    ( Config.nameResolver fsProjConfigOptions path proj
                                      |> ValueOption.toOption )
                                |> Option.bind func
                        | ScopeStrategy.Explicit
                        | ScopeStrategy.ExplicitOrUnscoped ->
                            Projects.tryGetScope proj
                            |> ValueOption.toOption
                        |> function
                            | _ when checkIgnored.IsSome
                                && Path.GetFileNameWithoutExtension(path)
                                   |> checkIgnored.Value -> None
                            | value -> value
                    {
                        InitialVersion =
                            match this.config.InitialVersionStrategy with
                            | ProjectInitialVersionStrategy.Simple s ->
                                let success,value = SemVersion.TryParse s
                                if success then Some value else None
                            | ProjectInitialVersionStrategy.Mapping dictionary when scope.IsSome ->
                                let success,value = SemVersion.TryParse dictionary[scope.Value]
                                if success then Some value else None
                            | ProjectInitialVersionStrategy.MappingOrSimple(mapping, fallback) ->
                                match scope with
                                | Some scope ->
                                    let success,value = SemVersion.TryParse mapping[scope]
                                    if success then Some value else
                                    let success,value = SemVersion.TryParse fallback
                                    if success then Some value else None
                                | _ ->
                                    let success,value = SemVersion.TryParse fallback
                                    if success then Some value else None
                            | _ -> None
                            |> Option.orElse
                               (proj
                                |> Projects.tryGetInitialVersion
                                |> ValueOption.toOption) 
                        AutoBump =
                            proj
                            |> Projects.tryGetAutoBump
                            |> ValueOption.defaultValue
                               (match this.config.Bump.DefaultBumpStrategy with
                                | ForceBumpStrategy.ForScopes l ->
                                    scope
                                    |> Option.map(fun s -> l |> List.contains s)
                                    |> Option.defaultValue false
                                | ForceBumpStrategy.All -> true
                                | ForceBumpStrategy.Auto -> false)
                        Scope = scope
                        Epoch =
                            proj
                            |> Projects.tryGetEpoch
                            |> ValueOption.toOption
                    }
            }
        match this.config.ProjectType with
        | ProjectType.FSharp _ ->
            Projects.findProjectsAndLoad this
            |> Seq.map (makeCrackedProj >> CrackedProject.FSharp)
        | ProjectType.None projectNoneConfig ->
            projectNoneConfig.PathScopeMapping
            |> Seq.map (fun keyValue ->
                CrackedProject.NonFSharp {
                    ProjectDirectory = keyValue.Key
                    Scope = keyValue.Value
                })
    member this.WriteAssemblyFiles (versions: SepochSemver seq) =
        this.CrackRepo
        |> Seq.choose(function
            | CrackedProject.FSharp proj -> Some proj
            | _ -> None)
        |> Seq.iter(function
            | { GitNetOptions = { Scope = None } }
            | { AssemblyFile = None } ->
                ()
            | { ProjectDirectory = path
                AssemblyFile = Some assemblyFile
                GitNetOptions = { Scope = Some scop } } ->
                let assemblyPath = Path.Combine(this.rootDir, path, assemblyFile)
                match
                    versions
                    |> Seq.tryFind(
                        _.Sepoch.GetScope
                        >> ValueOption.exists((=) scop)
                        )
                with
                | Some sepochSemver ->
                    try
                    let attrs = AssemblyInfoFile.getAttributes assemblyPath
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
                    with e ->
                        e
                        |> printfn "%A"
                | _ -> ()
             
            )
    member this.WriteAssemblyFiles (mapping: IDictionary<string, SepochSemver>) =
        mapping.Values
        |> this.WriteAssemblyFiles
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
