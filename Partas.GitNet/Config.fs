[<AutoOpen>]
module Partas.GitNet.ConfigTypes

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open Fake.Core
open LibGit2Sharp.FSharp
open Partas.ConventionalCommits

[<RequireQualifiedAccess>]
type FileSplittingStrategy =
    /// <summary>
    /// All release notes in one file
    /// </summary>
    | Monolith
    /// <summary>
    /// One release notes file per scope
    /// </summary>
    | PerScope
    /// <summary>
    /// One release notes per epoch
    /// </summary>
    | PerEpochMonolith
    /// <summary>
    /// One release notes per scope, per epoch
    /// </summary>
    | PerEpochPerScope
[<RequireQualifiedAccess>]
type ScopePrefixConfig =
    /// <summary>
    /// A function to make your own prefix. The function is provided
    /// the scope name (or not if unscoped) and should return a prefix
    /// (or not)
    /// </summary>
    | Templated of FSharpFunc<string option,string option>
    /// <summary>
    /// Prefix is the scope delimited by <c>[]</c>
    /// </summary>
    | SquareBrackets
    /// <summary>
    /// Prefix is the scope delimited by <c>()</c>
    /// </summary>
    | Parenthesis
    /// <summary>
    /// Prefix is the scope delimited by <c>&lt;&gt;</c>
    /// </summary>
    | AngleBrackets
    /// <summary>
    /// Only for GitHub. Only if your repo has the required labels.
    /// </summary>
    | Label
    /// <summary>
    /// No prefix.
    /// </summary>
    | None

[<RequireQualifiedAccess>]
type MacroGroupType =
    /// <summary>
    /// The release notes are grouped primarily by scopes.
    /// </summary>
    | Scoped
    /// <summary>
    /// The release notes are grouped by tag, with each commit
    /// having a scope prefix
    /// </summary>
    | ScopePrefix of ScopePrefixConfig
    /// <summary>
    /// The release notes are grouped by Epoch, with an option for
    /// subgrouping further by one of the above
    /// </summary>
    | Epoch
    | EpochAnd of MacroGroupType
[<RequireQualifiedAccess>]
type OutputPathType =
    /// <summary>
    /// A path to the output file. If multi-file is done, then
    /// the directory and file name are used as prefixes of the others.
    /// Alternatively use PerScope with multifiling
    /// </summary>
    | Simple of string
    /// <summary>
    /// Allows you to set the path per file. This will ignore monolith
    /// file settings.
    /// </summary>
    | PerScope of (string -> string)
    /// <summary>
    /// Writes to a "RELEASE_NOTES.md" file in the root.
    /// </summary>
    | Default
[<RequireQualifiedAccess>]
type FSharpNameResolution =
    /// <summary>
    /// The file name is used to derive the scope name
    /// </summary>
    | FileName
    /// <summary>
    /// The title is used to derive the scope name.
    /// Projects without a title will be ignored.
    /// </summary>
    | Title
    /// <summary>
    /// The directory of a project is used to derive the scope name.
    /// </summary>
    | Directory
    /// <summary>
    /// Title > FileName
    /// </summary>
    | Auto
[<RequireQualifiedAccess>]
type ProjectInitialVersionStrategy =
    /// <summary>
    /// All receive same initial version
    /// </summary>
    | Simple of string
    /// <summary>
    /// No initial versions
    /// </summary>
    | None
    /// <summary>
    /// projects are mapped to an initial version
    /// </summary>
    | Mapping of IDictionary<string, string>
    /// <summary>
    /// Like mapping, but any that are not explicitly set in the mapping
    /// use the fallback like Simple.
    /// </summary>
    | MappingOrSimple of mapping: IDictionary<string, string> * fallback: string
type ProjectFSharpConfig = {
    /// <summary>
    /// Ignored projects (by their file name without their extensions).
    /// </summary>
    IgnoredProjects: string list
    /// <summary>
    /// Applies a function to a resolved name (see <c>NameResolution</c>
    /// for where the name comes from) to produce the scope, or ignore it.
    /// </summary>
    AutoScoping: string -> string option
    /// <summary>
    /// Projects can explicitly set their scope using a property. This
    /// allows your code to overwrite this where encountered.
    /// </summary>
    OverrideExplicitScopes: bool
    /// <summary>
    /// The strategy used to resolve the name of the project.
    /// </summary>
    /// <remarks>
    /// There are multiple sources for a project name.<br/>
    /// There is the property title in the project, the file name,
    /// and the project directory.<br/>
    /// Depending on the name resolution strategy, you can manipulate
    /// this source (before it is fed to the autoscoper if relevant).
    /// </remarks>
    NameResolution: FSharpNameResolution
}

/// <summary>
/// Configuration for repos that are not FSharp related.
/// </summary>
/// <remarks>
/// <c>PathScopeMapping</c> is a dictionary type property
/// that contains the mapping of directories to scopes for non
/// FSharp/dotnet projects.
/// </remarks>
type ProjectNoneConfig = {
    /// <summary>
    /// Mapping of directories relative to the git root
    /// to scopes.
    /// </summary>
    PathScopeMapping: IDictionary<string, string>
}
[<RequireQualifiedAccess>]
type ProjectType =
    /// <summary>
    /// Extra capabilities are employed to automanage scopes etc.
    /// </summary>
    | FSharp of ProjectFSharpConfig option
    /// <summary>
    /// No fsproj detection, scopes must be defined, etc.
    /// </summary>
    | None of ProjectNoneConfig

/// <summary>
/// Commit ignore strategies. See Remarks for overview.
/// </summary>
/// <remarks>
/// <list type="table">
///     <listheader>
///         <term>Strategy</term>
///         <description>Description</description>
///     </listheader>
///     <item>
///         <term>Author</term>
///         <description>Ignores commits based on the <c>Author</c> which is distinct from the <c>Committer</c></description>
///     </item>
///     <item>
///         <term>Email</term>
///         <description>Ignores commits based on the <c>Author</c> email.</description>
///     </item>
///     <item>
///         <term>Type</term>
///         <description>Ignores commits based on the <c>Type</c> if they are conventional commits</description>
///     </item>
///     <item>
///         <term>FooterKey</term>
///         <description>Ignores commits based on the presence of a <c>FooterKey</c> if they are conventional commits</description>
///     </item>
///     <item>
///         <term>FooterKeyValue</term>
///         <description>Ignores commits based on whether a particular <c>Footer</c> key value pair is present (if they are conventional commits).</description>
///     </item>
///     <item>
///         <term>Sha</term>
///         <description>Ignores commits based on their <c>Sha</c>. It only checks the abbreviated forms as is provided by most tools, and will shorten full <c>Sha</c>s if required.</description>
///     </item>
///     <item>
///         <term>Tag</term>
///         <description>Ignore all the commits of a particular <c>Tag</c></description>
///     </item>
///     <item>
///         <term>TimeRange</term>
///         <description>Ignore commits that fall within a specific time period.</description>
///     </item>
///     <item>
///         <term>CommitScope</term>
///         <description>Ignore commits that have a specified conventional commit scope. (eg: <c>fix(ci): summary</c> has a scope of <c>ci</c>)</description>
///     </item>
///     <item>
///         <term>SubjectRegex</term>
///         <description>Ignore commits based on their subject (or the first line) that matches the given regex.</description>
///     </item>
///     <item>
///         <term>SkipCi</term>
///         <description>Ignore commits that start with <c>[skip ci]</c></description>
///     </item>
/// </list>
/// </remarks>
[<RequireQualifiedAccess>]
type IgnoreCommit =
    /// <summary>
    /// Ignores commits based on the <c>Author</c> name.
    /// </summary>
    /// <remarks>
    /// The committer is distinguished from the author.
    /// </remarks>
    | Author of author: string
    /// <summary>
    /// Ignores commits based on the <c>Author</c> email.
    /// </summary>
    | Email of email: string
    /// <summary>
    /// Ignores commits based on the <c>Type</c> (if they are conventional commits).
    /// </summary>
    | Type of ``type``: string
    /// <summary>
    /// Ignores commits based on a <c>FooterKey</c> (if they are conventional commits).
    /// </summary>
    | FooterKey of footerToken: string
    /// <summary>
    /// Ignores commits based on a <c>FooterKey</c> and <c>FooterValue</c> combination
    /// (if they are conventional commits).
    /// </summary>
    | FooterKeyValue of footerToken: string * footerValue: string
    /// <summary>
    /// Ignores commits based on their <c>Sha</c>.
    /// </summary>
    /// <remarks>
    /// We only check that the beginning of the actual <c>Sha</c> matches the
    /// given <c>Sha</c>. This means you can use abbreviated Shas at your own
    /// discretion.
    /// </remarks>
    | Sha of sha: string
    /// <summary>
    /// Ignore all commits related to a tag.
    /// </summary>
    | Tag of tag: string
    /// <summary>
    /// Ignore all commits that were made within a
    /// specified time period.
    /// </summary>
    | TimeRange of fromDate: DateTimeOffset * timeSpan: TimeSpan
    /// <summary>
    /// Ignore all commits that have a specified conventional commit scope.
    /// </summary>
    | CommitScope of scope: string
    /// Ignore all commits that have conventional commit subject,
    /// or a first line that matches the given regex.
    | SubjectRegex of regex: Regex
    /// Ignore all commits that start with [skip ci].
    | SkipCi

/// <summary>
/// Configuration of commit related options.
/// </summary>
type CommitConfig = {
    /// <summary>
    /// A list of ignore strategies that is applied in collection.
    /// </summary>
    /// <remarks>
    /// Still too early in development to measure impact of these strategies,
    /// but we can assume they can become significant.<br/><br/>
    /// </remarks>
    Ignore: IgnoreCommit list
}
[<RequireQualifiedAccess>]
type ForceBumpStrategy =
    /// <summary>
    /// When force bumping, you can apply the bump to specified scopes.
    /// </summary>
    | ForScopes of string list
    /// <summary>
    /// Force bump is applied to all scopes.
    /// </summary>
    | All
    /// <summary>
    /// For specific scope if it is the CWD, else all.
    /// </summary>
    | Auto
    /// <summary>
    /// No bump
    /// </summary>
    | None
[<RequireQualifiedAccess>]
type ForceBumpValue =
    /// <summary>
    /// Force bump value is determined by commits
    /// </summary>
    | Auto
    /// <summary>
    /// Force bump by the type
    /// </summary>
    | BumpType of BumpType
    /// <summary>
    /// Force bump to target
    /// </summary>
    | Target of string
[<RequireQualifiedAccess>]
type GenerateReleaseStrategy =
    /// <summary>
    /// Release notes are generated for each scope 
    /// </summary>
    | PerScope
    /// <summary>
    /// Release notes are only generated for specific scopes
    /// </summary>
    | ForTargets of string list
    /// <summary>
    /// Release notes are generated for all valid scopes together
    /// </summary>
    | Monolith
    /// <summary>
    /// No release notes are generated
    /// </summary>
    | None
[<RequireQualifiedAccess>]
type GenerateRelease =
    /// <summary>
    /// Will pipe out release notes for any release above minor.
    /// </summary>
    | MinMinor
    /// <summary>
    /// Will pipe out release notes for any release above major.
    /// </summary>
    | MinMajor
    /// <summary>
    /// Will pipe out release notes for any release above epoch.
    /// </summary>
    | MinEpoch
    /// <summary>
    /// Will pipe out release notes for any release above patch.
    /// </summary>
    /// <remarks>Same as <c>Any</c></remarks>
    | MinPatch
    /// <summary>
    /// Will pipe out release notes for any release.
    /// </summary>
    | Any
    /// <summary>
    /// Will never pipe out release notes.
    /// </summary>
    | None
/// <summary>
/// Management strategy for <c>AssemblyInfo</c>/<c>AssemblyFile</c> <c>.fs</c>
/// files.
/// </summary>
[<RequireQualifiedAccess>]
type AssemblyFileManagement =
    /// <summary>
    /// Will update assembly file/info files if they exist
    /// </summary>
    | UpdateIfExists
    /// <summary>
    /// Will create and update assembly file/info files
    /// </summary>
    | Create
    /// <summary>
    /// Ignores file/info files
    /// </summary>
    | None
[<RequireQualifiedAccess>]
type ScopeStrategy =
    /// <summary>
    /// Normal commit logging
    /// </summary>
    | None
    /// <summary>
    /// Explicit scopes are logged only
    /// </summary>
    | Explicit
    /// <summary>
    /// Explicit scopes are logged, and scope
    /// names are generated when not explicit, and logged
    /// under that scope.
    /// </summary>
    | Auto

/// <summary>
/// Strategies for determining whether a commit will result in a specified
/// bump type.
/// </summary>
[<RequireQualifiedAccess>]
type BumpMatcher =
    /// ConventionalCommit types
    | Type of string
    /// Regex string applied to the subject.
    | DescriptionRegex of string
    /// Whether a specific footer is present
    | FooterKey of string
    /// Same as FooterKey at the moment.
    | FooterKeyValue of string * string
    /// Match against a type, and then apply a regex to the subject as an OR op
    | TypeDescription of typ: string * descriptionRegex: string
    /// Match against a type, and then the footer key as an OR op
    | TypeFooterKey of typ: string * footerKey: string
    /// Match against a type, and then the footer as an OR op (value ignored at the moment).
    | TypeFooterValue of typ: string * footerKey: string * footerValue: string

/// <summary>
/// Epoch matcher can only be used to match a footer key. The value of the
/// footer is then used as the new epoch name. Remember that casing is not
/// used in matching footers as per the conventional commit spec.
/// </summary>
type EpochMatcher = EpochMatcher of footerKey: string with
    member this.Value = let (EpochMatcher value) = this in value

/// <summary>
/// A mapping of strategies to the bump types (Epoch, Major, Minor, Patch).
/// </summary>
type CommitBumpTypeMapping = {
    Epoch: EpochMatcher list
    Major: BumpMatcher list
    Minor: BumpMatcher list
    Patch: BumpMatcher list
}

type CommitBumpTypeMapping with
    /// <summary>
    /// Computes a function from the mapping record which can be used
    /// on parsed commits. Returns None if the commit does not indicate
    /// any bump level.
    /// </summary>
    member this.ComputeBumpMatcher: ParsedCommit -> BumpType option =
        let tryConventional: ParsedCommit -> ConventionalCommit voption = _.TryToConventionalCommit
        let matchType s =
                fun (commit: ParsedCommit) ->
                    commit |> tryConventional
                    |> ValueOption.map (_.Type >> (=) s)
                    |> ValueOption.defaultValue false
        let matchDesc reg =
                fun commit ->
                    tryConventional commit
                    |> ValueOption.map
                           (_.Subject
                         >> Regex(reg).IsMatch)
                    |> ValueOption.defaultValue false
        let matchFooter key =
                fun (commit: ParsedCommit) ->
                    commit.TryGetFooterValue(key)
                    |> _.IsSome
        let makeMatcher bumpMatcher: ParsedCommit -> bool = bumpMatcher |> function
            | BumpMatcher.Type s ->
                matchType s
            | BumpMatcher.DescriptionRegex reg ->
                matchDesc reg
            | BumpMatcher.FooterKeyValue(key,_)
            | BumpMatcher.FooterKey key ->
                matchFooter key
            | BumpMatcher.TypeDescription(typ,desc) ->
                fun commit ->
                    matchType typ commit || matchDesc desc commit
            | BumpMatcher.TypeFooterKey(typ,key) ->
                fun commit ->
                    matchType typ commit || matchFooter key commit
            | BumpMatcher.TypeFooterValue(typ,key,_) ->
                fun commit ->
                    matchType typ commit || matchFooter key commit
        let majorMatchers inp =
            this.Major |> List.map makeMatcher
            |> List.tryFind (fun func -> func inp)
            |> Option.isSome
            |> function true -> Some BumpType.Major | _ -> None
        let minorMatchers inp =
            this.Minor |> List.map makeMatcher
            |> List.tryFind (fun func -> func inp)
            |> Option.isSome
            |> function true -> Some BumpType.Minor | _ -> None
        let patchMatchers inp =
            this.Patch |> List.map makeMatcher
            |> List.tryFind (fun func -> func inp)
            |> Option.isSome
            |> function true -> Some BumpType.Patch | _ -> None
        // todo epoch
        fun commit ->
            majorMatchers commit
            |> Option.orElse (minorMatchers commit)
            |> Option.orElse (patchMatchers commit)

/// Single case DU of a commit group with the list of matching strategies.
type GroupMatcher = GroupMatcher of CommitGroup * BumpMatcher list with
    member inline this.Deconstruct = let (GroupMatcher(group,matchers)) = this in (group,matchers)

/// <summary>
/// Configuration for the changelog/releasenotes output.
/// </summary>
type OutputConfig = {
    /// <summary>
    /// The file writing/creation strategy (whether to compile all
    /// the scopes into one file or split etc).
    /// </summary>
    FileSplitting: FileSplittingStrategy
    /// <summary>
    /// The grouping strategy (whether to group by their scopes, or
    /// epochs, first).
    /// </summary>
    MacroGrouping: MacroGroupType
    /// <summary>
    /// The output path.
    /// </summary>
    Path: OutputPathType
    // TODO
    /// <summary>
    /// Null op atm.
    /// </summary>
    Formatting: MacroGroupType
    /// <summary>
    /// The strategies that map commits to groups.
    /// </summary>
    GroupMatcher: GroupMatcher list
    /// <summary>
    /// A list of strategies applied to commits to determine
    /// if they should be ignored.
    /// </summary>
    Ignore: IgnoreCommit list
    /// <summary>
    /// Whether to allow unconventional commits.
    /// </summary>
    AllowUnconventional: bool
    /// <summary>
    /// The default commit group for unmatched commits.
    /// </summary>
    DefaultUnmatchedGroup: CommitGroup
    /// <summary>
    /// Whether to allow unmatched commits.
    /// </summary>
    AllowUnmatched: bool
}

let private parseToCommit<'T when 'T :> ICommit> = fun (parsed: 'T) -> parsed :> ICommit
type OutputConfig with
    /// <summary>
    /// Computes a single function from the group matching list that
    /// will group a commit, or return none if unmatched by all strategies.
    /// </summary>
    member this.ComputeGroupMatcher =
        let tryConventional: ParsedCommit -> ConventionalCommit voption = _.TryToConventionalCommit
        let tryOnConv map func =
            tryConventional
            >> ValueOption.map(map >> func)
            >> ValueOption.defaultValue false
        let matchType = fun group typ (commit: ParsedCommit) ->
            if commit |> tryOnConv _.Type ((=) typ) then
                Some group
            else None
        let matchDescriptionRegex = fun group reg commit ->
            if commit |> tryOnConv _.Subject (Regex(reg).IsMatch) then
                Some group : CommitGroup option
            else None
        let matchFooterKey = fun group key commit ->
            if commit |> tryOnConv id _.ContainsFooter(key) then
                Some group
            else None
        let matchFooterKeyValue = fun group key value commit ->
            let footer =
                commit
                |> tryConventional
                |> ValueOption.toOption
                |> Option.bind _.TryGetFooterValue(key)
            if
                footer
                |> Option.exists ((=) value)
            then Some group
            else None
        let funcs: FSharpFunc<ParsedCommit,CommitGroup Option> list = [
            for group,matchers in this.GroupMatcher |> List.map _.Deconstruct do
                for matcher in matchers do
                    match matcher with
                    | BumpMatcher.Type s ->
                        yield matchType group s
                    | BumpMatcher.DescriptionRegex s ->
                        yield matchDescriptionRegex group s
                    | BumpMatcher.FooterKey s ->
                        yield matchFooterKey group s
                    | BumpMatcher.FooterKeyValue(s, value) ->
                        yield matchFooterKeyValue group s value
                    | BumpMatcher.TypeDescription(typ, descriptionRegex) ->
                        yield (
                            fun commit ->
                                commit
                                |> matchType group typ
                                |> Option.orElse (matchDescriptionRegex group descriptionRegex commit))
                    | BumpMatcher.TypeFooterKey(typ, footerKey) ->
                        yield (
                            fun commit ->
                                commit
                                |> matchType group typ
                                |> Option.orElse (matchFooterKey group footerKey commit)
                            )
                    | BumpMatcher.TypeFooterValue(typ, footerKey, footerValue) ->
                        yield (
                            fun commit ->
                                commit
                                |> matchType group typ
                                |> Option.orElse (matchFooterKeyValue group footerKey footerValue commit)
                            )
        ]
        fun commit ->
            funcs
            |> List.tryFind (fun func -> func commit |> _.IsSome)
            |> Option.defaultValue (fun _ -> None)
            <| commit
    member this.ComputeCommitFilter(?tag: GitNetTag) =
        this.Ignore
        |> List.map (function
            | IgnoreCommit.Author author ->
                fun (commit: GitNetCommit) ->
                    commit
                    |> GitNetCommit.Author.name
                    |> (=) author
            | IgnoreCommit.CommitScope scope ->
                fun commit ->
                    commit
                    |> _.ParsedCommit
                    |> _.TryToConventionalCommit
                    |> ValueOption.exists (
                        _.Scope
                        >> ValueOption.exists ((=) scope)
                        )
            | IgnoreCommit.Email email ->
                fun commit ->
                    commit
                    |> GitNetCommit.Author.email
                    |> (=) email
            | IgnoreCommit.FooterKey key ->
                fun commit ->
                    commit.ParsedCommit
                        .TryToConventionalCommit
                    |> ValueOption.map  _.ContainsFooter(key)
                    |> ValueOption.defaultValue false
            | IgnoreCommit.FooterKeyValue(key,value) ->
                fun commit ->
                    commit.ParsedCommit
                        .TryToConventionalCommit
                    |> ValueOption.toOption
                    |> Option.bind 
                        _.TryGetFooterValue(key)
                    |> Option.exists ((=) value)
            | IgnoreCommit.Sha sha ->
                fun commit ->
                    commit |> GitNetCommit.sha
                    |> String.startsWith sha
            | IgnoreCommit.Tag tagName ->
                fun _ ->
                    tag
                    |> Option.exists
                           (GitNetTag.git >> Tag.name >> (=) tagName)
            | IgnoreCommit.TimeRange(start,offset) ->
                let endDate = start + offset
                fun commit ->
                    let actual =
                        commit
                        |> GitNetCommit.Committer.date
                    start <= actual
                    && actual <= endDate
            | IgnoreCommit.Type typ ->
                fun commit ->
                    commit.ParsedCommit
                    |> _.TryToConventionalCommit
                    |> ValueOption.exists(
                        _.Type
                        >> (=) typ
                        )
            | IgnoreCommit.SubjectRegex regex ->
                fun commit ->
                    commit.ParsedCommit
                    |> _.TryToConventionalCommit
                    |> ValueOption.map(
                        _.Subject
                        >> regex.IsMatch
                        )
                    |> ValueOption.defaultValue(
                        commit.Ref.MessageShort
                        |> regex.IsMatch
                        )
            | IgnoreCommit.SkipCi ->
                fun commit ->
                    commit.Ref.MessageShort.ToLower()
                    |> String.startsWith "[skip ci]"
            )
        |> fun matchers ->
            fun commit ->
                List.exists (fun func -> func commit) matchers
                |> not
/// <summary>
/// Bumping related configuration.
/// </summary>
type BumpConfig = {
    /// <summary>
    /// Maps commits to bump levels (epoch, major, minor, patch).
    /// </summary>
    Mapping: CommitBumpTypeMapping
    /// <summary>
    /// The default bump strategy used.
    /// </summary>
    DefaultBumpStrategy: ForceBumpStrategy
    /// <summary>
    /// NO OP -
    /// When to pipe a release note section for GH.
    /// </summary>
    GenerateRelease: GenerateRelease
    /// <summary>
    /// NO OP -
    /// How to provide the release notes.
    /// </summary>
    GenerateReleaseStrategy: GenerateReleaseStrategy
}

/// <summary>
/// Indicates how to determine the Github URL.
/// </summary>
type GithubUrlStrategy =
    /// The repository remotes are explored for a matching github url.
    | Auto
    /// Explicitly set the github url.
    | Manual of string

/// <summary>
/// Github configuration and settings.
/// </summary>
type GithubConfig = {
    /// <summary>
    /// The strategy to determine the github url, or set it explicitly.
    /// </summary>
    Url: GithubUrlStrategy
}

/// <summary>
/// Network configuration, for git services such as GitHub, GitLab, GitBucket
/// etc (only GitHub implemented atm).
/// </summary>
type NetworkConfig = {
    Github: GithubConfig
}

/// <summary>
/// The GitNet configuration container.
/// </summary>
type GitNetConfig = {
    /// Path to the repository. Defaults to the current working directory
    /// if not explicitly set.
    RepositoryPath: string
    /// The scoping strategy.
    Scope: ScopeStrategy
    /// The initial version strategy for scopes/repos that do not have
    /// a release/version tag.
    InitialVersionStrategy: ProjectInitialVersionStrategy
    /// Output configuration
    Output: OutputConfig
    /// The repository project type (F# or not).
    ProjectType: ProjectType
    /// Bump configuration
    Bump: BumpConfig
    /// Assembly file strategy for F# projects.
    AssemblyFiles: AssemblyFileManagement
    /// Network config for GitHub and related.
    Network: NetworkConfig
    /// With FSProjects, will write the versions to the projects for the build process.
    WriteVersionToProjects: bool
}

/// Contains default configuration values and settings used across the application.
/// This module provides default strategies, configurations, and mappings related
/// to various components such as file splitting, output paths, versioning, commit
/// grouping, and release generation.
module Defaults =
    module NonFSharp =
        /// <summary>
        /// Default <c>ProjectType.None</c>
        /// </summary>
        let projectType = ProjectType.None
        /// <summary>
        /// Default <c>{ PathScopeMapping = dict [] }</c>
        /// </summary>
        let projectNoneConfig = {
            PathScopeMapping = dict []
        }
        /// <summary>
        /// Default <c>ScopeStrategy.Auto</c>
        /// </summary>
        let scopeStrategy = ScopeStrategy.Auto
    /// <summary>
    /// Default <c>FileSplittingStrategy.Monolith</c>
    /// </summary>
    let fileSplittingStrategy = FileSplittingStrategy.Monolith
    /// <value><c>ScopePrefixConfig.SquareBrackets</c></value>
    let scopePrefixConfig = ScopePrefixConfig.SquareBrackets
    /// <summary>
    /// Default <c>MacroGroupType.Scoped</c>
    /// </summary>
    let macroGroupType = MacroGroupType.Scoped
    /// <summary>
    /// Default <c>OutputPathType.Simple</c>
    /// </summary>
    let outputPathType = OutputPathType.Simple
    /// <summary>
    /// Default <c>ProjectInitialVersionStrategy.Simple</c>
    /// </summary>
    let projectInitialVersion = ProjectInitialVersionStrategy.Simple "0.1.0"
    module FSharp =
        /// <summary>
        /// Default <c>FSharpNameResolution.Auto</c>
        /// </summary>
        let fsharpNameResolution = FSharpNameResolution.Auto
        /// <summary>
        /// <code>
        /// IgnoredProjects = []
        /// AutoScoping = _.Split('.') >> Seq.last >> Some
        /// OverrideExplicitScopes = false
        /// NameResolution = fsharpNameResolution
        /// </code>
        /// </summary>
        let projectFSharpConfig = {
            IgnoredProjects = []
            AutoScoping = _.Split('.') >> Seq.last >> Some
            OverrideExplicitScopes = false
            NameResolution = fsharpNameResolution
        }
        /// <summary>
        /// Default <c>ProjectType.FSharp</c>
        /// </summary>
        let projectType = ProjectType.FSharp
        /// <summary>
        /// Default <c>AssemblyFileManagement.UpdateIfExists</c>
        /// </summary>
        let assemblyFileManagement = AssemblyFileManagement.UpdateIfExists
        /// <summary>
        /// Default <c>ScopeStrategy.Auto</c>
        /// </summary>
        let scopeStrategy = ScopeStrategy.Auto

    /// <summary>
    /// Default <c>{ Ignore = [] }</c>
    /// </summary>
    let commitConfig = { Ignore = [] }
    /// <summary>
    /// Default <c>ForceBumpStrategy.None</c>
    /// </summary>
    let forceBumpStrategy = ForceBumpStrategy.None
    /// <summary>
    /// Default <c>ForceBumpValue.Auto</c>
    /// </summary>
    let forceBumpValue = ForceBumpValue.Auto
    /// <summary>
    /// Default <c>GenerateReleaseStrategy.PersScope</c>
    /// </summary>
    let generateReleaseStrategy = GenerateReleaseStrategy.PerScope
    /// <summary>
    /// Default <c>GenerateRelease.None</c>
    /// </summary>
    let generateRelease = GenerateRelease.None
    /// <summary>
    /// <para>Epoch: EpochMatcher 'epoch'</para>
    /// <para>Major: Types - 'breaking', 'remove'</para>
    /// <para>Minor: Types - 'feat', 'new', 'add'</para>
    /// <para>Patch: Types - 'fix', 'perf', 'update', 'change'</para>
    /// </summary>
    let commitBumpTypeMapping = {
        Epoch = [
            EpochMatcher("epoch")
        ]
        Major = [
            BumpMatcher.Type "breaking"
            BumpMatcher.Type "remove"
        ]
        Minor = [
            BumpMatcher.Type "feat"
            BumpMatcher.Type "new"
            BumpMatcher.Type "add"
        ]
        Patch = [
            BumpMatcher.Type "fix"
            BumpMatcher.Type "perf"
            BumpMatcher.Type "update"
            BumpMatcher.Type "change"
        ]
    }
    /// <summary>
    /// <para>Breaking: breaking</para>
    /// <para>Changed: update, change, updated</para>
    /// <para>Deprecated: depr, deprecated, deprecate</para>
    /// <para>Feat: feat, enhancement, new, added</para>
    /// <para>Fix: fix, fixed</para>
    /// <para>Revert: revert, rollback</para>
    /// </summary>
    let groupMatchers = [
        GroupMatcher(CommitGroup.Defaults.breaking, [ BumpMatcher.Type "breaking" ])
        GroupMatcher(CommitGroup.Defaults.changed, [
            BumpMatcher.Type "update"
            BumpMatcher.Type "updated"
            BumpMatcher.Type "change"
        ])
        GroupMatcher(CommitGroup.Defaults.deprecated, [
            BumpMatcher.Type "depr"
            BumpMatcher.Type "deprecated"
            BumpMatcher.Type "deprecate"
        ])
        GroupMatcher(CommitGroup.Defaults.feat, [
            BumpMatcher.Type "feat"
            BumpMatcher.Type "enhancement"
            BumpMatcher.Type "new"
            BumpMatcher.Type "added"
        ])
        GroupMatcher(CommitGroup.Defaults.fix, [
            BumpMatcher.Type "fix"
            BumpMatcher.Type "fixed"
        ])
        GroupMatcher(CommitGroup.Defaults.revert, [
            BumpMatcher.Type "rollback"
            BumpMatcher.Type "revert"
        ])
    ]
    /// <summary>
    /// <code>
    /// IgnoreCommit.SkipCi
    /// IgnoreCommit.FooterKeyValue("changelog", "true")
    /// IgnoreCommit.FooterKeyValue("gitnet", "ignore")
    /// </code>
    /// </summary>
    let ignoreCommits: IgnoreCommit list = [
        IgnoreCommit.SkipCi
        IgnoreCommit.FooterKeyValue("changelog", "true")
        IgnoreCommit.FooterKeyValue("gitnet","ignore")
    ]
    /// <summary>
    /// Default <c>true</c>
    /// </summary>
    let allowUnconventional = true
    /// <summary>
    /// Default <c>CommitGroup.Defaults.other</c>
    /// </summary>
    let defaultUnmatchedGroup = CommitGroup.Defaults.other
    /// <summary>
    /// Default <c>true</c>
    /// </summary>
    let allowUnmatched = true
    /// <summary>
    /// <code>
    /// FileSplitting = fileSplittingStrategy
    /// MacroGrouping = macroGroupType
    /// Path = IO.Path.Combine(Environment.CurrentDirectory, "RELEASE_NOTES.md") |> outputPathType
    /// Formatting = macroGroupType
    /// GroupMatcher = groupMatchers
    /// Ignore = ignoreCommits
    /// AllowUnconventional = allowUnconventional
    /// DefaultUnmatchedGroup = defaultUnmatchedGroup
    /// AllowUnmatched = allowUnmatched
    /// </code>
    /// </summary>
    let outputConfig = {
        FileSplitting = fileSplittingStrategy
        MacroGrouping = macroGroupType
        Path = IO.Path.Combine(Environment.CurrentDirectory, "RELEASE_NOTES.md") |> outputPathType
        Formatting = macroGroupType
        GroupMatcher = groupMatchers
        Ignore = ignoreCommits
        AllowUnconventional = allowUnconventional
        DefaultUnmatchedGroup = defaultUnmatchedGroup
        AllowUnmatched = allowUnmatched
    }
    /// <summary>
    /// <code>
    /// Mapping = commitBumpTypeMapping
    /// DefaultBumpStrategy = forceBumpStrategy
    /// GenerateRelease = generateRelease
    /// GenerateReleaseStrategy = generateReleaseStrategy
    /// </code>
    /// </summary>
    let bumpConfig = {
        Mapping = commitBumpTypeMapping
        DefaultBumpStrategy = forceBumpStrategy
        GenerateRelease = generateRelease
        GenerateReleaseStrategy = generateReleaseStrategy
    }
    /// <summary>
    /// Default <c>Url = GitHubUrlStrategy.Auto</c>
    /// </summary>
    let githubConfig = {
        Url = GithubUrlStrategy.Auto
    }
    /// <summary>
    /// Default <c>{ Github = githubConfig }</c>
    /// </summary>
    let networkConfig = {
        Github = githubConfig
    }
    let gitNetConfig isFSharp = {
        Network = networkConfig
        RepositoryPath = System.Environment.CurrentDirectory
        Scope = if isFSharp then FSharp.scopeStrategy else NonFSharp.scopeStrategy
        InitialVersionStrategy =
            projectInitialVersion
        Output = outputConfig
        ProjectType =
            if isFSharp then
                FSharp.projectFSharpConfig
                |> Some
                |> FSharp.projectType
            else
                NonFSharp.projectNoneConfig
                |> NonFSharp.projectType
        Bump = bumpConfig
        AssemblyFiles = FSharp.assemblyFileManagement
        WriteVersionToProjects = true
    }

module ProjectFSharpConfig =
    let init = Defaults.FSharp.projectFSharpConfig
module ProjectNoneConfig =
    let init = Defaults.NonFSharp.projectNoneConfig
module CommitConfig =
    let init = Defaults.commitConfig
module CommitBumpTypeMapping =
    let init = Defaults.commitBumpTypeMapping
module OutputConfig =
    let init = Defaults.outputConfig
module BumpConfig =
    let init = Defaults.bumpConfig
module GithubConfig =
    let init = Defaults.githubConfig
module NetworkConfig =
    let init = Defaults.networkConfig
module GitNetConfig =
    let init isFSharp = Defaults.gitNetConfig isFSharp
    let initFSharp = Defaults.gitNetConfig true
    let initNonFS = Defaults.gitNetConfig false

/// <summary>
/// Convenience active patterns to elicit specific settings from a <c>GitNetConfig</c> record within pattern matches.
/// </summary>
module Patterns =
    let (|FileSplitting|) = _.Output.FileSplitting
    let (|ScopePrefixFormat|) = function
        | { Output = { Formatting = MacroGroupType.ScopePrefix(value) } } ->
            value
        | _ -> ScopePrefixConfig.None
    let (|GroupStrategy|) = function
        | { Output = { MacroGrouping = grouping } } ->
            grouping
    let (|OutputPath|) = function
        | { Output = { Path = OutputPathType.Simple path } } ->
            fun (_: string) -> path
        | { Output = { Path = OutputPathType.PerScope func }; RepositoryPath = repoPath } ->
            fun inp -> System.IO.Path.Combine(repoPath,func inp)
        | { RepositoryPath = repoPath } ->
            fun _ -> System.IO.Path.Combine(repoPath, "RELEASE_NOTES.md")
    let (|FSharpNameResolution|) = function
        | { ProjectType = ProjectType.FSharp(Some { NameResolution = nameResolution }) } ->
            Some nameResolution
        | _ -> None
    let (|ProjectInitialVersionStrategy|) = function
        | { InitialVersionStrategy = versioning } -> versioning
    let (|FSharpProjectConfig|) = function
        | { ProjectType = ProjectType.FSharp(config) } -> config
        | _ -> None
    let (|NoneProjectConfig|) = function
        | { ProjectType = ProjectType.None config } -> Some config
        | _ -> None
    let (|IgnoreCommits|) = function
        | { Output = { Ignore = l } } -> l
    let (|ForceBumpStrategy|) = function
        | { Bump = { DefaultBumpStrategy = value } } -> value
    let (|GenerateRelease|) = function
        | { Bump = { GenerateReleaseStrategy = strategy; GenerateRelease = value } } ->
            strategy,value
    let (|AssemblyFiles|) = function
        | { AssemblyFiles = value } -> value
    let (|ScopeStrategy|) = function
        | { Scope = strat } -> strat
    let (|CommitBumpMappings|) = function
        | { Bump = { Mapping = value } } -> value
    let (|AllowUnconventional|) = function
        | { Output = { AllowUnconventional = value } } -> value
    let (|UnmatchedGroup|) = function
        | { Output = { DefaultUnmatchedGroup = value } } -> value
    let (|AllowUnmatched|) = function
        | { Output = { AllowUnmatched = value } } -> value
    let (|GroupMatcher|) = function
        | { Output = output } -> output.ComputeGroupMatcher
    let (|BumpMatcher|) = function
        | { Bump = bump } -> bump.Mapping.ComputeBumpMatcher
