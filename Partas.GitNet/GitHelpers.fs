module Partas.GitNet.GitHelpers

open LibGit2Sharp.FSharp
open Partas.Tools.SepochSemver
let private getCommitsAfter repo o =
    Repository.commits repo
    |> CommitLog.Query.until o

let private getCommitsBetween repo o1 o2 =
    Repository.commits repo
    |> CommitLog.Query.between o1 o2

let private getCommitsTo repo o =
    Repository.commits repo
    |> CommitLog.Query.since o

type GitNetRuntime with
    member this.GetCommitsAfter(tag: GitNetTag) =
        GitNetTag.git tag |> getCommitsAfter this.repo
    member this.GetCommitsBetween(tag1: GitNetTag, tag2: GitNetTag) =
        (tag1 |> GitNetTag.git, tag2 |> GitNetTag.git)
        ||> getCommitsBetween this.repo
    member this.GetCommitsTo(tag: GitNetTag) =
        GitNetTag.git tag |> getCommitsTo this.repo
    member this.DiffCommitPaths paths commit =
        let tree = commit |> Commit.tree
        this.diff.Compare<TreeChanges>(tree, DiffTargets.Index, paths)
    member private this.DiffCommit(commit) =
        let tree = commit |> Commit.tree
        this.diff.Compare<TreeChanges>(tree, DiffTargets.Index)
    member this.Diff(paths,commit) = this.DiffCommitPaths paths commit
    member this.Diff(commit: Commit) = this.DiffCommit(commit)
    member this.Diff(tree: LibGit2Sharp.Tree) = this.diff.Compare<TreeChanges>(tree, DiffTargets.Index)
    member this.Diff(commit: GitNetCommit) = commit |> GitNetCommit.git |> this.Diff
