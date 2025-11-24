[<AutoOpen>]
module rec Partas.GitNet.Types

open Partas.Tools.SepochSemver
open Partas.ConventionalCommits
open LibGit2Sharp.FSharp

/// Short hand access to LibGit2Sharp types
[<AutoOpen>]
module Aliases =
    type DiffTargets = LibGit2Sharp.DiffTargets
    type ExplicitPathsOptions = LibGit2Sharp.ExplicitPathsOptions
    type TreeChanges = LibGit2Sharp.TreeChanges
    type Commit = LibGit2Sharp.Commit
    type Tag = LibGit2Sharp.Tag
    type GitObject = LibGit2Sharp.GitObject

/// <summary>
/// Indicates the value of a <i>Bump</i>.
/// </summary>
[<RequireQualifiedAccess>]
type BumpType =
    | Patch
    | Minor
    | Major
    /// Includes the epoch value
    | Epoch of string

/// Record of details relating to a FSharp project.
type FSharpCrackedProject = {
    /// The project directory path from the repo root.
    ProjectDirectory: string
    /// The project file path (.fsproj extension) relative to repo root.
    ProjectFileName: string
    /// Compiled files in the project. Currently un-utilised
    SourceFiles: string list
    /// AssemblyFile for the project if present relative to the project directory path.
    AssemblyFile: string option
    /// GitNet configuration properties in the project.
    GitNetOptions: CrackedProject.GitNetOptions
}

/// Record of details for a non FSharp project (or where a project is, and its name).
type NonFSCrackedProject = {
    /// The directory used to filter commits.
    ProjectDirectory: string
    /// The scope name for the commits.
    Scope: string
}

[<RequireQualifiedAccess>]
type CrackedProject =
    | FSharp of FSharpCrackedProject
    | NonFSharp of NonFSCrackedProject

module CrackedProject =
    /// Properties that can be set in a .fsproj
    type GitNetOptions = {
        /// GitNetInitialVersion
        InitialVersion: Semver.SemVersion option
        /// GitNetAutoBump
        AutoBump: bool
        /// GitNetScope
        Scope: string option
        /// GitNetEpoch
        Epoch: string option
        /// GitNetAutoBumpBranchName
        AutoBumpBranchName: string option
        /// <summary>
        /// Version of the project file.
        /// </summary>
        /// <remarks>
        /// This is not representative of a custom <c>GitNet</c> property like the other fields.
        /// Instead, it serves to provide the initial version of the project file to consumers to use
        /// where relevant.<br/>
        /// It doesn't serve any purpose in default functions of GitNet.
        /// </remarks>
        Version: Semver.SemVersion option
    }
    let getFSharp = function
        | CrackedProject.FSharp proj -> ValueSome proj
        | _ -> ValueNone
    let getNonFSharp = function
        | CrackedProject.NonFSharp proj -> ValueSome proj
        | _ -> ValueNone
    module FSharp =
        let projectDirectory: FSharpCrackedProject -> _ = _.ProjectDirectory
        let projectFileName = _.ProjectFileName
        let sourceFiles = _.SourceFiles
        let assemblyFile = _.AssemblyFile >> ValueOption.ofOption
        let gitNetOptions = _.GitNetOptions
    module NonFSharp =
        let projectDirectory = _.ProjectDirectory
        let scope: NonFSCrackedProject -> _ = _.Scope

/// A record of the parsed commit, and the git object it was
/// derived from.
type GitNetCommit = {
    ParsedCommit: ParsedCommit
    Ref: Commit
}

/// A DU of the parsed tag, and the git object it was derived from.
type GitNetTag =
    /// A SepochSemver tag
    | GitNetTag of GitNetTag.Sepoch
    /// A Semver tag
    | SemVerTag of GitNetTag.Semver
    /// Non-Semver compatible tag
    | GitTag of Tag

/// <summary>
/// CommitGroup. You can define specific settings for a commit group,
/// such as the name, the position, the heading level used, a preamble,
/// a postamble, whether to only count the number of commits in the group.<br/><br/>
/// At least the title must be defined.
/// </summary>
/// <remarks>
/// All defaults are None/false; <c>HeadingLevel</c> default is <c>4</c>.
/// </remarks>
[<AbstractClass>]
type CommitGroup() =
    abstract Title: string
    abstract Position: int option
    abstract HeadingLevel: int
    abstract Prelude: string option
    abstract Postfix: string option
    abstract CountOnly: bool
    
    default this.Position = None
    default this.HeadingLevel = 4
    default this.Prelude = None
    default this.Postfix = None
    default this.CountOnly = false
    
module CommitGroup =
    let createSimple text = { new CommitGroup() with member this.Title = text }
    module Defaults =
        let private makeDefault title position = {
            new CommitGroup() with
                member this.Title = title
                member this.Position = Some position
        }
        let fix = makeDefault "Fixed" 2
        let feat = makeDefault "Added" 1
        let breaking = makeDefault "BREAKING CHANGE" 0
        let removed = makeDefault "Removed" 3
        let deprecated = makeDefault "Deprecated" 4
        let revert = makeDefault "Revert" 5
        let changed = makeDefault "Changed" 2
        let other = makeDefault "Others" 6
        
/// Single case DU for GitHub LibGit2Sharp remotes.
[<Struct>]
type GitHubRemote = GitHubRemote of LibGit2Sharp.Remote with
    member this.Value = let (GitHubRemote value) = this in value

module GitNetTag =
    type Sepoch = {
        Ref: Tag
        SepochSemver: SepochSemver
    }
    type Semver = {
        Ref: Tag
        Semver: Semver.SemVersion
    }
    let create tag =
        let createImpl =
            Tag.name
            >> parseSepochSemver
            >> function
                | { Sepoch = Sepoch.None } as sepSemver ->
                    { Ref = tag
                      Semver = sepSemver.SemVer }
                    |> SemVerTag
                | sepochSemver ->
                    { Ref = tag
                      SepochSemver = sepochSemver }
                    |> GitNetTag
        try
            createImpl tag
        with
        _ -> GitTag tag
    module Validation =
        let versioned = function
            | GitNetTag _ | SemVerTag _ as tag -> Some tag
            | _ -> None
        let sepochSemver = function
            | GitNetTag _ as tag -> Some tag
            | _ -> None
    let getScope = function
        | GitNetTag { SepochSemver = { Sepoch = sepoch } } ->
            sepoch.GetScope
        | _ -> ValueNone
    let chooseSemverCompatible = function
        | GitNetTag _ | SemVerTag _ as tag -> ValueSome tag
        | _ -> ValueNone
    let toSemverString = function
        | SemVerTag { Semver = semver }
        | GitNetTag { SepochSemver = { SemVer = semver } } ->
            semver.ToString()
        | _ -> failwith "Not a valid semver tag"
    let tryToSemverString tag =
        try toSemverString tag |> ValueSome
        with _ -> ValueNone
    let toSepochSemverString = function
        | SemVerTag { Semver = semver } -> semver.ToString()
        | GitNetTag { SepochSemver = sepoch } -> sepoch.ToString()
        | _ -> failwith "Not a valid semver tag"
    let tryToSepochSemverString tag =
        try toSepochSemverString tag |> ValueSome
        with _ -> ValueNone
    let toString = function
        | SemVerTag { Semver = semver } -> semver.ToString()
        | GitNetTag { SepochSemver = sepoch } -> sepoch.ToString()
        | GitTag tag -> Tag.name tag
    
    module Git =
        let tag = function
            | GitNetTag { Ref = tag }
            | SemVerTag { Ref = tag }
            | GitTag tag -> tag
        let name = tag >> Tag.name
        let fullName = tag >> Tag.fullName
        let sha = tag >> Tag.target >> GitObject.sha
        let commit = tag >> Tag.target >> GitObject.peel<Commit>
        let author = commit >> Commit.author
        let committer = commit >> Commit.committer
        module Author =
            let name = author >> Signature.name
            let email = author >> Signature.email
            let date = author >> Signature.date
        module Committer =
            let name = committer >> Signature.name
            let email = committer >> Signature.email
            let date = committer >> Signature.date
    let git = Git.tag
module GitNetCommit =
    let create parser commit =
        {
            ParsedCommit = commit |> Commit.message |> parser
            Ref = commit
        }
    let git = _.Ref
    let parsed = _.ParsedCommit
    let conventional = parsed >> function
        Conventional _ | Breaking _ as commit -> Some (commit :> IConventionalCommit)
        | _ -> None
    let sha = git >> Commit.sha
    let author = git >> Commit.author
    let committer = git >> Commit.committer
    let subject commit =
        let conventionalSubject =
            conventional >> Option.map _.Subject
        commit |> conventionalSubject
        |> Option.defaultValue (commit |> git |> Commit.subject)
    let tryType = conventional >> Option.map _.Type
    let tryFooters = conventional >> Option.map _.Footers
    let tryFooter key = conventional >> Option.map _.TryGetFooterValue(key)
         
    module Author =
        let name = author >> Signature.name
        let email = author >> Signature.email
        let date = author >> Signature.date
    module Committer =
        let name = committer >> Signature.name
        let email = committer >> Signature.email
        let date = committer >> Signature.date

    module Sha =
        let abbrev count = sha >> _.Substring(0, count)
        let githubAbbrev = abbrev 7
        let humanFriendly = abbrev 5
 
