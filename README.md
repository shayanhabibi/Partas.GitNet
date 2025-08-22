# GitNet

- [x] Configurable
- [x] Finds all your F# Projects
- [x] Checks what commits have effected the projects
- [x] ConventionalCommits determine if project gets bumped
- [x] Can write your `AssemblyInfo.fs` files
- [x] Adds or updates your `.fsproj` `<ProjectVersion>` and `<Version>` tags so packaging projects will have correct dependency versions
- [x] Commits its own changes, and creates versioned tags

I have the laziest CI, I build, package and push my projects on every run (with skip duplicate).

I can just do a git push at the end of the run. Delightful for me.

> [!WARNING]
> My CI is risky, and it's okay, because GitNet is
> configurable. You can run steps and inspect outputs to determine
> whether to make commits/change files etc.
> 
> Also unlike in this repo, you'd want to add tests into your CI (the tests in this repo depend on local git trees to test, at least until such a point that this repo has enough history to utilise).

## Git that Net

[Check out the output.](https://github.com/shayanhabibi/Partas.GitNet/blob/master/RELEASE_NOTES.md)

[See my build project that creates this.](https://github.com/shayanhabibi/Partas.GitNet/blob/master/Build/Program.fs#L71-L92)

## Development

The project is already a boon for me, and motivation to progress is kind of *eh*. There's lots of options out there, and this is highly opinionated.

If you like the looks of it, give it a star, and I'll probably work on documentation, and making a simple CLI (already have looked a bit into how this would work with SpectreCoff, Spectre.Console et al).
