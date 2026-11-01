# M1 Gravity-Only Simulator Contracts

> **Status:** Provisional M1 working specification — adopted 2026-11-01\
> **Issues:** [#3 — Physical state objects and simulation interfaces](https://github.com/skolins3651/lets-play-baseball/issues/3), with implementation deferred to [#4](https://github.com/skolins3651/lets-play-baseball/issues/4) and [#5](https://github.com/skolins3651/lets-play-baseball/issues/5) where indicated\
> **Scientific specification:** [`conventions.md`](conventions.md)\
> **Architecture:** [`docs/architecture.md`](../architecture.md) summarizes the current design; root [`AGENTS.md`](../../AGENTS.md) governs contributions.

## Purpose

Define the implemented, engine-independent input contracts and the intended future output behavior for the initial gravity-only trajectory simulator. Issue #3 implements only `Vector3d`, `FlightState`, and `FlightRequest`. Output types, executable simulation interfaces, and numerical integration are deferred to Issue #4. The contracts support deliberate evolution for ground-contact events, physical object parameters, aerodynamics, and spin without embedding their behavior now.

## Implemented inputs (Issue #3)

All three public types use the namespace `Lpb.Physics`. The concrete request contains:

```
FlightRequest
  InitialState
    Position: Vector3d       // m, ball center
    Velocity: Vector3d       // m/s
  GravityMagnitude: double   // m/s^2, finite and >= 0, acts -Z
  Duration: double           // s, finite and > 0
  TimeStep: double           // s, finite and > 0
```

| Type | Representation and constructor | Properties |
|---|---|---|
| `Vector3d` | Non-positional immutable `readonly record struct`; `Vector3d(double x, double y, double z)` | Get-only `double X`, `Y`, `Z` |
| `FlightState` | Non-positional immutable `readonly record struct`; `FlightState(Vector3d position, Vector3d velocity)` | Get-only `Vector3d Position`, `Velocity` |
| `FlightRequest` | Sealed immutable class; `FlightRequest(FlightState initialState, double gravityMagnitude, double duration, double timeStep)` | Get-only `InitialState`, `GravityMagnitude`, `Duration`, `TimeStep` |

`Vector3d` is unit-agnostic: its physical meaning and units come from the consuming property. `FlightState.Position` is ball-center position in metres; `Velocity` is linear velocity in metres/second. Both use the field-fixed coordinates in [`conventions.md`](conventions.md). Vector arithmetic operators and unit-wrapper types are not needed by Issue #3.

Standalone simulation starts at `t = 0 s`; time is elapsed simulation time, with no clock field in the initial state. All vector components are finite `double`s. Any coordinate (including negative `z`) and any finite velocity is acceptable **when ground contact is disabled**. Gravity and numerical settings are explicit scalar request properties; separate environment/settings wrapper types are unnecessary at this stage.

The scalar gravity magnitude is part of the initial **constant-gravity dynamics**, not a hardcoded assumption about every future force system or numerical integrator. `Vector3d` preserves double precision; `System.Numerics.Vector3` is single precision and must not silently substitute for it. There is no default gravity magnitude.

No baseball mass, radius, angular velocity, orientation, or air properties are required to run #4. Introduce parameters only when the relevant physical model needs them.

## Intended output (documented now; concrete types deferred to Issue #4)

The following remains a conceptual shape, not an implemented API:

```
SimulationResult
  Samples: ordered sequence of TrajectorySample
    TrajectorySample
      Time: double            // s
      State
        Position: Vector3d   // m
        Velocity: Vector3d   // m/s
  FinalState: FlightState
  FinalTime: double          // s
  CompletionReason: DurationReached
```

- Include the initial sample at exactly `t = 0` and the final sample at the requested `T`. With `T = 2` and `dt = 0.25`, there are **nine** samples.
- Samples are in increasing simulation-time order. Initially record every integration step, including a shorter final step if necessary; optional independent output sampling can be added later.
- `FinalState` and `FinalTime` agree with the last sample. Successful completion of #4 is `DurationReached`; #5 introduces `GroundContact` and may need other explicitly documented outcomes (e.g., a maximum-duration limit before contact).
- Simulation results contain **SI physical values**, not formatted feet/mph strings. Derived reports and user-friendly units are consumers of this data.
- Failures caused by invalid requests are reported as validation errors, **not** successful simulation results containing invalid values.

## Responsibilities and boundaries

| Responsibility | Initial role | Evolution |
|---|---|---|
| Physical state | Implemented immutable 3D position and velocity values | Add spin/orientation if physics requires them |
| Environment and physical properties | Gravity magnitude is an implemented scalar input | Add atmosphere/object parameters only when their models require them |
| Gravity dynamics | Evaluate time derivatives `dr/dt = v`, `dv/dt = (0,0,-g)` | Additional force/dynamics models without hardcoding gravity into integrator |
| Numerical integrator | Advance the provided differential equations using RK4 | Other methods and state dimensions when justified |
| Trajectory controller | Validate/coordinate input, stop at `T`, collect samples | Event detection and configurable sampling |
| Result analysis | Consume states to calculate useful measurements | #5 maximum height, contact time/location, etc. |

This table specifies **boundaries**, not a required one-to-one list of C# interfaces/classes. Only physical state and request inputs are implemented in Issue #3. Dynamics, integration, trajectory control/sampling, and result reporting remain conceptual responsibilities until their implementation stages. A simple future entry point such as `SimulationResult Simulate(FlightRequest request)` may be sufficient; its executable API will be established with Issue #4.

**Deliberate Issue #3 staging:** The original issue asks for separation "behind explicit interfaces." The approved interpretation is to document the boundaries now and defer executable simulation interfaces until they have concrete responsibilities and implementations in Issue #4. Concrete `TrajectorySample`, `SimulationResult`, and completion-reason types are also deferred. No unused interface hierarchy or placeholder simulation method is introduced. This interpretation must be reconciled with the issue wording before eventual closure; it is not literal implementation of that original task wording.

The future numerical integrator must work with a derivative evaluator rather than embedding a baseball-specific gravity formula throughout its stepping logic. It need not become a completely general solver for arbitrary future bodies on day one. Fixed-step RK4 is the intended initial baseline, not a permanent restriction on future methods. Avoid Unity, CLI, database, and rendering dependencies in `Lpb.Physics`; references continue from CLI/tests **to** Physics only.

## Validation policy

At construction, reject:

- `NaN`/positive or negative infinity in any vector component, gravity, duration, or timestep;
- negative gravity magnitude (zero is allowed for constant-velocity tests);
- `Duration <= 0` or `TimeStep <= 0`.

`Vector3d` validates each component with `double.IsFinite`. `FlightState` accepts only `Vector3d` values, whose public construction already enforces finiteness. `FlightRequest` validates gravity, duration, and timestep. Invalid values throw `ArgumentOutOfRangeException` identifying the relevant constructor parameter (`x`, `y`, `z`, `gravityMagnitude`, `duration`, or `timeStep`); no input is clamped, normalized, or substituted.

No rule prohibits negative coordinates or velocity components. Zero gravity and a timestep larger than duration are valid. Every finite component is accepted; gravity has no upper bound, and duration/timestep accept every finite strictly positive value. No arbitrary magnitude or resource restrictions are imposed by these constructors.

The structs have explicit validating constructors and get-only properties, with no public setters or `init` accessors. Zero-initialized `default(Vector3d)` and `new Vector3d()` are valid zero vectors; default/parameterless `FlightState` is valid at the origin with zero velocity. Record copying cannot introduce invalid values through public property assignment. `FlightRequest` has only its validating constructor; `default(FlightRequest)` is null rather than a constructed request. A future simulation entry point must reject null requests.

Finite inputs alone do not guarantee representable intermediate results or practical sample counts. Issue #4 must address numerical overflow, time-step progress, and excessive sample allocation explicitly; execution limits must be documented rather than silently truncating output. Added physical parameters will need model-specific validation when introduced (e.g., positive radius for a contacting sphere).

Issue #3's NUnit tests cover valid construction, component/state preservation, nonfinite values, invalid scalar ranges, parameter-specific errors, legitimate negative values, zero gravity, valid struct defaults, and representative small/large finite values. They do not execute trajectories.

## Ground-contact extension (Issue #5)

Add the properties `GroundElevation: double` (m) and `BallRadius: double` (m, positive) at appropriate environment/object-property boundaries **when #5 is implemented**; do not put them in the evolving state. Contact is a descending center crossing of `z = GroundElevation + BallRadius` with a suitably specified rule for initial contact/penetration. Record the **refined contact time and state** as the terminal sample; don't simply select the first below-plane RK4 sample. #5 does **not** implement bouncing, rolling, detailed impacts, or fair/foul baseball rules.

## Verification and change discipline

- Use the independent analytical fixtures in [`conventions.md`](conventions.md) as permanent gravity-only, aerodynamics-disabled regression tests during #4 and beyond. Their suggested `1e-9` absolute tolerance is provisional for the initial fixtures; actual numerical accuracy and convergence requirements are reviewed in #4.
- Keep RK4 intermediate states and scratch derivatives internal; the output contracts need only time-stamped physical states and completion information.
- Add tests for exact ending time when the step does not divide `T`; avoid results whose final time exceeds the requested duration.
- When new physics demands additional inputs/state variables, evolve these contracts deliberately with corresponding regression tests. **Extendability means controlled evolution, not a promise to never change public APIs.**
- Do not add CLI parsing, persistence, wind, drag, Magnus, seam geometry, or physics-based collision response as part of Issue #3's minimal implementation.

## Questions intentionally left for later work

- Concrete output types, completion-reason representation, and executable simulation interface design for Issue #4.
- Whether/how to make integration method or output sampling publicly configurable beyond the initial fixed RK4/every-step contract.
- Runtime resource limits for large `T / dt` requests and any future streaming/decimation behavior.
- Detailed ground-contact edge cases and accuracy rules for Issue #5.
- Whether rotational state is constant, evolving, or orientation-coupled in later aerodynamic models.
