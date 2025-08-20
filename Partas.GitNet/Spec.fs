module Partas.GitNet.Spec

let [<Literal>] dateFormat = "yyyy-M-dd"

module MSBuild =
    /// Explicitly sets the scope value for a FSProject.
    [<Literal>]
    let Scope = "GitNetScope"
    /// Explicitly sets the current Epoch for a FSProject
    [<Literal>]
    let Epoch = "GitNetEpoch"
    /// Explicitly declares for GitNet to automatically
    /// bump the versions when acted on.
    [<Literal>]
    let AutoBump = "GitNetAutoBump"
    /// Explicitly declare the initial version
    /// to instantiate if no other is found.
    [<Literal>]
    let InitialVersion = "GitNetInitialVersion"
    /// Explicitly declare whether to only auto bump
    /// if the current branch is equal to the set value.
    [<Literal>]
    let AutoBumpBranchName = "GitNetAutoBumpBranchName"
    /// Explicitly declare whether to automatically
    /// update assembly files for the project.
    [<Literal>]
    let AutoUpdateAssemblyFiles = "GitNetAutoUpdateAssemblyFiles"
    
