# Let's Play Baseball (LPB)

A modular, physics-grounded baseball simulation and management project written primarily in C#.

M0 established the development foundation. M1 includes gravity-only and gravity-plus-quadratic-drag RK4, immutable results, first-ground-contact termination, and optional continuous trajectory analysis. Issue #6 was independently reviewed and merged through [PR #21](https://github.com/skolins3651/lets-play-baseball/pull/21). The Trajectory Lab ([Issue #19](https://github.com/skolins3651/lets-play-baseball/issues/19), PR #20) now supports both models through the scoped #6 follow-up; this follow-up remains local pending review and publication authorization.

The [current architecture](docs/architecture.md) connects the project-wide design to the [physics conventions and analytical fixtures](docs/physics/conventions.md), [gravity-only contracts](docs/physics/gravity-only-contract.md), and [drag contracts and verification](docs/physics/drag-model.md). The authoritative simulation must remain independent of Unity, rendering, databases, and external runtime services. Future Unity integration may require retargeting, multi-targeting, or an adapter; a .NET 10 assembly is not assumed to load directly into Unity.

## Prerequisites

- Git and access to [the GitHub repository](https://github.com/skolins3651/lets-play-baseball).
- [.NET SDK 10.0.403](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), installed on every development computer. A runtime-only installation is insufficient.
- JetBrains Rider is the preferred editor; the .NET CLI also works independently of an IDE.

All five projects target `net10.0`. Root `global.json` selects exactly SDK `10.0.403`, disables roll-forward, and excludes previews. No additional .NET workloads, Unity, Blender, database, Python, Node.js, or frontend package installation is needed. The visualizer uses the ASP.NET Core shared framework included with the SDK and a modern browser with WebGL for its 3D view.

## Solution overview

```text
LPB.slnx
src/
  Lpb.Physics/             Engine-independent gravity/drag RK4, shared execution, and immutable contracts
  Lpb.Cli/                 Console shell; references Lpb.Physics
  Lpb.Visualizer/          Loopback ASP.NET Core app and locally bundled Plotly.js; references Physics
tests/
  Lpb.Physics.Tests/       NUnit analytical, RK4, safety, contract, and smoke tests; references Physics
    packages.lock.json    Locked test dependency graph
  Lpb.Visualizer.Tests/    Adapter, sampling, ownership, and HTTP tests; browser-state harness
    packages.lock.json    Locked visualizer test dependency graph
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

SDK selection should report `10.0.403`. The smoke and input-contract tests remain intact. Numerical tests verify all four analytical gravity fixtures and a separate standard-gravity reference at `1e-9` absolute SI tolerance; a manufactured nonconstant derivative verifies the four RK4 stages and weighting at `1e-13`. Further tests cover immutable outputs, indexed timing, resource boundaries, and numerical failures. These tolerances apply to the specified fixtures, not arbitrary requests. The test command fails if no tests are discovered or executed.

The CLI currently accepts invocation without arguments, prints the following line, and exits successfully:

```text
LPB bootstrap ready. Physics simulation is not implemented.
```

This is legacy bootstrap-shell text, preserved until the CLI workstream replaces it; it no longer describes the library's capabilities. The CLI still has no command parser, scenario-input handling, simulation command, or export schema.

## Local trajectory visualizer

From the repository root, on macOS or Windows PowerShell:

```sh
dotnet run --project src/Lpb.Visualizer/Lpb.Visualizer.csproj
```

Open [http://localhost:5055](http://localhost:5055), enter launch settings, and press **Run simulation**. After a Release build, add `--configuration Release --no-build --no-restore`. Change an occupied port with `-- --port 5056`; binding remains loopback-only. Stop with Ctrl+C.

Three coordinated views display the same C# numerical trajectory. Speed defaults to mph; distances to feet; radius to inches. Integration timestep and plotted-point count are separate controls. **Apply density** reuses the captured last successful run even when launch inputs have pending edits. All experiments are temporary; refresh, expiry, eviction, or shutdown can lose them. No historical saves or exports are provided.

**Gravity only** remains the default, with the existing 100 mph / 30° elevation / 45° azimuth launch, 3 ft center height, 1.45 in radius, 0.01 s timestep, and 10 s limit. Select **Gravity + drag** to expose illustrative mass **145 g**, density **1.225 kg/m³**, and constant coefficient **0.35**. These are uncalibrated stationary-air assumptions without spin, lift, wind, or variable atmosphere. The single radius supplies both aerodynamic and contact geometry. Both models support contact-aware and unconstrained flight independently. Zero density/coefficient retains drag identity and settings while disabling drag numerically. Inactive aerodynamic edits are retained in the form but excluded from gravity-only execution and pending comparison.

See the [visualizer guide](docs/visualizer.md) for controls, exact directions, coordinate references, numerical fallbacks, HTTP ownership, limits, and browser verification. Plotly.js 4.1.2 is bundled locally with its [license and provenance](src/Lpb.Visualizer/wwwroot/vendor/README.md); no CDN is used at runtime.

## Gravity-only library API

A C# consumer referencing `Lpb.Physics` can execute a trajectory directly:

```csharp
using Lpb.Physics;

var request = new FlightRequest(
    new FlightState(new Vector3d(0, 0, 0), new Vector3d(6, 8, 10)),
    gravityMagnitude: 10,
    duration: 2,
    timeStep: 0.25);
SimulationResult result = GravityOnlySimulator.Simulate(request);
// Nine samples, including t = 0 and exactly t = 2 seconds.
FlightState finalState = result.FinalState;
```

Position is ball-centre position in metres; velocity is metres/second in the documented field frame. Gravity is explicit and acts along negative Z. `TrajectorySample` contains time and state. `SimulationResult.Samples` is an owned, immutable snapshot; final state/time come directly from its last sample, and completion is `DurationReached` or `GroundContact`. The unconstrained API above still always reaches duration.

The nominal timestep defines indexed double-precision boundaries rather than repeated additions. The final interval is shortened as necessary to reach the exact requested duration; a timestep larger than duration is valid. Preflight rejects more than **100,000 steps / 100,001 samples** before trajectory allocation. Null invocation throws `ArgumentNullException`, resource rejection throws `InvalidOperationException`, and unsafe arithmetic or unrepresentable time stages throw `ArithmeticException`. Failures never return a partial successful result. See [the contract](docs/physics/gravity-only-contract.md) for exact grid and representability rules.

Contact-aware execution is explicit:

```csharp
var contact = new GroundContactSettings(groundElevation: -0.05, ballRadius: 0.05);
var maximumDuration = new FlightRequest(request.InitialState, 10, 5, 0.3);
var landing = GravityOnlySimulator.SimulateUntilGroundContact(maximumDuration, contact);
TrajectorySample? contactSample = TrajectoryMeasurements.GetGroundContact(landing);
Vector3d displacement = TrajectoryMeasurements.GetDisplacement(landing);
Vector3d horizontal = TrajectoryMeasurements.GetHorizontalDisplacement(landing); // Z = 0
TrajectorySample maximum = GravityOnlyTrajectory.GetMaximumHeight(landing, maximumDuration);
FlightState interior = GravityOnlyTrajectory.Evaluate(landing, maximumDuration, 0.5);
// Contact time is landing.FinalTime; contactSample describes the ball center, not the ground point.
```

Contact height is ground elevation plus positive radius. Reject nonfinite geometry and a radius too small to increase the representable elevation. Exact initial contact moving upward launches; stationary/downward contact returns one sample at zero; penetration is rejected. Contact at duration takes precedence. Unfinished flights return no landing data.

Contact-aware execution caps **actual ordinary work at 100,000 steps**, allowing long requested horizons with early contact. Budget exhaustion throws instead of jumping to duration. Contact and same-step apex searches each allow at most 128 additional evaluations. All local contact bounds must pass: `1e-10 s` time width, `1e-9 m` height residual/position uncertainty, and `1e-9 m/s` velocity uncertainty. Only then may terminal center Z be projected onto the plane. These bounds do not replace accumulated integration-error assessment.

Measurements are optional. Numerical analysis requires explicit matching request values and private immutable provenance supplied by **both** simulator APIs. Recorded timestamps return stored states exactly; interior evaluation uses RK4 without projection. Maximum height is continuous over the actual interval, including between samples, with earliest time for constant height. Numerical inability to resolve a required event or analysis query throws `ArithmeticException`.

The model has constant gravity only: no air resistance, wind, spin, bounce, or rolling. The separate visualizer consumes this library; CLI scenarios, structured exports, and comparison plotting remain later work. Unconstrained paths continue below ground. Finite inputs can still overflow RK4 arithmetic or lack representable stage times; ordinary double rounding also limits precision. No claim of general convergence or empirical baseball accuracy follows from these baseline verification tests.

## Gravity-plus-drag library API

The common `FlightRequest` constructor is unchanged. Aerodynamic parameters are explicit immutable assumptions, with no constructor defaults:

```csharp
var aerodynamics = new AerodynamicSettings(
    ballMass: 0.145, ballRadius: 0.0366, airDensity: 1.225, dragCoefficient: 0.35);
var flight = new FlightRequest(
    new FlightState(new Vector3d(0, 0, 1.0366), new Vector3d(30, 20, 25)),
    gravityMagnitude: 9.80665, duration: 6, timeStep: 0.01);
var drag = GravityDragSimulator.SimulateUntilGroundContact(flight, aerodynamics, groundElevation: 0);
var state = GravityDragTrajectory.Evaluate(drag, flight, aerodynamics, 1.2345);
var apex = GravityDragTrajectory.GetMaximumHeight(drag, flight, aerodynamics);
// drag.ModelKind == FlightModelKind.GravityAndDrag
```

`GravityDragSimulator.Simulate(flight, aerodynamics)` provides unconstrained flight. Contact takes ground elevation only and derives the ball radius from the aerodynamic configuration. Mass/radius must be finite and positive; density/coefficient finite and nonnegative. The values above illustrate baseball-scale inputs and are not calibrated parameters.

Enabled drag compares one full RK4 step against two half-steps in **all six components**, accepting raw differences no greater than `1e-8 m` / `1e-8 m/s` and applying representation and model-sign checks. It retains the full-step state, does not adapt the requested timestep, and throws when required checks fail. Event refinement uses separate bounded numerical criteria and a checked interior apex witness. These observed checks do not certify global trajectory error or empirical baseball accuracy.

A zero density or coefficient reuses established gravity computation and event guarantees while retaining **drag identity and complete aerodynamic provenance**. Gravity-only evaluators reject that result; drag evaluators accept equivalent matching configurations and reject mismatches, even at recorded timestamps. Samples, actual completion, contact information, and on-demand measurements remain authoritative immutable Physics values. Optional analysis failure does not invalidate a successful flight.

See [the drag specification](docs/physics/drag-model.md) for tolerances, resource accounting, independent references, rounding regressions, practical performance, and limitations. The research-only Python fixture generator is optional; ordinary build and CI remain .NET-only. The CLI remains unchanged. The Lab selects the matching authoritative APIs, preserves numerical diagnostics and optional-analysis fallback, and never silently changes numerical settings. [Issue #22](https://github.com/skolins3651/lets-play-baseball/issues/22) tracks the separate rounded exact-root gravity contact limitation, also inherited by disabled drag.

## Test dependencies and reproducibility

Only the test projects have direct NuGet dependencies:

| Package | Version | Purpose |
| --- | --- | --- |
| NUnit | 5.0.0 | Test framework; includes a .NET 10 target |
| NUnit3TestAdapter | 6.3.1 | NUnit discovery/execution through VSTest |
| Microsoft.NET.Test.Sdk | 18.11.0 | VSTest test-host/build integration |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.14 | Visualizer tests only; HTTP test host and static-content integration |

NUnit 5 is selected over the older SDK-template version. See the [NUnit release notes](https://docs.nunit.org/articles/nunit/release-notes/framework.html) and the [adapter package](https://www.nuget.org/packages/NUnit3TestAdapter/6.3.1). Despite its name, `NUnit3TestAdapter` supports newer NUnit frameworks. The project uses VSTest; it does not opt into Microsoft.Testing.Platform. Native Rider runner support for a new NUnit major release may differ; the documented CLI test command is the shared validation path.

Both test projects enable NuGet lock-file generation and check in their `packages.lock.json`. CLI, Physics, and Visualizer have no direct NuGet package dependencies and do not need lock files. Locked solution restore validates both test graphs while restoring all five projects. Initial restore needs access to NuGet; subsequent builds and tests reuse restored dependencies. The visualizer runtime has no analytics or telemetry service; existing test-tool transitive packages are confined to tests.

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

The workflow runs on pull requests targeting `main` and pushes to `main`, with `ubuntu-latest` and `windows-latest` jobs. Each job installs the pinned SDK, restores locked dependencies, builds Release, runs the full NUnit suite with zero-test failure enabled, and runs the CLI. It requests only repository-content read permission and includes no coverage, caching, publishing, or deployment steps.

Local validation does not establish a passing GitHub Actions run. Each change requires its own CI verification after publication; Windows and Linux execution are checked by the workflow. macOS validation uses the same commands locally.
