module Partas.GitNet.Spec

/// <summary>
/// The string format used in printing.
/// </summary>
let [<Literal>] dateFormat = "yyyy-MM-dd"

/// <summary>
/// These are literals that can be used as tags in <c>.fsproj</c> <c>PropertyGroup</c> elements.
/// Their impact and relevance is discussed for each item.
/// </summary>
module MSBuild =
    /// <summary>
    /// Explicitly sets the scope value for a FSProject.
    /// </summary>
    /// <example>
    /// <para>
    /// For the case of a project <c>Partas.Solid.Common</c>, the default scheme results in the scope being
    /// <c>Common</c>.
    /// </para>
    /// <para>A method of explicitly setting the Scope within the project file is using the <c>GitNetScope</c>
    /// build property</para>
    /// </example>
    [<Literal>]
    let Scope = "GitNetScope"
    /// <summary>
    /// Explicitly sets the current Epoch for a FSProject
    /// </summary>
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
