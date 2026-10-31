# Let's Play Baseball (LPB)

A modular, physics-grounded baseball simulation and management project written primarily in C#.

M0 establishes the development foundation: an empty physics library, a headless CLI shell, an automated smoke test, and build/test CI. Physics models, physical state, numerical integration, trajectories, and scientific validation begin in M1.

The architecture baseline is [working draft v0.2](docs/architecture.md). The authoritative simulation must remain independent of Unity, rendering, databases, and external runtime services. Future Unity integration may require retargeting, multi-targeting, or an adapter; a .NET 10 assembly is not assumed to load directly into Unity.

## Prerequisites

- Git and access to [the GitHub repository](https://github.com/skolins3651/lets-play-baseball).
- [.NET SDK 10.0.403](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), installed on every development computer. A runtime-only installation is insufficient.
- JetBrains Rider is the preferred editor; the .NET CLI also works independently of an IDE.

All three projects target `net10.0`. Root `global.json` selects exactly SDK `10.0.403`, disables roll-forward, and excludes previews. No additional .NET workloads, Unity, Blender, database, or Python installation is needed for M0.

## Solution overview

```text
LPB.slnx
src/
  Lpb.Physics/             Engine-independent library; initially no public types
  Lpb.Cli/                 Console shell; references Lpb.Physics
tests/
  Lpb.Physics.Tests/       NUnit infrastructure smoke test; references Lpb.Physics
    packages.lock.json    Locked test dependency graph
.github/workflows/ci.yml   Linux and Windows build/test workflow
```

Open `LPB.slnx` in Rider. Existing local Rider settings are ignored by Git and should be preserved.

## Restore, build, test, and run

Run these commands from the repository root. They work in macOS shells and Windows PowerShell without platform-specific build scripts.

```sh
dotnet --version
dotnet restore LPB.slnx --locked-mode
dotnet build LPB.slnx --configuration Release --no-restore
dotnet test LPB.slnx --configuration Release --no-build --no-restore -- RunConfiguration.TreatNoTestsAsError=true
dotnet run --project src/Lpb.Cli/Lpb.Cli.csproj --configuration Release --no-build --no-restore
```

SDK selection should report `10.0.403`. The smoke test loads and identifies the physics assembly; it establishes project wiring and test execution, not physics correctness. The test command fails if no tests are discovered or executed.

The CLI currently accepts invocation without arguments, prints the following line, and exits successfully:

```text
LPB bootstrap ready. Physics simulation is not implemented.
```

No command parser, simulation settings, scenario inputs, or output schema is implemented in M0.

## Test dependencies and reproducibility

Only the test project has direct NuGet dependencies:

| Package | Version | Purpose |
| --- | --- | --- |
| NUnit | 5.0.0 | Test framework; includes a .NET 10 target |
| NUnit3TestAdapter | 6.3.1 | NUnit discovery/execution through VSTest |
| Microsoft.NET.Test.Sdk | 18.11.0 | VSTest test-host/build integration |

NUnit 5 is selected over the older SDK-template version. See the [NUnit release notes](https://docs.nunit.org/articles/nunit/release-notes/framework.html) and the [adapter package](https://www.nuget.org/packages/NUnit3TestAdapter/6.3.1). Despite its name, `NUnit3TestAdapter` supports newer NUnit frameworks. The project uses VSTest; it does not opt into Microsoft.Testing.Platform. Native Rider runner support for a new NUnit major release may differ; the documented CLI test command is the shared validation path.

The test project enables NuGet lock-file generation, and its `packages.lock.json` is checked in. The dependency-free CLI and physics library do not need lock files. Locked solution restore validates the test dependency graph while restoring all three projects. Initial restore needs access to NuGet; subsequent builds and tests reuse restored dependencies.

When deliberately changing package versions, edit the test project, run `dotnet restore LPB.slnx --force-evaluate`, review the lock-file diff, and repeat the validation commands above. Commit the project and lock-file changes together when publication is authorized.

To adopt an SDK servicing update, install the selected SDK on each computer, update `sdk.version` in root `global.json` and the version recorded in this README, and retain the deliberate roll-forward policy. Run restore with `--force-evaluate`, review any lock-file changes, and repeat local and CI validation. CI installs its SDK from `global.json`.

## Development across computers

Use an independent local clone on the Mac laptop and each Windows 11 desktop. GitHub is the authoritative remote for committed source changes. Keep active clones outside OneDrive, Dropbox, iCloud Drive, and similar synchronized folders. Uncommitted changes, ignored settings, and build outputs stay on the computer where they were created.

For an initial clone:

```sh
git clone https://github.com/skolins3651/lets-play-baseball.git
cd lets-play-baseball
```

Before starting a new change, ensure the working tree is clean, update `main`, and create a feature branch:

```sh
git switch main
git pull --ff-only
git switch -c chore/example-change
```

After making and validating a change, review the diff and stage only intended files. When authorized to publish, commit and push the feature branch, then use a pull request to integrate into `main`:

```sh
git status
git diff
git add <intended-files>
git commit -m "Describe the change"
git push -u origin chore/example-change
```

Before moving to another computer, commit and push the work you intend to transfer when authorized. On the other computer, begin with a clean working tree, fetch the branch, switch to it, and pull:

```sh
git fetch origin
git switch chore/example-change
git pull --ff-only
```

For a branch not yet present locally, `git switch` creates a tracking branch from the fetched branch on `origin`. If the working tree has uncommitted work, preserve it before switching branches; it does not automatically transfer to another clone. Avoid making divergent changes to the same branch on multiple computers before synchronizing.

`.gitattributes` normalizes text files to LF, while `.editorconfig` provides matching UTF-8, newline, and indentation defaults. Local SDK installations and IDE preferences remain machine-specific. Unity/Blender asset workflows, Git LFS, and graphics tooling are deferred until needed.

## Continuous integration

The workflow runs on pull requests targeting `main` and pushes to `main`, with `ubuntu-latest` and `windows-latest` jobs. Each job installs the pinned SDK, restores locked dependencies, builds Release, runs the smoke test with zero-test failure enabled, and runs the CLI. It requests only repository-content read permission and includes no coverage, caching, publishing, or deployment steps.

Local validation does not establish a passing GitHub Actions run. CI verification remains pending until the workflow is published and executes on GitHub. macOS is validated locally for M0; Windows and Linux execution are checked by the workflow after publication.
