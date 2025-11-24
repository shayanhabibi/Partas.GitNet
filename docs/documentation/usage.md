---
title: Usage
category: Documentation
description: Usage of GitNet
---

# GitNet Usage in CI

The first thing to manage when using `GitNet` is building a `GitNetRuntime`, which is a disposable wrapper around `LibGit2Sharp.Repository` that helps by caching particular methods and functions when built.

Building a runtime requires you to feed a `GitNetConfig` to its constructor.

## GitNetConfig

Build a config from scratch, or use the default config and modify it to your needs.

## GitNetRuntime

Create a gitnet runtime with the config made previously.

```fsharp
// In a CI you usually won'y be managing
// temporary lifetimes, so a `let` should
// be permissible.
let runtime = new GitNetRuntime(config)
```

## Run Methods

You can typically just use the `_.Run` method to automatically determine the bumps, apply the tags, version files as determined by your config, create your RELEASE_NOTES and stage/commit them as configured.

## Controlled Usage

If you wish to control the versioning/actions, then you will instead want to use a combination of the runtimes `_.DryRun` and other methods/functions.

### DryRun

DryRuns do not involve modification/commits of files, and simply calculates what bumps would be conducted for the commits in the repository, and the content of the MarkdownFile you would pipe to the release notes.

The typical `_.Run` uses `_.DryRun` under the hood itself.

A dry run returns you a `RuntimeOutput` record:

```fsharp
type RuntimeOutput = {
    Markdown: string
    Bumps: IDictionary<string, SepochSemver>
    Versions: FrozenDictionary<string, GitNetTag voption>
}
```

### CrackRepo

`_.CrackRepo` lazily evaluates the projects and their settings in the repository. You'll typically use this to modify the project file programmatically.

There are bindings to map various properties within the `.fsproj`, compute values of properties, or stage files in the `CrackedProject` module.

### Creating a Workflow

A `_.Run` completes the following set of actions.

1. Completes a DryRun
2. Completes Project Cracker
3. Filters projects 
   * Configured to AutoBump
   * Are being bumped according to the DryRun
4. Writes and stages version bumps to assembly files if configured
5. Writes and stages version bumps to project files if configured
6. Commits changes and tags if configured
7. Rewrites the RELEASE_NOTES and Commits changes if configured
