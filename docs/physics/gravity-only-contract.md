# M1 Gravity-Only Simulator Contracts

> **Status:** Provisional M1 working specification — adopted 2026-11-01; Issue #4 implementation updated 2026-11-02\
> **Issues:** [#3 — Physical state objects and simulation interfaces](https://github.com/skolins3651/lets-play-baseball/issues/3), with executable integration in [#4](https://github.com/skolins3651/lets-play-baseball/issues/4) and contact/measurements deferred to [#5](https://github.com/skolins3651/lets-play-baseball/issues/5)\
> **Scientific specification:** [`conventions.md`](conventions.md)\
> **Architecture:** [`docs/architecture.md`](../architecture.md) summarizes the current design; root [`AGENTS.md`](../../AGENTS.md) governs contributions.

## Purpose

Define the implemented, engine-independent gravity-only simulator, inputs, outputs, and execution policy. Issue #3 established `Vector3d`, `FlightState`, and `FlightRequest`; Issue #4 adds executable RK4 integration and immutable trajectory results. The contracts support deliberate evolution for ground-contact events, physical object parameters, aerodynamics, and spin without embedding their behavior now.

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

`Vector3d` is unit-agnostic: its physical meaning and units come from the consuming property. `FlightState.Position` is ball-center position in metres; `Velocity` is linear velocity in metres/second. Both use the field-fixed coordinates in [`conventions.md`](conventions.md). No public vector arithmetic operators or unit-wrapper types are needed at this stage.

Standalone simulation starts at `t = 0 s`; time is elapsed simulation time, with no clock field in the initial state. All vector components are finite `double`s. Any coordinate (including negative `z`) and any finite velocity is acceptable **when ground contact is disabled**. Gravity and numerical settings are explicit scalar request properties; separate environment/settings wrapper types are unnecessary at this stage.

The scalar gravity magnitude is part of the initial **constant-gravity dynamics**, not a hardcoded assumption about every future force system or numerical integrator. `Vector3d` preserves double precision; `System.Numerics.Vector3` is single precision and must not silently substitute for it. There is no default gravity magnitude.

No baseball mass, radius, angular velocity, orientation, or air properties are required to run #4. Introduce parameters only when the relevant physical model needs them.

## Simulation entry point and output (Issue #4)

The public static entry point is `GravityOnlySimulator.Simulate(FlightRequest request)`. It returns a successful `SimulationResult` or throws. There is no public integrator selector, injection API, or output-sampling setting.

```text
SimulationResult
  Samples: IReadOnlyList<TrajectorySample>
    Time: double              // s
    State: FlightState        // position m, velocity m/s
  FinalState: FlightState
  FinalTime: double           // s
  CompletionReason: DurationReached
```

- `TrajectorySample` is a non-positional immutable `readonly record struct`, with constructor `TrajectorySample(double time, FlightState state)` and get-only properties. It rejects nonfinite or negative time with `ArgumentOutOfRangeException(nameof(time))`. Its default value is a valid zero-time, zero-state sample.
- `SimulationResult` is a sealed immutable class. Its internal constructor `SimulationResult(TrajectorySample[] samples, double requestedDuration)` validates non-null samples, finite positive duration, 2–100,001 samples, finite physical values, initial time exactly zero, strictly increasing times, and terminal time exactly equal to the requested duration. Invalid constructor arguments use standard argument exceptions. Internal visibility restricts construction to the assembly, so every assembly caller still undergoes validation.
- The constructor clones the supplied array before validating the snapshot. `Samples` exposes a private sealed `ReadOnlyCollection<TrajectorySample>` wrapper around that copy. Its legacy `ICollection.SyncRoot` returns the wrapper itself, preventing the standard wrapper's forwarding behavior from exposing the backing array. Later source-array mutations cannot change a result, and generic/non-generic collection mutation is rejected. No writable backing collection is exposed.
- Record every integration endpoint, including the initial sample and any shortened final step. With `T = 2` and `dt = 0.25`, there are **nine** samples.
- `FinalState` and `FinalTime` are derived directly from the terminal sample. The public `CompletionReason` enum contains only `DurationReached`, returned by every successful result. Issue #5 will deliberately extend completion and construction contracts for contact events; no event placeholders exist now.
- Results contain **SI physical values**, not formatted feet/mph strings. Derived reports and display units are consumers of this data.
- A returned result always completes the requested trajectory. Invalid invocation, resource rejection, or numerical failure throws without returning an ordinary successful partial result. A future trajectory legitimately ending at a physical event is distinct from execution failure.
- There is no public result constructor. Historical reconstruction or import may justify a separately designed construction API later; internal construction is not a permanent architectural restriction.

## Responsibilities and boundaries

| Responsibility | Implemented component | Evolution |
|---|---|---|
| Physical state | Public immutable `FlightState` | Add spin/orientation if physics requires them |
| Environment and properties | Explicit scalar gravity in `FlightRequest` | Add atmosphere/object parameters when required |
| Gravity dynamics | Internal `GravityDynamics` evaluates `dr/dt = v`, `dv/dt = (0,0,-g)` | Additional dynamics without embedding gravity in integration |
| Scratch derivatives | Internal readonly `FlightDerivative` holds six rates, checked by the integrator | Evolve with the required physical state |
| Numerical integration | Internal `Rk4Integrator` advances a supplied evaluator | Other methods when justified |
| Time control | Internal `SimulationTimeGrid` performs bounded preflight and supplies indexed endpoints | Event detection and configurable sampling |
| Trajectory control | Public static `GravityOnlySimulator` coordinates components and collects samples | Additional models when needed |
| Result reporting | Public immutable samples/result and completion enum | #5 measurements and contact completion |

The integrator's internal executable boundary is:

```csharp
FlightState Step(double startTime, double endTime, FlightState state,
    Func<double, FlightState, FlightDerivative> evaluate);
```

The evaluator receives simulation time and state; gravity currently ignores time. A delegate is sufficient: there is no unused interface hierarchy or general-purpose solver framework. RK4 scratch values stay internal. `InternalsVisibleTo` grants the existing test assembly access for focused numerical tests without reversing project references.

**Historical Issue #3 staging:** Its approved scope documented boundaries and deferred outputs/executable integration until their implementation in #4. Those deferrals are now fulfilled by the components above; the existing input contracts remain unchanged.

Fixed-step RK4 is the initial baseline, not a permanent restriction on future methods. `Lpb.Physics` has no Unity, CLI, database, rendering, or external-service dependencies; references continue from CLI/tests **to** Physics only.

## Indexed time and resource policy

Time starts at exactly zero. Define the nominal boundary `B(i) = (double)i * TimeStep`, using rounded double multiplication. The required step count `N` is the smallest positive integer with `B(N) >= Duration`.

The provisional maximum is **100,000 integration steps**, hence at most **100,001 samples**. This is a revisable execution policy, not an added `FlightRequest` restriction. Before allocating trajectory storage:

1. Compare `B(100000)` with duration; reject if it is smaller.
2. Otherwise binary-search `[1, 100000]` for the first reaching boundary, in at most 17 iterations.
3. Allocate exactly `N + 1` sample slots.

No division, `Ceiling`, large integer conversion, or excessive step-count scan is needed. Positive infinity from overflowing boundary multiplication serves only as a preflight upper-bound sentinel. Nonterminal execution endpoints must be finite; the terminal endpoint is assigned directly from the finite requested duration.

For step `i`, use `B(i)` when `i < N`, otherwise the exact requested `Duration`. The effective RK4 interval is `nextTime - currentTime`. Require finite nonnegative current time, a finite strictly later endpoint no greater than duration, and a finite positive interval. The loop executes exactly the bounded preflight count. A timestep greater than duration produces one shortened step.

This indexed definition governs integer-boundary rounding consistently in preflight and execution; it is not exact-real-number division or repeated addition. For example, `dt = 1e300`, `T = 100000d * dt` fits the limit even though `Ceiling(T / dt)` can exceed 100,000. The next representable duration above a finite `B(100000)` exceeds the limit. Ordinary rounding makes effective intervals differ slightly from nominal `dt`; these differences alone are not failures. No tolerance merges distinct requested endpoints, and no duration/timestep is silently changed.

## RK4 numerical safety and failures

With `h = endTime - startTime`, evaluate the classical four stages at the start, midpoint, midpoint, and exact endpoint. Intermediate states are `y + (h/2)k1`, `y + (h/2)k2`, and `y + h*k3`; complete the step as `y + (h/6)*(k1 + 2*k2 + 2*k3 + k4)`, where each `k` is a derivative. This follows the [classical RK4 rule](https://dlmf.nist.gov/3.7#v), without an analytical gravity shortcut.

Require finite positive half-step and sixth-step coefficients, and a representable midpoint strictly inside the interval. Check every derivative component, scaled rate, intermediate state, weighted product/sum, final increment, and completed state. Checks precede public validated-vector construction, keeping numerical failures distinct from input errors.

| Failure | Exception |
|---|---|
| Null simulation request | `ArgumentNullException`, parameter `request` |
| More than 100,000 indexed steps | `InvalidOperationException`, identifying limit and request settings |
| Unsafe time progression, unrepresentable required coefficient/midpoint, or nonfinite RK4 calculation | `ArithmeticException`, identifying step/time and relevant stage/component |

Use conventional double arithmetic and conservative rejection. Finite constructor inputs do not promise executable RK4: `T = dt = double.Epsilon` loses required coefficients; `T = Math.BitIncrement(1d)`, `dt = 1` leaves a final interval with no representable midpoint. Large finite states or weighted derivatives can overflow even when specialized arithmetic could recover a mathematical endpoint. No scaled-arithmetic framework, clamping, partial success, wall-clock timeout, or random stopping policy is introduced.

Ordinary finite state changes smaller than local state resolution are allowed; an unchanged rounded component alone is not failure. Useful relative accuracy is not guaranteed for every extreme finite request. Operation ordering and stopping depend on simulation inputs, while cross-platform physical comparisons use declared tolerances rather than a promise of bitwise identity. Time-contract comparisons are exact.

## Validation policy

At construction, reject:

- `NaN`/positive or negative infinity in any vector component, gravity, duration, or timestep;
- negative gravity magnitude (zero is allowed for constant-velocity tests);
- `Duration <= 0` or `TimeStep <= 0`.

`Vector3d` validates each component with `double.IsFinite`. `FlightState` accepts only `Vector3d` values, whose public construction already enforces finiteness. `FlightRequest` validates gravity, duration, and timestep. Invalid values throw `ArgumentOutOfRangeException` identifying the relevant constructor parameter (`x`, `y`, `z`, `gravityMagnitude`, `duration`, or `timeStep`); no input is clamped, normalized, or substituted.

No rule prohibits negative coordinates or velocity components. Zero gravity and a timestep larger than duration are valid. Every finite component is accepted; gravity has no upper bound, and duration/timestep accept every finite strictly positive value. No arbitrary magnitude or resource restrictions are imposed by these constructors.

The structs have explicit validating constructors and get-only properties, with no public setters or `init` accessors. Zero-initialized `default(Vector3d)` and `new Vector3d()` are valid zero vectors; default/parameterless `FlightState` is valid at the origin with zero velocity. Record copying cannot introduce invalid values through public property assignment. `FlightRequest` has only its validating constructor; `default(FlightRequest)` is null rather than a constructed request. The simulation entry point rejects null requests.

Finite inputs alone do not guarantee representable intermediate results or practical sample counts. The execution safeguards above address overflow, progression, and sample allocation without changing constructor acceptance. Added physical parameters will need model-specific validation when introduced (e.g., positive radius for a contacting sphere).

Issue #3's NUnit tests cover valid construction, component/state preservation, nonfinite values, invalid scalar ranges, parameter-specific errors, legitimate negative values, zero gravity, valid struct defaults, and representative small/large finite values. They do not execute trajectories.

## Ground-contact extension (Issue #5)

Add the properties `GroundElevation: double` (m) and `BallRadius: double` (m, positive) at appropriate environment/object-property boundaries **when #5 is implemented**; do not put them in the evolving state. Contact is a descending center crossing of `z = GroundElevation + BallRadius` with a suitably specified rule for initial contact/penetration. Record the **refined contact time and state** as the terminal sample; don't simply select the first below-plane RK4 sample. #5 does **not** implement bouncing, rolling, detailed impacts, or fair/foul baseball rules.

## Verification and change discipline

- Use the independent analytical fixtures in [`conventions.md`](conventions.md) as permanent gravity-only, aerodynamics-disabled regression tests during #4 and beyond. Their accepted `1e-9` absolute tolerance applies to the small analytical fixtures only, not all possible inputs. A test-only manufactured nonconstant derivative separately verifies RK4 stages and weighting at `1e-13`; broader convergence studies remain in #10.
- Keep RK4 intermediate states and scratch derivatives internal; the output contracts need only time-stamped physical states and completion information.
- Add tests for exact ending time when the step does not divide `T`; avoid results whose final time exceeds the requested duration.
- When new physics demands additional inputs/state variables, evolve these contracts deliberately with corresponding regression tests. **Extendability means controlled evolution, not a promise to never change public APIs.**
- Do not add CLI parsing, persistence, wind, drag, Magnus, seam geometry, or collision response as part of the gravity-only baseline.

## Questions intentionally left for later work

- Whether/how to make integration method or output sampling publicly configurable beyond the initial fixed RK4/every-step contract.
- Revision of the provisional resource policy and any future streaming/decimation behavior.
- Specialized arithmetic for extreme inputs, broader convergence studies (#10), and empirical validation.
- Detailed ground-contact edge cases and accuracy rules for Issue #5.
- Whether rotational state is constant, evolving, or orientation-coupled in later aerodynamic models.
