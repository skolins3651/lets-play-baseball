# M1 Gravity-Only Simulator Contracts

> **Status:** Provisional M1 working specification — adopted 2026-11-01; Issue #5 numerical certification updated 2026-11-04; approved Issue #22 occurrence/timing certificate implemented locally 2026-11-12, pending publication\
> **Issues:** [#3 — Physical state objects and simulation interfaces](https://github.com/skolins3651/lets-play-baseball/issues/3), with executable integration in [#4](https://github.com/skolins3651/lets-play-baseball/issues/4) and contact/measurements in [#5](https://github.com/skolins3651/lets-play-baseball/issues/5)\
> **Scientific specification:** [`conventions.md`](conventions.md)\
> **Architecture:** [`docs/architecture.md`](../architecture.md) summarizes the current design; root [`AGENTS.md`](../../AGENTS.md) governs contributions.

## Purpose

Define the implemented, engine-independent gravity-only simulator, inputs, outputs, and execution policy. Issue #3 established `Vector3d`, `FlightState`, and `FlightRequest`; Issue #4 adds executable RK4 integration and immutable trajectory results. The contracts support deliberate evolution for ground-contact events, physical object parameters, aerodynamics, and spin with first-contact termination and on-demand analysis now implemented in Issue #5. Issue #6 implements a separate [gravity-plus-drag model](drag-model.md) while preserving this gravity-only baseline; spin remains deferred.

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

## Simulation entry points and output

The unconstrained public static entry point is `GravityOnlySimulator.Simulate(FlightRequest request)`. Contact-aware execution uses `SimulateUntilGroundContact(FlightRequest request, GroundContactSettings contact)`. It returns a successful `SimulationResult` or throws. There is no public integrator selector, injection API, or output-sampling setting.

```text
SimulationResult
  Samples: IReadOnlyList<TrajectorySample>
    Time: double              // s
    State: FlightState        // position m, velocity m/s
  FinalState: FlightState
  FinalTime: double           // s
  CompletionReason: DurationReached | GroundContact
```

- `TrajectorySample` is a non-positional immutable `readonly record struct`, with constructor `TrajectorySample(double time, FlightState state)` and get-only properties. It rejects nonfinite or negative time with `ArgumentOutOfRangeException(nameof(time))`. Its default value is a valid zero-time, zero-state sample.
- `SimulationResult` is a sealed immutable class. Its retained provenance-free internal constructor `SimulationResult(TrajectorySample[] samples, double requestedDuration)` validates non-null samples, finite positive duration, 2–100,001 samples, finite physical values, initial time exactly zero, strictly increasing times, and terminal time exactly equal to the requested duration. Invalid constructor arguments use standard argument exceptions. Production construction additionally receives the immutable request, reason, and optional geometry. It permits a one-sample immediate contact or a multi-sample descending contact within the horizon; requires an exact terminal contact height, rejects penetration/earlier recorded contact, unknown reasons, and mismatched initial state. Duration completion still requires at least two samples and exact requested final time. Internal visibility restricts construction to the assembly, so every assembly caller still undergoes validation.
- The constructor clones the supplied array before validating the snapshot. `Samples` exposes a private sealed `ReadOnlyCollection<TrajectorySample>` wrapper around that copy. Its legacy `ICollection.SyncRoot` returns the wrapper itself, preventing the standard wrapper's forwarding behavior from exposing the backing array. Later source-array mutations cannot change a result, and generic/non-generic collection mutation is rejected. No writable backing collection is exposed.
- Record every ordinary integration endpoint until completion, replacing a contacting candidate with the refined terminal sample. Include the initial sample and any shortened final step. With `T = 2` and `dt = 0.25`, there are **nine** samples.
- `FinalState` and `FinalTime` are derived directly from the terminal sample. The enum preserves `DurationReached` and appends `GroundContact`. Ground contact at the horizon takes precedence over duration completion.
- Results contain **SI physical values**, not formatted feet/mph strings. Derived reports and display units are consumers of this data.
- A returned result successfully reaches the requested duration or first qualifying contact. Invalid invocation, resource rejection, or numerical failure throws without returning an ordinary successful partial result. Physical event termination is distinct from execution failure.
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
| Trajectory control | Shared internal `FlightExecution`; `GravityOnlySimulator` supplies gravity dynamics/event policy | Shared with the separate drag model |
| Result reporting | Public immutable samples/result and completion enum | Deliberate later contract evolution |
| Contact refinement | Internal `GravityOnlyRefinement` | Constant-gravity bracket assumptions must be revisited for new models |
| Optional analysis | Public `TrajectoryMeasurements` and `GravityOnlyTrajectory` | Explicit model context rather than a general registry |

The integrator's internal executable boundary is:

```csharp
FlightState Step(double startTime, double endTime, FlightState state,
    Func<double, FlightState, FlightDerivative> evaluate);
```

The evaluator receives simulation time and state; gravity currently ignores time. A delegate is sufficient: there is no unused interface hierarchy or general-purpose solver framework. RK4 scratch values stay internal. `InternalsVisibleTo` grants the existing test assembly access for focused numerical tests without reversing project references.

**Historical Issue #3 staging:** Its approved scope documented boundaries and deferred outputs/executable integration until their implementation in #4. Those deferrals are now fulfilled by the components above; the existing input contracts remain unchanged.

Fixed-step RK4 is the initial baseline, not a permanent restriction on future methods. `Lpb.Physics` has no Unity, CLI, database, rendering, or external-service dependencies; references continue from CLI/tests **to** Physics only.

### Shared infrastructure after Issue #6

`FlightExecution` owns indexed progression, ordinary work/sample budgets, allocation, immediate contact, completion, exception context, and successful result construction. Its advancement/event delegates preserve specialized physical policies. `TrajectoryEvaluation` owns timestamp lookup and exact recorded-maximum comparisons. `NumericalEventInterval` contains the unchanged interior-time and finite-state checks. `Rk4Integrator`, `SimulationTimeGrid`, and `GravityOnlyRefinement` remain unchanged. Issue #22 adds a gravity-specific postcondition on the refiner's proposed contact; it preserves the existing local uncertainty certificates. Drag's enabled-drag consistency policy and apex witness do not apply to gravity-only execution.

## Unconstrained indexed time and resource policy

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
| Free-flight horizon above 100,000 indexed steps, or contact-aware ordinary-budget exhaustion | `InvalidOperationException`, identifying limit and request settings |
| Unsafe time progression, unrepresentable required coefficient/midpoint, or nonfinite RK4 calculation | `ArithmeticException`, identifying step/time and relevant stage/component |

Use conventional double arithmetic and conservative rejection. Finite constructor inputs do not promise executable RK4: `T = dt = double.Epsilon` loses required coefficients; `T = Math.BitIncrement(1d)`, `dt = 1` leaves a final interval with no representable midpoint. Large finite states or weighted derivatives can overflow even when specialized arithmetic could recover a mathematical endpoint. No scaled-arithmetic framework, general clamping, partial success, wall-clock timeout, or random stopping policy is introduced. Only successfully localized contact permits the bounded terminal-Z projection specified below.

Ordinary finite state changes smaller than local state resolution are allowed; an unchanged rounded component alone is not failure. Useful relative accuracy is not guaranteed for every extreme finite request. Operation ordering and stopping depend on simulation inputs, while cross-platform physical comparisons use declared tolerances rather than a promise of bitwise identity. Time-contract comparisons are exact.

## Validation policy

At construction, reject:

- `NaN`/positive or negative infinity in any vector component, gravity, duration, or timestep;
- negative gravity magnitude (zero is allowed for constant-velocity tests);
- `Duration <= 0` or `TimeStep <= 0`.

`Vector3d` validates each component with `double.IsFinite`. `FlightState` accepts only `Vector3d` values, whose public construction already enforces finiteness. `FlightRequest` validates gravity, duration, and timestep. Invalid values throw `ArgumentOutOfRangeException` identifying the relevant constructor parameter (`x`, `y`, `z`, `gravityMagnitude`, `duration`, or `timeStep`); no input is clamped, normalized, or substituted.

No rule prohibits negative coordinates or velocity components. Zero gravity and a timestep larger than duration are valid. Every finite component is accepted; gravity has no upper bound, and duration/timestep accept every finite strictly positive value. No arbitrary magnitude or resource restrictions are imposed by these constructors.

The structs have explicit validating constructors and get-only properties, with no public setters or `init` accessors. Zero-initialized `default(Vector3d)` and `new Vector3d()` are valid zero vectors; default/parameterless `FlightState` is valid at the origin with zero velocity. Record copying cannot introduce invalid values through public property assignment. `FlightRequest` has only its validating constructor; `default(FlightRequest)` is null rather than a constructed request. The simulation entry point rejects null requests.

Finite inputs alone do not guarantee representable intermediate results or practical sample counts. The execution safeguards above address overflow, progression, and sample allocation without changing constructor acceptance. Contact geometry has the additional model-specific validation below.

Issue #3's NUnit tests cover valid construction, component/state preservation, nonfinite values, invalid scalar ranges, parameter-specific errors, legitimate negative values, zero gravity, valid struct defaults, and representative small/large finite values. They do not execute trajectories.

## Ground-contact execution (Issue #5)

`GroundContactSettings(double groundElevation, double ballRadius)` is a public sealed immutable class with get-only properties. Both values must be finite and radius strictly positive (`ArgumentOutOfRangeException` naming the input). The rounded sum `H = groundElevation + ballRadius` must be finite and strictly greater than ground elevation. Overflow or absorbed radius throws `ArgumentException` naming `ballRadius`. Geometry is separate from evolving `FlightState`; no mass or spin is added.

Initial center Z is compared exactly with H: below rejects unsupported penetration (`ArgumentException`, `request`); equality with vertical velocity <= 0 gives immediate `GroundContact`; equality with positive velocity allows launch. Signed zero follows ordinary double equality. There is no geometric tolerance.

### Detection and localization

Constant nonnegative gravity makes vertical velocity nonincreasing and the path concave downward. An on/below-plane descending endpoint supplies a numerical contact candidate. A later endpoint exactly on the represented plane needs no further RK4 evaluation, but must pass the independent occurrence/timing certificate below. For launch and return within one step, exclude the initial root; if the return endpoint is below the plane, numerically find the apex and require a resolvable above-plane state before contact refinement. Inconsistent candidates fail. These assumptions are gravity-specific, not guarantees about future aerodynamic models.

Bisection maintains an above-plane lower contact endpoint and on/below-plane descending upper endpoint. Apex brackets have positive/nonpositive vertical velocity. Trial states advance through unchanged RK4 from the **original ordinary interval start**, never from successively narrowed endpoints. Compare signs without multiplying residuals. Integration and event localization remain numerical; constant-gravity expressions are additionally used only as the bounded event-evidence postcondition below, never to generate or replace states or solve for a contact timestamp.

For nonexact contact, all of the following must hold simultaneously:

| Localization check | Limit |
| --- | --- |
| Time bracket width | 1e-10 s |
| Unprojected terminal height residual | 1e-9 m |
| Position uncertainty, each component | 1e-9 m |
| Velocity uncertainty, each component | 1e-9 m/s |
| Trial evaluations per root search | 128 |

Position uncertainty includes **both mathematical motion and floating-point state-evaluation rounding**. A velocity-times-width bound alone does not cover jumps in rounded coordinates, even when its multiplication is rounded outward. Comparing two rounded endpoint positions is also insufficient to bound every interior RK4 evaluation.

For each changing coordinate, keep the original ordinary interval anchor `(p0, v0, t0)`. With outward-rounded nonnegative arithmetic, let `D = upperTime - t0`, `W = upperTime - lowerTime`, `a` be gravity magnitude for Z and zero for X/Y, and define:

```text
B = abs(v0) + a*D
S = abs(p0) + B*D
E = 64*ulp(S) + 256*double.Epsilon*double.MaxValue
position uncertainty = B*W + 2*E
```

Here `ulp(S)` is the spacing toward increasing magnitude, itself rounded outward. Reject nonexact localization if a changing anchor coordinate's spacing already exceeds `1e-9 m`. A coordinate with zero velocity and zero acceleration is exactly unchanged by this gravity RK4 expression and has zero uncertainty. Otherwise each computed bound above must be finite and the complete position uncertainty must be <= `1e-9 m`. Excess uncertainty continues bisection, possibly reaching a verified exact root; if refinement cannot certify it, stagnation or the existing 128-evaluation cap throws. The coarse-coordinate guard throws directly with a position-precision diagnostic.

### Why the state-evaluation allowance is conservative

This allowance is specific to the **existing classical RK4 operation order and constant gravity**, evaluated from one fixed anchor. Gravity reads velocities, not stage positions, and ignores stage timestamps. Thus scratch-position rounding and midpoint rounding cannot feed back into the derivatives. The absolute contributions to a completed coordinate are bounded by `abs(p0) + abs(v0)*D + a*D*D <= S`, including cancellation. Expanding the scalar expression gives fewer than 32 relative-rounding factors along any contribution (interval subtraction, half/sixth coefficients, velocity-stage arithmetic, weighted sums, increment, and final addition). With binary64 unit roundoff `u = 2^-53`, `gamma(32) = 32*u/(1-32*u) < 64*u`, and `ulp(S) >= u*S`. Therefore `64*ulp(S)` conservatively bounds normal evaluation rounding relative to the exact-arithmetic gravity RK4 path. This uses the [standard floating-point rounding model](https://nhigham.com/wp-content/uploads/2021/04/high21m.pdf); the RK4-specific envelope and operation count are LPB's derivation, not a claim from that reference.

Gradual underflow adds absolute errors not covered by relative factors. The separate `256*double.Epsilon*double.MaxValue` term reserves 32 error sites with a conservative amplification envelope of eight times the largest finite coefficient/rate; constant gravity and the finite `B`, `D`, and checked RK4 arithmetic bound the propagated products. It is about `2.274e-13 m` per evaluation. `double.Epsilon` here means the smallest positive subnormal, not unit roundoff. All computations of these allowances are outward-rounded. Bound overflow or a positive bound product underflowing to zero throws explicitly.

The envelope applies uniformly to **every** safely evaluated timestamp in the bracket, not just its endpoints. Two such evaluations can differ by at most mathematical motion `B*W` plus their two rounding allowances `2*E`; this also covers an exact evaluated root inside a nonexact bracket. Neither small bracket width nor endpoint agreement can bypass this additional budget. The reviewed `X = 2^30` reproduction is rejected because the moving coordinate's spacing is already `2^-22 m`, above the positional limit. Large intermediate motion may also prevent certification despite small final coordinates. The allowance intentionally rejects some finite configurations that might be computable more accurately with specialized arithmetic.

### Contact velocity uncertainty

The velocity certificate is separate from the position certificate. `gravity*W` bounds physical velocity variation, but cannot alone bound rounded RK4 velocity values. For each component let `a` be its acceleration magnitude (zero for X/Y, gravity for Z), `v0` the **original ordinary interval anchor velocity**, and use the same outward-rounded elapsed bound D and bracket width W as above:

```text
C = abs(v0) + a*D
Ev = 16*ulp(C) + 16*double.Epsilon*double.MaxValue
velocity uncertainty = a*W + 2*Ev
```

If `a == 0`, all four acceleration rates, their weighted sum, and the final increment are exactly zero: adding that increment leaves the finite component unchanged. Its certified uncertainty is **exactly zero**, even for large or subnormal velocities. This applies to both horizontal components and to all components in zero gravity, without evaluating unnecessary bounds.

For an accelerated component, reject nonexact contact directly if the anchor velocity's spacing exceeds `1e-9 m/s`. Otherwise the complete finite combined uncertainty above must be <= `1e-9 m/s`. All nonnegative products, sums, spacings, and allowances are rounded outward using the existing checked helpers. Invalid/nonfinite inputs, overflowing bounds, positive products lost to zero through underflow, and inability to represent a conservative bound throw `ArithmeticException`. Representable subnormal values retain an explicit absolute rounding allowance. An excessive finite bound continues bounded refinement; without a qualifying exact root, failure to certify eventually throws at stagnation or the existing cap. The high-speed review reproduction (`vz0 = -1e9`, `g = 59.604644775390625`, duration/timestep `2e-9`) now fails with integration-step and velocity-precision context.

### Derivation of the velocity-evaluation allowance

Under constant gravity, every RK4 acceleration derivative is the same exact input `q` (zero or `-g`); rounded intermediate velocities and stage timestamps do **not** feed back into completed velocity. The actual expression is:

```text
h = fl(t - t0)
c = fl(h / 6)
s1 = fl(q + 2*q)
s2 = fl(s1 + 2*q)
s3 = fl(s2 + q)
vhat(t) = fl(v0 + fl(c*s3))
```

Doubling q is exact whenever finite, including subnormals; overflow is already rejected by RK4. The remaining dependency has seven rounding sites: interval subtraction, division, three weighted additions, increment multiplication, and final addition. The exact-arithmetic contribution magnitudes are bounded by `abs(v0) + a*D <= C`, including cancellation at the final addition. Using eight factors conservatively, `gamma(8) = 8*u/(1-8*u) < 16*u`, with `u = 2^-53` and `ulp(C) >= u*C`; thus `16*ulp(C)` covers normal evaluation rounding. This is a bound derived from the numerical expression, not an analytical trajectory evaluator.

The additional `16*double.Epsilon*double.MaxValue` term covers absolute gradual-underflow errors at up to eight sites with a conservative propagation envelope of twice the largest finite coefficient/rate. The checked weighted sum and elapsed coefficient are finite, and `a*D` is bounded finitely by construction. These facts bound propagated subtraction/coefficient, weighted-sum, increment, and final-addition errors; there is no stage-state feedback. The term is approximately `1.421e-14 m/s` per evaluation and is outward-rounded. It is intentionally different from the more conservative position allowance.

For every safely evaluated timestamp in the bracket, its interval from the fixed anchor is <= D and its evaluation error is <= Ev. For **any two** such timestamps, physical velocity changes by at most `a*W`; adding the two rounding errors gives `a*W + 2*Ev`. This includes the terminal candidate and arbitrary interior evaluations, not merely the two endpoints. It also remains valid with a nonzero anchor timestamp. The implementation checks all three velocity components; unchanged axes use the exact-zero rule rather than an unnecessary error floor.

Time, contact-height residual, every position and velocity component, finite state, and valid descending contact evidence remain conjunctive. These are local bounds relative to one numerical anchor; they do not bound accumulated integration error or establish empirical baseball accuracy. The rounding derivations must be revisited if dynamics or RK4 operation order change.

A verified exact evaluated contact root identifies one numerical timestamp after direction, horizon, finite-state, and bracket/launch checks. Rounded equality alone does not establish physical occurrence or timely first contact; every proposed contact must also pass the following postcondition. Mere proximity never establishes contact. Nonexact success selects the evaluated upper endpoint and changes only its center Z to H, with residual <= 1e-9 m and every uncertainty bound satisfied. Retain its time, X/Y, and velocity. This event projection is neither collision response nor general trajectory clamping.

Check convergence before requesting another trial. A bracket with no interior timestamp may use an already evaluated state only if every bound passes. Otherwise timestamp stagnation, unsafe RK4 coefficients/midpoints/arithmetic, invalid brackets, unresolved airborne excursions, or 128-evaluation exhaustion throw contextual `ArithmeticException`. No partial successful trajectory is returned.

### Independent occurrence and first-contact timing (Issue #22)

`GravityContactEvidence` validates the unchanged refiner's proposed terminal time before `FlightExecution` records or accepts contact. It checks both the original ordinary anchor and the immutable release state independently, including ordinary exact roots, roots encountered during bisection, exact upward-launch returns, and contact at the horizon. Disabled drag uses the same check while retaining its drag identity and settings. Immediate stationary/downward contact remains the known initial state and bypasses integration and this postcondition.

For each origin `(to, zo, vo)`, use the exact mathematical values of the supplied binary64 inputs and the existing rounded contact height H:

```text
d(s) = s - to
C    = zo - H
Q(s) = C + vo*d(s) - g*d(s)*d(s)/2
W(s) = vo - g*d(s)
```

The origin must be airborne, or be the initial time-zero touching upward launch. At proposed time t require `upper(Q(t)) <= 0` and `upper(W(t)) < 0` for **both** origins. A strictly positive clearance lower bound disproves contact; an enclosure that cannot establish occurrence/direction rejects as unresolved. A maximum-travel reachability bound is merely necessary and is not used as sufficient contact evidence.

Then, independently for each origin, require either a certified clearance enclosure **exactly `[0,0]`**, or a certified first-contact window `[a,t]` of actual width no greater than the existing represented `T = 1e-10 s`, with `lower(Q(a)) >= 0`. The singleton proves the mathematical clearance is zero, not just that an ordinary rounded expression or coordinate equals zero. Descending direction is still required. If both origins certify singleton zeros, no earlier timestamp is needed.

Construct the common earlier timestamp as `a = max(anchor.Time, 0, SubtractBounds(t,T).Upper)`. The subtraction upper endpoint is the least binary64 value not below the exact real t-T. Thus a >= t-T and a >= anchor.Time. Require a < t and independently certify the width with `SubtractBounds(t,a)`: positive lower bound and upper bound <= T. Never decrement a to manufacture a window or widen T. These proof timestamps are not samples or RK4 trial requests, so no representable RK4 midpoint between a and t is needed.

From an airborne origin, constant nonnegative gravity admits at most one future qualifying descending crossing. An upward touching launch with positive gravity has the initial root plus one positive descending return; t > to and certified negative W(t) exclude the initial root. With zero gravity it cannot return. Therefore the terminal signs prove the qualifying root r <= t; singleton Q proves r=t, otherwise nonnegative Q(a) proves r>=a. The combined certificate establishes `0 <= t-r <= T` for each origin, including the authoritative release trajectory. Crossing-by-time alone does not establish this timing condition.

The anchor and release are distinct exact input states; no claim is made that the rounded anchor lies on the exact release trajectory. Their roots must fit the same absolute window (or equal t), rather than silently combining their clearances and velocities. Release evidence prevents prior coordinate rounding from erasing original clearance. The common cutoff protects first-contact ordering relative to the last ordinary anchor. These event proofs do not certify global six-component integration error or empirical baseball accuracy.

#### Scalar enclosures and compensated cancellation

Assume binary64 round-to-nearest, ties-to-even, gradual underflow, and explicit single-rounding FMA. Check every required intermediate and outward endpoint for finiteness. All arithmetic below is private to the gravity event helper; no public precision subsystem or analytical state evaluator is introduced.

For addition/subtraction use TwoSum: `s=fl(a+b)`, `z=fl(s-a)`, `e=fl(fl(a-fl(s-z))+fl(b-z))`. Subject to finite required operations, `a+b=s+e` exactly, including gradual underflow. If e=0 return `[s,s]`; if e>0 return `[s,BitIncrement(s)]`; if e<0 return `[BitDecrement(s),s]`. These are tight directed bounds; subtraction negates the second finite operand exactly. See [Ogita, Rump, and Oishi, Theorem 3.4](https://www.tuhh.de/ti3/paper/rump/OgRuOi05.pdf).

For multiplication let `p=fl(a*b)` and `e=FMA(a,b,-p)`. [Explicit .NET FMA](https://learn.microsoft.com/en-us/dotnet/api/system.math.fusedmultiplyadd?view=net-10.0) rounds the exact ternary expression once. A nonzero residual has the exact product error's sign and permits the corresponding directed endpoint. A zero residual can have underflowed and does not generally prove exactness. Without a proof, enclose the product with both adjacent values, even when p is zero; unrepresentable outward endpoints reject.

The sufficient safe-product threshold is `abs(p) >= 2^-968`. Two binary64 significands yield at most 106 product bits. At or above that rounded magnitude, the exact product's normalized exponent is at least -969, so its least-significant-bit grid exponent is at least `-969-105 = -1074`. The rounding residual has at most 53 significant bits, lies on the minimum-subnormal grid, and is finite and exactly representable. FMA therefore returns that exact residual in this range; zero then proves exactness. This is a derivation, not an empirical threshold.

Multiplication by zero or +/-1 is structurally exact for finite operands. Multiplication by +/-1/2 is exact only when finite doubling of the rounded product reproduces the corresponding signed operand. A failed round trip supplies no exactness proof. Signed zero denotes the same mathematical zero; addition/subtraction underflow is covered by TwoSum. A product or FMA residual lost below the subnormal range is never silently promoted to exact zero.

For one scalar affine expression `a*b+c`, retain a certified exact product residual ep and TwoSum addition residual es: `a*b=p+ep`, `p+c=s+es`, hence `a*b+c=s+(ep+es)`. Enclose ep+es using TwoSum, then add that correction interval to `[s,s]`. This preserves proven cancellation. If ep cannot be certified exact, fall back to ordinary conservative product-interval plus addend; an approximate residual is never used as an exact correction. All required intermediate range checks remain in force, even when an alternative arithmetic method could recover a finite answer.

Compose interval addition by directed endpoint sums, multiplication by interval corners, and affine expressions by their box corners; multilinearity gives containing extrema. Negation and comparisons/min/max of finite endpoints are exact. Evaluate `D=SubtractBounds(s,to)`, `C=SubtractBounds(zo,H)`, `halfD=D/2`, `rate=Affine([-g,-g],halfD,[vo,vo])`, `Q=Affine(D,rate,C)`, and `W=Affine([-g,-g],D,[vo,vo])`. Elapsed-time bounds contain the actual mathematical timestamp difference, not just the integrator's rounded interval. Repeated appearances of d may conservatively lose dependence, but can never falsely narrow a bound.

The compensated affine bound is needed for legitimate exact horizons. With u=2^-52, `t=zo=1-u`, `vo=-u*u`, `g=2*(1+u)`, H=0, the exact rate `vo-g*t/2=-1` and Q(t)=0. Independent intervalization loses the cancelling product/addition residuals; retaining them certifies the exact root. Neither this identity nor FMA introduces an integration or root-solving shortcut.

#### Rejections, compatibility, and limits

At H=1 and H=2^20, one-ULP clearance with speed .75 ULP/s and zero gravity cannot reach the plane in one second, although RK4 rounds onto it. These cases throw contextual `ArithmeticException` in gravity-only and both disabled-drag modes. Returning duration would violate `SimulationResult` because the required recorded endpoint is on-plane and descending. Do not fabricate positive clearance, move H, discard a sample, or swallow rejection as no contact.

Crossing-by-time is also insufficient: with clearance u and speed 1.25u/s, true contact is .8 s while the old equality shortcut selects 1 s. Terminal Q/W signs pass, but the earlier-window evidence rejects. Conversely a root can be within T yet before the rounded-up cutoff. For example t=1, C=16.9999999983, vo=-17 has delay about 9.999990378245831e-11 s but no containing represented earlier endpoint within T. Report inability to certify the selected window, not a proved delay exceeding T.

At t=2^19 a 2^-34 s earlier gap fits; immediately above that timestamp the preceding gap is 2^-33 s and does not. Nonexact evidence rejects when no earlier timestamp fits. A mathematically exact root can still succeed without a window. Unresolved underflow/cancellation, bound overflow, or unrepresentable endpoints reject deliberately; not every finite valid request is executable.

Required failures retain origin, operation, and candidate-time diagnostics plus the existing integration-step/time wrapper. The refiner's local position/velocity certificates and all state/time safety checks remain unchanged and conjunctive. No returned sample is adjusted by this helper. It adds no RK4 calls, no adaptive retry, and at most four origin/timestamp evaluations with bounded scalar corner arithmetic. Provenance identifies `classical-rk4/indexed-v1;gravity-events-v2`, including disabled drag; enabled-drag policies are unchanged.

NUnit verifies the actual C# scalar/composite enclosures against verification-only exact binary-rational arithmetic, including deterministic and seeded adversarial ranges. Public regressions retain the 42 test-first cases, genuine exact/nonexact contacts, launch/horizon semantics, geometry, model provenance, ownership, budgets, and optional analysis contracts. Numerical tests support the implementation; they do not replace the arithmetic derivation.

### Actual-work budget

Contact-aware execution does not reject solely because the requested whole horizon exceeds 100,000 steps. Permit at most 100,000 ordinary RK4 steps and 100,001 stored samples. Use `B(i) = (double)i * TimeStep`; choose exact `Duration` only when `B(i) >= Duration`, otherwise execute B(i). Reaching the work cap must never jump to duration. Contact during step 100,000 succeeds; without contact or duration completion, throw `InvalidOperationException` identifying the limit and request settings.

Immediate contact bypasses preflight and ordinary integration. Sample capacity starts at at most 256, doubles with a cap of 100,001, then trims before result construction; defensive result cloning remains. Refinement samples are not recorded. Contact refinement permits 128 additional RK4 evaluations, with up to 128 separately bounded apex evaluations for same-step launch/return. Finite-time, sentinel-overflow, and RK4 checks are unchanged. A required ordinary candidate can still fail before an earlier contact is safely bracketed.

## Optional measurements and continuous evaluation

`TrajectoryMeasurements.GetDisplacement(result)` returns final minus initial position. `GetHorizontalDisplacement(result)` returns that vector with Z zero, not scalar distance or traveled path length. Required subtractions are checked for finiteness before vector construction; failure is `ArithmeticException`. Elapsed time is `result.FinalTime`. `GetGroundContact(result)` returns the terminal `TrajectorySample?` only for `GroundContact`, otherwise null. Its time is completed flight/contact time; its position is the **ball center**, not the ground-surface point at elevation zg.

Both production gravity simulation APIs construct results retaining private immutable provenance: model identity, source request, applicable contact geometry, and fixed numerical-policy identity, including immediate contact and contact-aware duration completion. `SimulationResult.ModelKind` is `GravityOnly` for these APIs; the provenance-free constructor reports null. No dynamics object, general model, mutable metadata, or analysis cache is exposed. The compatibility two-argument internal result constructor has no provenance: sample-only measurements remain valid, but numerical context-dependent analysis rejects it.

`GravityOnlyTrajectory.Evaluate(result, request, time)` and `GetMaximumHeight(result, request)` require explicit matching gravity-only model identity and context. They reject drag-model results even when density/coefficient disables drag and the common request matches. Compare all initial-state components, gravity, duration, and timestep exactly; an equivalent independently constructed request is accepted. Missing provenance throws `ArgumentException` naming `result`; mismatches name `request`; null inputs throw `ArgumentNullException`.

Evaluation permits finite time in `[0, result.FinalTime]` only (`ArgumentOutOfRangeException`, `time`). Binary-search stored timestamps: an exact match returns the recorded state verbatim, including a projected terminal contact. Otherwise perform at most one RK4 advancement from the preceding sample. Do not linearly interpolate, apply projection to interior states, or evaluate beyond actual termination. Very near contact, the unprojected interior path can have a small signed residual; the terminal adjustment is bounded as above.

Maximum height returns a `TrajectorySample` identifying greatest modeled ball-center Z and its earliest time over the **actual** interval. Initially descending paths and constant-height paths select the initial sample; ascending truncated paths use their terminal height and prefer the earliest stored occurrence when it is tied. Locate an interior apex by a binary search of velocity endpoints and bounded bisection of vertical velocity, through the same numerical machinery. Require time width <= 1e-10 s, vertical height uncertainty <= 1e-9 m, gravity-times-width <= 1e-9 m/s, and selected vertical velocity residual <= 1e-9 m/s; a verified exact velocity zero needs no further narrowing. For nonexact apex localization, certify height uncertainty with the **existing position allowance** evaluated from the original interval anchor; speed-times-width alone does not bound rounded height. Choose the nonpositive endpoint, then compare the numerical candidate with all stored samples using exact height comparisons. Equal evaluated heights prefer the earlier timestamp, including the first stored occurrence of an ascending rounding plateau. No approximate-equality tolerance merges different heights. Each query has its own 128-evaluation cap. No future apex or landing data is invented for a truncated flight.

### Focused certification audit

The apex's `abs(vhat.Z) <= 1e-9 m/s` residual is a direct check of the selected **evaluated** velocity. It is not a claim about pairwise velocity variation throughout a contact bracket. Its additional gravity-times-width condition limits physical time-localization variation. Those apex checks remain unchanged; applying the contact velocity certificate merely for symmetry would change their meaning.

The audit did demonstrate a separate nonexact **apex-height** failure of the same rounding-certification kind: from `z0 = 2^30`, `vz0 = 0.007603029409634734`, gravity `242.45617275970022`, and a single interval of `7.839591505441008e-5 s`, the old accepted bracket `[3.135836415265976e-5, 3.13583664890401e-5]` contained evaluated heights differing by `2^-22 m` (for example at `3.135836623030339e-5 s`). Its tiny mathematical height-change estimate missed that jump. Nonexact apex height now reuses the approved position certificate, unchanged in its formula, and this coarse-coordinate case rejects explicitly. No apex velocity-residual tolerance was altered.

Exact evaluated contact/apex roots identify one numerical timestamp, with finite-state and event/bracket conditions still required. Contact additionally requires the Issue #22 occurrence/timing postcondition above; historical guidance treating rounded plane equality as independently sufficient is superseded. Apex refinement is unchanged. These fast paths do not promise a smooth neighboring rounded trajectory or a uniform error bound over a nonzero neighborhood. Immediate contact returns the known initial point. Terminal-Z projection remains restricted to nonexact contact after **all** position and velocity certificates pass; no successful contact is published unless the independent postcondition also passes. Projection never changes velocity or conceals failed certification. Height residuals compare the selected represented state against the represented plane with outward-rounded subtraction. Time endpoints remain the exact recorded doubles.

Measurements run only when requested. Unsafe numerical analysis throws rather than weakening accuracy or concealing failures.

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
- Whether rotational state is constant, evolving, or orientation-coupled in later aerodynamic models.
