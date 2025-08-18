module Partas.GitNet.Cli.ConfigWrapper

open Partas.GitNet

// Json schema
module OptionTypes =
    type FileSplittingStrategy = ConfigTypes.FileSplittingStrategy option
    type ScopePrefixConfig = ConfigTypes.ScopePrefixConfig option
    type MacroGroupType = ConfigTypes.MacroGroupType option
    type OutputPathType = ConfigTypes.OutputPathType option
    type FSharpNameResolution = ConfigTypes.FSharpNameResolution option
    type ProjectInitialVersionStrategy = ConfigTypes.ProjectInitialVersionStrategy option
    type ProjectFSharpConfig = ConfigTypes.ProjectFSharpConfig option
    type ProjectNoneConfig = ConfigTypes.ProjectNoneConfig option
    type IgnoreCommit = ConfigTypes.IgnoreCommit option
    type CommitConfig = ConfigTypes.CommitConfig option
    type ForceBumpStrategy = ConfigTypes.ForceBumpStrategy option
    type ForceBumpValue = ConfigTypes.ForceBumpValue option
    type GenerateReleaseStrategy = ConfigTypes.GenerateReleaseStrategy option
    type GenerateRelease = ConfigTypes.GenerateRelease option
    type AssemblyFileManagement = ConfigTypes.AssemblyFileManagement option
    type ScopeStrategy = ConfigTypes.ScopeStrategy option
    type BumpMatcher = ConfigTypes.BumpMatcher option
    type EpochMatcher = ConfigTypes.EpochMatcher option
    type CommitBumpTypeMapping = ConfigTypes.CommitBumpTypeMapping option
    type GroupMatcher = ConfigTypes.GroupMatcher option
    type OutputConfig = ConfigTypes.OutputConfig option
    type BumpConfig = ConfigTypes.BumpConfig option
    type GithubUrlStrategy = ConfigTypes.GithubUrlStrategy option
    type GithubConfig = ConfigTypes.GithubConfig option
    type NetworkConfig = ConfigTypes.NetworkConfig option
    type GitNetConfig = ConfigTypes.GitNetConfig option

