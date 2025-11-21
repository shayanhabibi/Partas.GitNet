---
title: Overview
category: Documentation
categoryindex: 1
description: Overview of GitNet
---


# GitNet: CI Tools and Libraries for F#

Install with dotnet.

```bash
dotnet add package Partas.GitNet
```

## Usage

> Under construction

For the moment, please see the API reference [for the entry class you build](reference/partas-gitnet-gitnet-runtime-gitnetruntime.html) and the [main entry methods](reference/partas-gitnet-runextensions.html) of that class.

## How it works

Let's have a look at the Mono-repo for GitNet itself, which contains 3 projects that I publish and version separately:

```generic
root
|
|- Partas.GitNet
|   |- Partas.GitNet.fsproj
|   |- **.fs
|
|- Partas.GitNet.Markdown
|   |- Partas.GitNet.Markdown.fsproj
|   |- **.fs
|   
|- Partas.Tools.SepochSemver
    |- Partas.Tools.SepochSemver.fsproj
    |- **.fs
```

1. First, we find and consume all the `.fsproj` files in a repository.<br/><blockquote>We gather information such as the name of the project, the current version, any GitNet specific settings etc.</blockquote>
2. Parse git tags as possible SemVer tags or SepochSemver tags
3. Categorise SepochSemver tags (SemVer tags do not have information to make them assignable to a project)
4. Parse commits for the projects into ConventionalCommits
5. Categorise commits by the project they effect.<br/><blockquote>This is determined by whether the commit contains diffs within the directory of the project.</blockquote>
6. Categorise commits by the tags they fall under. The rest are collected as 'Unreleased' commits.
7. Determine whether the unreleased commits would result in a bump under the rules of ConventionalCommits and SemVer, and then apply this bump (* this depends on the GitNet configuration)
8. Generate/modify AssemblyInfo files (* this depends on the GitNet configuration)
9. Modify the `.fsproj` files with the new Version/PackageVersion information (* this depends on the GitNet configuration).
10. Stages or commits the changes/tags depending on your configuration; all you have to do is push the changes if all tests pass etc.

### What does it all mean!

Imagine the following commit made to a file in `Partas.GitNet`:

```
feat!: Change consumer API to new schema
```

It would be unnecessary for this commit to result in a major version bump for all 3 projects (especially if they are unrelated).

Partas.GitNet however, only views this commit as being related to the project `Partas.GitNet`, and so it would result in the commit only showing in the release notes for that project, and result in a major bump for that project alone.

## Alternatives

There are alternative methodologies to achieve similar outcomes, you could use `EasyBuild.ChangelogGen` and associated tools, by delineating your projects by a footer value 'Tag: &lt;project>', and generating filtered changelogs for those projects, and versioning based on changes in the changelogs etc.

Personally, I find my approach to be a bit more rigid (in how it can coop with other approaches), but abstracts more away for me.

## Configuration

> Under construction

## Recipes

> Under construction

## CLI

> Both the CLI itself and the documentation are under construction

## Future Direction

> Under construction
