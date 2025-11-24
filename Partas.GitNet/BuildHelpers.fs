[<AutoOpen>]
module Partas.GitNet.BuildHelpers

// During the build process, we are abstracting away version control.
// However, we must keep in mind that a mono repo where packages reference
// each other require knowledge of the other projects versions for their
// nuget referencing.
// For this reason, we need build helpers which will version the project
// files directly for the build process. These are not necessary to commit.

open System.Collections.Generic
open System.Xml.Linq
open Fake.IO
open Fake.DotNet
open System.Xml.XPath
open LibGit2Sharp.FSharp
open Partas.GitNet.RepoCracker
open Partas.Tools.SepochSemver

module CrackedProject =
    /// <summary>
    /// <para>WARNING:</para>
    /// <para>This action overwrites the file with any changes made during <c>fn</c> if it returns Ok</para>
    /// </summary>
    /// <param name="fn"></param>
    /// <param name="proj"></param>
    let withFsProj (fn: XDocument -> Result<unit, 'E>) (proj: CrackedProject) =
        let absProjectPath = Path.combine proj.RepoRoot proj.ProjectFileName
        let proj = MSBuild.loadProject absProjectPath
        fn proj.Document
        |> Result.map(fun () -> proj.Save(absProjectPath))
    /// Retrieves all compiled file paths
    let getCompiledFilePaths (proj: CrackedProject) =
        proj
        |> withFsProj(
            _.XPathSelectElements("//Compile[@Include]")
            >> Seq.map (_.XPathEvaluate("string(@Include)") >> unbox<string>)
            >> Error
          )
        |> function
            | Error values ->
                values |> Seq.toList
            | _ -> [] // unreachable
    /// Retrieves all Content file paths
    let getContentFilePaths (proj: CrackedProject) =
        proj
        |> withFsProj(
            _.XPathSelectElements("//Content[@Include]")
            >> Seq.map (_.XPathEvaluate("string(@Include)") >> unbox<string>)
            >> Error
          )
        |> function
            | Error values ->
                values |> Seq.toList
            | _ -> [] // unreachable
    /// Retrieves all ItemGroup 'None' file paths
    let getNoneFilePaths (proj: CrackedProject) =
        proj
        |> withFsProj(
            _.XPathSelectElements("//ItemGroup/None[@Include]")
            >> Seq.map (_.XPathEvaluate("string(@Include)") >> unbox<string>)
            >> Error
          )
        |> function
            | Error values ->
                values |> Seq.toList
            | _ -> [] // unreachable
    let inline private stageFiles (runtime: GitNetRuntime) (files: string list) =
        try
        let index = runtime.repo |> Repository.index
        for file in files do
            index |> Index.addFile file
        Ok ()
        with e ->
            Error e
    /// Stages all compiled files
    let stageCompiledFiles (runtime: GitNetRuntime) (proj: CrackedProject) =
        getCompiledFilePaths proj
        |> List.map (Path.combine proj.ProjectDirectory)
        |> stageFiles runtime
        |> Result.map(fun _ -> proj)
    /// Stages the project file (.fsproj)
    let stageProjectFile (runtime: GitNetRuntime) (proj: CrackedProject) =
        proj.ProjectFileName
        |> Path.combine proj.ProjectDirectory
        |> List.singleton
        |> stageFiles runtime
        |> Result.map(fun _ -> proj)
    /// Stages all Content files
    let stageContentFiles runtime proj =
        getContentFilePaths proj
        |> List.map (Path.combine proj.ProjectDirectory)
        |> stageFiles runtime
        |> Result.map(fun _ -> proj)
    /// Stages all 'None' files
    let stageNoneFiles runtime proj =
        getNoneFilePaths proj
        |> List.map (Path.combine proj.ProjectDirectory)
        |> stageFiles runtime
        |> Result.map(fun _ -> proj)
    /// Stages the project file, the compiled files, and the content files (does not stage None files)
    let stageAllFiles runtime =
        stageProjectFile runtime
        >> Result.bind (stageCompiledFiles runtime)
        >> Result.bind (stageContentFiles runtime)
    module Document =
        let withProject (fn: XElement -> unit) (document: XDocument) =
            match
                document.XPathSelectElement("//Project[@Sdk]")
                |> ValueOption.ofObj
            with
            | ValueSome element ->
                element |> fn
                document
            | ValueNone -> failwith "Cannot modify an XML file without a Project Element."
        let withPropertyGroup (fn: XElement -> unit) (document: XDocument) =
            match
                document.XPathSelectElement("//PropertyGroup")
                |> ValueOption.ofObj
            with
            | ValueSome element ->
                element |> fn
                document
            | ValueNone ->
                let element = XElement("PropertyGroup") |> fn
                document
                |> withProject(_.AddFirst(element))
        /// Will perform an action on an XElement, or first create it as a property
        /// in the PropertyGroup, and then perform the action
        let withProperty (propertyGroupElementName: string) (fn: XElement -> unit) (document: XDocument) =
            match
                document.XPathSelectElement("//PropertyGroup/" + propertyGroupElementName)
                |> ValueOption.ofObj
            with
            | ValueSome element ->
                fn element
                document
            | ValueNone ->
                document
                |> withPropertyGroup _.Add(XElement(propertyGroupElementName) |> fn)
            
        let withVersion (fn: XElement -> unit) (document: XDocument) = withProperty "Version" fn document
        let withPackageVersion (fn: XElement -> unit) (document: XDocument) = withProperty "PackageVersion" fn document
        let withNpmDependencies(fn: XElement -> unit) (document: XDocument) = withProperty "NpmDependencies" fn document
        let withPackageReleaseNotes(fn: XElement -> unit) (document: XDocument) = withProperty "PackageReleaseNotes" fn document
        let withNpmPackage (npmPackage: string) (fn: XElement -> unit) (document: XDocument) =
            document
            |> withNpmDependencies(fun npmDependencies ->
                match
                    npmDependencies.XPathSelectElement($"//NpmPackage[@Name={npmPackage}]")
                    |> ValueOption.ofObj
                with
                | ValueSome element -> fn element
                | ValueNone ->
                    let packageNode =
                        XElement("NpmPackage")
                    packageNode.Add(XAttribute("Name", npmPackage))
                    npmDependencies.Add(packageNode |> fn)
                )

module XElement =
    let withValue value (element: XElement) =
        element.Value <- value
    
let private versionProject (versionString: string) proj =
    let absProjectPath = Path.combine proj.RepoRoot proj.ProjectFileName
    let originalContent = File.readAsString absProjectPath
    let revert = fun () -> File.writeString false absProjectPath originalContent
    let addComment: XElement -> XElement =
        let comment = XComment("Package & Version are AutoGenerated. Do not modify outside of your GitNet CI, or if you no longer plan on using GitNet.")
        fun el -> el.AddBeforeSelf(comment); el
    proj
    |> CrackedProject.withFsProj (fun doc ->
        try
        doc
        |> CrackedProject.Document.withVersion (addComment >> XElement.withValue versionString)
        |> CrackedProject.Document.withPackageVersion (addComment >> XElement.withValue versionString)
        |> ignore
        Ok()
        with e ->
            Error e
        )
    |> Result.map(fun _ -> revert)

let private getProjectsForMapping (mapping: IDictionary<string, SepochSemver>) (runtime: GitNetRuntime) =
        runtime.CrackRepo
        |> Seq.choose(function
            | ({ GitNetOptions = { Scope = Some scope } } as proj) when mapping.ContainsKey(scope) ->
                Some proj
            | _ -> None
            )
        |> Seq.map(fun proj ->
                let semver =
                    mapping.Item(proj.GitNetOptions.Scope.Value)
                proj,semver)

type GitNetRuntime with
    member this.VersionProject(project: CrackedProject, version: Semver.SemVersion, ?stageFile: bool, ?cacheDisposal: bool) =
        let projectPath = Path.combine this.rootDir project.ProjectFileName
        let stageFile = defaultArg stageFile false
        let cacheDisposal = defaultArg cacheDisposal true
        versionProject (version.ToString()) project
        |> function
            | Ok dispose when cacheDisposal -> this.Disposals.Add dispose
            | Error e -> raise e
            | _ -> ()
        if stageFile then
            match CrackedProject.stageAllFiles this project with
            | Ok _ -> this.StatVersionFile projectPath
            | Error e -> e |> printfn "Filed to stage %s:\n%A" projectPath
        else this.StatVersionFile projectPath
    /// <summary>
    /// Uses the bump dictionary
    /// to write the versions to the
    /// files to prepare them for packing.
    /// </summary>
    member this.VersionProjects(mapping: IDictionary<string, SepochSemver>, ?stageFile: bool, ?cacheDisposal: bool) =
        this
        |> getProjectsForMapping mapping
        |> Seq.iter(
            fun (proj, semver) ->
                this.VersionProject(proj, semver.SemVer, ?cacheDisposal = cacheDisposal, ?stageFile = stageFile)
            )
    member this.RevertVersionProjects() =
            let runDisposal func =
                try func() with e ->
                e |> printfn "Error while disposing of cached disposals:\n%A"
            this.Disposals |> Seq.iter runDisposal
