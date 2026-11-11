# M1 Quadratic Drag: Physics, Numerical Contracts, and Verification

> **Status:** Issue #6 independently reviewed and merged through [PR #21](https://github.com/skolins3651/lets-play-baseball/pull/21) — November 10, 2026.\
> **Architecture:** [overview](../architecture.md). **Baseline:** [conventions](conventions.md), [gravity-only guarantees](gravity-only-contract.md).  
> **Scope:** Complete deterministic gravity-plus-drag flight in stationary air. No empirical calibration is claimed.

## Physical model and explicit inputs

In the existing SI field frame (+Z upward), implement

\[
\dot{\mathbf r}=\mathbf v,\qquad
\dot{\mathbf v}=-g\hat{\mathbf z}-k\|\mathbf v\|\mathbf v,\qquad
k=\frac{\rho C_D\pi R^2}{2m}.
\]

Gravity, air density, coefficient, mass, and spherical radius remain constant during a flight. Air is stationary, so field velocity is also air-relative velocity. Reference area is **frontal area πR²**, not sphere surface area. No lift, Magnus force, spin/orientation evolution, wind, seam effects, variable atmosphere/coefficient, bounce, rolling, or friction is introduced.

`FlightRequest(initialState, gravityMagnitude, duration, timeStep)` retains its constructor and properties. It supplies initial center position/velocity, finite nonnegative gravity, and finite positive duration/timestep. Unconstrained motion may continue below ground. A timestep larger than duration remains a valid input, although numerical execution may reject it.

`AerodynamicSettings(ballMass, ballRadius, airDensity, dragCoefficient)` is sealed and immutable, with get-only properties:

| Input | Units | Constructor acceptance |
|---|---|---|
| `BallMass` | kg | finite, strictly positive |
| `BallRadius` | m | finite, strictly positive |
| `AirDensity` | kg/m³ | finite, nonnegative |
| `DragCoefficient` | dimensionless | finite, nonnegative |

Invalid scalar inputs use parameter-specific `ArgumentOutOfRangeException`. No constructor defaults or substitutions exist. Finite valid inputs do not guarantee executable numerical arithmetic.

Illustrative baseball-scale values are `m=.145`, `R=.0366`, `rho=1.225`, `Cd=.35`, `g=9.80665`; they give `k≈.006221830544308885 m^-1`. Mass/radius are regulation-scale examples, density is a standard-atmosphere-scale illustration, and coefficient is a configurable assumption. None is an empirically calibrated LPB default.

## Public API, geometry, ownership, and provenance

```csharp
GravityDragSimulator.Simulate(request, aerodynamics);
GravityDragSimulator.SimulateUntilGroundContact(request, aerodynamics, groundElevation);
GravityDragTrajectory.Evaluate(result, request, aerodynamics, time);
GravityDragTrajectory.GetMaximumHeight(result, request, aerodynamics);
```

Contact accepts ground elevation only. Its single authoritative radius is `aerodynamics.BallRadius`, used for both drag and `H = groundElevation + radius`. The rounded H must be finite and strictly above ground elevation, matching `GroundContactSettings`. Nonfinite elevation throws `ArgumentOutOfRangeException`; an unsafe combination throws `ArgumentException` naming `groundElevation`.

Exact initial center comparisons apply without geometric tolerance: penetration rejects (`ArgumentException`, `request`); touching with vertical velocity <=0 returns immediate `GroundContact`; touching upward launches. Immediate contact returns the original state at time zero in one sample, bypassing all integration and preflight. It still retains complete model provenance.

`SimulationResult` preserves defensive cloning, finite/ordered sample validation, read-only ownership, and the protected legacy `ICollection.SyncRoot`. No public constructor or mutable metadata is added. Completion is `DurationReached` or `GroundContact`; contact at the horizon takes precedence when numerical contact evidence exists. `FinalTime` and `FinalState` come from the terminal sample.

Public `FlightModelKind? ModelKind` is `GravityOnly` or `GravityAndDrag` for production results. Only the retained internal provenance-free constructor reports null. Private immutable provenance retains the request, model identity, aerodynamic parameters where applicable, contact geometry even on duration completion, and fixed numerical-policy identity:

- `classical-rk4/indexed-v1;gravity-events-v1` for gravity computation, including disabled drag.
- `classical-rk4/indexed-v1;drag-full-state-v1;drag-events-v1` for enabled drag.

The identifiers describe implemented rules, not a public solver selector or registry. Request values and all four aerodynamic inputs must match exactly for drag analysis. Equivalent independently constructed immutable inputs are accepted. Equal derived k is insufficient. Wrong model/missing provenance throws `ArgumentException` naming `result`; request/aerodynamic mismatches name their arguments. Null inputs use `ArgumentNullException`. Validation precedes exact recorded-time returns.

Setting density or coefficient to zero delegates to established gravity dynamics, refinement certificates, and maximum-height computation. Results **retain `GravityAndDrag` identity and all aerodynamic settings**; public gravity analysis rejects them. Enabled-drag consistency/spacing restrictions do not apply to this delegated path. An instantaneous zero speed returns zero drag without changing identity or permanently disabling drag.

## Shared execution and boundaries

`FlightExecution` owns indexed progression, allocation, work/sample limits, immediate contact, completion, contextual execution exceptions, and immutable result construction. Both models supply anchored advancement and specialized event policies. `TrajectoryEvaluation` shares timestamp lookup and exact recorded-maximum comparison; `NumericalEventInterval` shares unchanged finite-state and interior-time mechanics.

`Rk4Integrator`, `FlightDerivative`, and `SimulationTimeGrid` retain their established stages, arithmetic, and guards. `GravityOnlyRefinement` retains its gravity-specific formulas, numerical certificates, and exact-root fast paths. Its short loops are not generalized into nonlinear policies. `GravityDragDynamics`, `DragNumericalEvaluation`, and `GravityDragRefinement` own the new specialized behavior.

Unconstrained flight preflights its entire horizon before sample allocation. Contact-aware execution caps actual ordinary work. Both keep **100,000 indexed ordinary intervals / 100,001 recorded samples**. Boundaries remain `(double)i * TimeStep`, with the exact requested duration substituted only at the reaching boundary. No repeated-addition drift, endpoint tolerance merging, adaptive advancement, or jump at budget exhaustion is introduced. Contact-aware capacity starts at at most 256, doubles with the established cap, and is trimmed before defensive result construction. Diagnostic and refinement states are not recorded samples.

Required failure throws rather than returning an ordinary successful partial trajectory. Resource rejection/exhaustion is `InvalidOperationException`; unsafe integration, localization, or representation is contextual `ArithmeticException`.

## Scoped safe force arithmetic

For enabled drag, set `s=max(abs(vx),abs(vy),abs(vz))`. At s=0 return zero drag. Otherwise compute `q=sqrt((vx/s)^2+(vy/s)^2+(vz/s)^2)`. Keep speed factored as `s*q` and compute each drag component as `-k*s*q*vi`, using the **original vi**.

One private normalized positive-product helper in `GravityDragDynamics` combines the fixed coefficient factors and acceleration products with `Math.ILogB`/`Math.ScaleB`. Coefficient normalization is cached per dynamics object. It is not an arbitrary-range/public arithmetic subsystem. The ordinary equation remains explicit.

Concrete independent range fixtures justify this helper. With mass 1, `(rho,Cd,R)=(1e-300,1e-100,1e200)` and `(1e300,1e100,1e-200)` both have k near π/2, although naive products yield zero/infinity and then NaN. High-precision products of the exact binary64 inputs supply the test reference. Further tests cover an unrepresentable materialized speed with finite accelerations, small speeds, and a tiny transverse component that would be lost through a normalized direction vector.

Exact zero component products stay zero. Nonzero final acceleration overflow or loss completely to zero throws with component context. Representable subnormals are accepted without a relative-accuracy promise. Negligible squared ratios may underflow in the norm because its largest ratio is one; original components remain available for their acceleration products. Final gravity-plus-drag addition must be finite. Ordinary cancellation and rounding remain allowed. No general clamping is performed.

## Enabled-drag numerical consistency policy

Every ordinary advancement, interior query, and refinement trial uses one full RK4 step F and two diagnostic half-steps H from the **same fixed numerical anchor**. Return F after acceptance. Never substitute H, divide discrepancies by a Richardson factor, or recursively step-double the diagnostic half-steps.

| Acceptance condition | Limit |
|---|---|
| Outward-rounded raw `abs(F.position[i]-H.position[i])`, every axis | 1e-8 m |
| Outward-rounded raw `abs(F.velocity[i]-H.velocity[i])`, every axis | 1e-8 m/s |
| Upward spacing of a changing state component | corresponding tolerance / 8 = 1.25e-9 SI |
| Required state/time calculations | finite and representable through unchanged RK4 |
| Completed-state sign/transition evidence | consistent with this physical model |

Identical represented components have zero discrepancy. Unsafe subtraction or outward comparison throws. Check spacing and finiteness at the anchor, RK4 stage states, and completed full/half/diagnostic states. A guarded derivative wrapper provides stage checks without changing the integrator.

Only structural invariants receive spacing exemptions: initially zero horizontal velocity remains zero and its coordinate constant; zero gravity plus zero vertical velocity keeps vertical velocity/height constant. With no gravity and no velocity the whole state is invariant. Assert invariant values rather than inferring them from rounded agreement or rounded-zero acceleration. Large **constant** coordinates may therefore remain valid, while large changing coordinates reject. Other equilibrium states undergo normal checks.

For completed primary and diagnostic paths, each horizontal velocity component preserves its sign and has nonincreasing absolute magnitude; initially zero components stay zero. The same constraints apply to vertical velocity when gravity is zero. Positive gravity permits ascent-to-descent but no later descent-to-ascent transition; vertical speed may legitimately increase or decrease, so no drag-only vertical magnitude/motion bound is imposed there. Loss of a nonzero sign through an unresolved zero also rejects.

For each drag-only axis, completed displacement must follow its velocity direction (or round to no change). Maximum mathematical motion during an interval is `abs(v_i(start))*elapsed`. Compare the completed coordinate against an outward representable endpoint envelope: round the positive interval upward, the speed-times-interval product upward, then the limiting endpoint toward the direction of motion. This accommodates the rounding of coordinate updates without adding an arbitrary absolute tolerance. An invariant zero-velocity axis cannot move; an unrepresentable bound rejects explicitly. The envelope permits local coordinate rounding and is not a physical-contact certificate or a uniform accumulated-error bound.

Check the primary full advancement, first diagnostic half, second diagnostic half, and ordering of the primary endpoint relative to the diagnostic midpoint. Reuse the actual recorded timestamp differences for these checks. All continuous and refinement evaluations use the same checked advancement. These policies are model-specific completed-state checks; RK4 scratch stages receive representation/structural-invariant checks without treating approximate stage velocities as separate physical event evidence.

Failed checks throw with component/operation context and, for ordinary work, indexed step/time context. Reduce timestep when appropriate; coordinate precision and range loss may require resolving the reported scale limitation. No tolerance is silently relaxed.

**These are observed consistency and representation conditions, not certified local/global integration-error bounds.** Step-doubling agreement can underestimate error; spacing alone does not bound arithmetic cancellation or every interior rounded evaluation. The fixed absolute policy intentionally rejects some finite inputs and does not promise useful relative accuracy for tiny quantities.

## Contact and apex mathematics

Writing w=vz, `w'=-g-k*speed*w`. With positive g, upward w decreases and reaches zero at most once; at zero its derivative is negative. Descending height decreases, but w itself may become less negative when drag slows a superterminal descent. With zero g, w keeps its nonzero sign over finite physical time; zero w stays zero. Drag alone produces no finite future apex. Pure vertical descent with positive g/k approaches `-sqrt(g/k)`.

These facts justify this model's contact/apex sign brackets. They are not generic execution assumptions and must be revisited for future Magnus forces.

### Contact identification and localization

Before selecting an ordinary contact candidate, compare **both** completed endpoint geometries using exact `Z > H` versus `Z <= H` classification. If one is airborne and the other on/below-plane, throw a contextual numerical exception in either direction. Do this even when the primary endpoint alone would report duration completion. A small discrepancy is not permission to merge classifications or silently substitute the diagnostic state.

An above-plane endpoint is never contact merely because its residual is small. An on/below-plane candidate requires negative vertical velocity on both full and diagnostic paths. Bracket a root between an airborne lower state and a descending on/below-plane upper state. Apply the same two-sided geometry rule before every refinement bracket update, including apex trials used to establish a touching launch's airborne excursion. Cache each updated upper endpoint's corresponding diagnostic state; no endpoint is reintegrated just for classification. For a touching upward anchor, first refine the apex and require a representably above-plane state; exclude the launch root even for a later exact return. An unresolved excursion throws.

All trials advance from the **original ordinary anchor**, including contact trials after apex localization. Compare signs without multiplying residuals. Use strictly interior bisection times. Each search permits 128 checked trials; same-step launch/return has separately bounded apex and contact searches.

For nonexact contact require all of: valid bracket/direction, outward time width <=1e-10 s, outward upper height residual <=1e-8 m on both numerical paths, full-state checks, and a timestamp within the horizon. Then select the upper endpoint and project **only Z** onto H. Keep its time, X/Y, and velocity. Projection is bounded by the accepted residual and is not collision response or general trajectory clamping.

The contact exact-root shortcut requires **both** primary and diagnostic endpoints exactly on H, with both descending, after launch exclusion, horizon, precision, and evaluation checks. In zero gravity, candidate contact must also be reachable independently from **both the ordinary anchor and the original release state**. An outward lower bound on clearance must not exceed an outward upper bound on initial downward speed times the applicable elapsed time. Unlike the coordinate-update envelope, these necessary contact tests add no coordinate-rounding allowance to travel. They can therefore reject even paired rounded roots that imply impossible motion. If the primary is exactly on H but the diagnostic is strictly below, retain the bracket and require ordinary time-width and paired-height-residual localization; primary equality alone does not authorize the timestamp. An airborne diagnostic always rejects. Even paired equality identifies numerical evaluations, not a universal proof of the true physical contact time or a uniform neighboring error bound. Check acceptance before another trial. Stagnation, insufficient budget, invalid evidence, or unsafe RK4 arithmetic throws; do not invent contact or jump to duration. A required ordinary candidate can itself fail before an earlier event is safely bracketed.

### Ordinary-anchor and release-origin reachability

The ordinary-anchor check uses the current interval's represented center height/vertical velocity and `candidateTime - anchor.Time`. Preserve this local safeguard. Independently, the release-origin check uses the **original immutable** `FlightRequest.InitialState`, the same original contact height H, and total `candidateTime` from release at t=0. `DragNumericalEvaluation` already captures that state once in its readonly `initial` field; the flight loop reuses that evaluator. No new public contract, retained-result field, or integration method is needed.

Each origin has its own comparison: subtract H from its center height, round positive clearance downward with `Math.BitDecrement`, round elapsed time upward with `Math.BitIncrement`, multiply by the origin's absolute downward velocity, and round that product upward. Reject only when clearance's lower bound strictly exceeds possible travel's upper bound. Reject nonfinite/invalid clearance, time, downward velocity, or unrepresentable bound arithmetic; diagnostics identify `ordinary-anchor` versus `release-origin`. Do not reuse the latest rounded height or velocity for the release comparison.

Earlier coordinate rounding may progressively reduce represented clearance while each individual step passes its own numerical checks. A candidate may therefore pass the local bound but fail the independent release bound. The combined guard is invoked at ordinary contact candidates and every new refinement upper endpoint **before** either the exact-root shortcut or terminal-Z projection. Thus it also protects horizon contact. The source contact height is the immutable geometry supplied to the existing refinement calls. No additional RK4 evaluation is performed and both 128-trial budgets remain unchanged.

These maximum-downward-speed comparisons apply only to zero gravity with downward release; positive gravity can accelerate descent and bypasses both comparisons. Initial touching stationary/downward states complete at t=0 before integration/guard invocation. Zero-gravity upward touching launches and stationary airborne paths remain duration-limited when no contact evidence exists. Positive-gravity launch/return and apex processing retain their existing policies. Reachability is **necessary, not sufficient**: passing either or both bounds does not prove contact, global integration accuracy, or empirical baseball accuracy.

### Maximum height and interior witness

Maximum-height analysis is optional and restricted to the actual interval. One-sample contact, initial descent, and constant height select release. Truncated ascent selects its actual terminal/recorded maximum. In positive gravity, binary search velocity signs to locate the unique upward-to-downward interval; negative velocities need not be sorted. Zero-gravity ascent has no future apex to search.

An exact evaluated zero vertical velocity retains the checked exact-root fast path. For a nonexact apex bracket require time width <=1e-10 s, both endpoint velocity residuals <=1e-8 m/s, and a strictly interior midpoint witness through the same full-state evaluator. Require witness velocity residual <=1e-8 m/s and observed lower/upper/witness height spread <=1e-8 m. Select their greatest height, preferring the earliest exact tie, then compare with all recorded samples. The witness consumes one of the same 128 trials. Refine further if conditions are not satisfied; inability to resolve the required witness throws. Do not project velocity.

Endpoint equality alone is insufficient. Two concrete regressions are retained:

- At Z=2^30, the Issue #5 rounding reproduction has equal endpoint heights and an interior height larger by 2^-22 m. Its changing-coordinate spacing fails the new gate, including before an exact-root shortcut.
- At Z=2^20, `vz0=.00023759466905108544`, `g=242.45617275970022`, and tiny positive density, the bracket `[9.79908938180126e-7,9.79988938180126e-7]` s has equal endpoint heights, but its midpoint is one ULP higher with zero evaluated vz. Spacing and full-state checks pass. The interior witness must find the higher height.

Also retain the ascending rounded plateau from `z0=1.9999999999999996`, `vz0=2^-52`, g=0, dt=1 with nonzero drag: the earliest recorded maximum is at t=2. Exact comparisons preserve adjacent distinct heights; no approximate-equality tie is added. Earliest selection concerns the defined numerical candidates and recorded samples, not exhaustive discovery of every unexamined floating-point plateau. The witness likewise observes a finite set, not a universal interior-height certificate.

## Continuous evaluation and measurements

Validate model/provenance first. Time must be finite in `[0,result.FinalTime]` (`ArgumentOutOfRangeException`, `time`). Exact stored timestamps return stored states verbatim, including projected terminal contact. Otherwise use checked RK4 from the preceding sample through the selected model's policy. No analytical drag shortcut, linear interpolation, interior projection, or post-termination evaluation is permitted. Very narrow required RK4 subintervals can fail representability even after a successful simulation.

`FinalTime` is elapsed simulation time. `CompletionReason` identifies actual completion. `TrajectoryMeasurements` supplies final-minus-initial displacement, horizontal displacement with Z=0, and nullable actual-contact time/state. It invents no landing or apex beyond duration. Contact coordinates describe the ball center. Optional evaluation/maximum/displacement failure throws without mutating or invalidating a completed result.

## Resource accounting

| Operation | Bound |
|---|---|
| One enabled-drag advancement | 3 RK4 calls / 12 derivatives |
| 100,000 ordinary intervals | 300,000 RK4 calls / 1,200,000 derivatives |
| One contact or apex search | 128 checked trials, including apex witness; <=384 additional RK4 calls |
| Same-step apex plus contact | Two independent budgets; <=768 additional RK4 calls |
| One interior continuous query | 3 RK4 calls |
| Exact recorded-time query / immediate contact | no integration |

Existing endpoints are reused. Full-state comparisons, damping/motion/reachability checks, and paired geometry classification add no RK4 calls. Gravity-only/disabled-drag work retains its previous accounting. Maximum-height lookup/scanning is bounded by the recorded sample limit, plus one bounded apex search. Refinement failure returns no ordinary partial result.

## Independent verification and reproducibility

Permanent tests distinguish force direction/parameter scaling, arithmetic safety, integration, event logic, ownership, provenance, and optional analysis. The original 395 Physics/Visualizer tests remain, unchanged in meaning. The SDK/package pins and locked dependency graphs are unchanged. CLI and Visualizer runtime behavior are unchanged.

Verification-only analytical references include zero-gravity drag `v=v0/(1+k*v0*t)`, `dx=log(1+k*v0*t)/k`, its 3D constant-direction form, vertical drop via tanh/log-cosh, vertical ascent apex via atan/log, and same-height vertical return `(atan(x)+asinh(x))/sqrt(g*k)` with `x=v0*sqrt(k/g)`. Production does not use these formulas. Tests also cover superterminal slowing, terminal motion, mechanical-energy dissipation `E'=-m*k*speed³`, and reduced representative horizontal travel versus vacuum.

The coarse horizontal review fixture (40 m/s, g=0, T=dt=4) has analytical X≈111.043056557832 m and Vx≈20.045172928851 m/s, versus full RK4 X≈106.458344664651 and Vx≈19.487327558124. Full/two-half differences of ≈4.341449 m and .559051 m/s reject. At dt=.01 its analytical errors are below 1e-8 m / 1e-9 m/s. The dt=.04 case demonstrates that passing a local 1e-8 consistency check does not imply global error <=1e-8.

The optional research generator [generate_drag_reference.py](../../research/generate_drag_reference.py) uses standard-library `decimal` extrapolated modified midpoint with independent event bisection. It imports no production dynamics, RK4, or root code. Ordinary .NET builds/CI consume the checked-in [JSON fixture](../../tests/Lpb.Physics.Tests/Fixtures/drag-reference.json), embedded in the test assembly, and require no Python or additional package.

From repository root, optionally regenerate with Python 3.11+:

```sh
python3 research/generate_drag_reference.py > tests/Lpb.Physics.Tests/Fixtures/drag-reference.json
```

The generator compares 55-digit arithmetic, macro-step .1, midpoint counts 4..14 against 70 digits, macro-step .05, counts 4..16. Modified midpoint endpoint smoothing supplies the even-power expansion extrapolated in inverse squared counts. All compared state/event differences must be <=1e-20 before emitting fixtures. These are stability observations, not formal reference error certificates.

For `(r0,v0)=((0,0,1.0366),(30,20,25))` with the illustrative parameters, the fine reference has contact time **4.377523765386656 s**, apex time **2.030809108019329 s**, and apex Z **24.21083340365763 m**. Reference-setting differences were <=6.32e-25 SI for states and <=2.68e-26 s for event times.

Fixture tolerances at production dt=.01 are 1e-8 m / 1e-9 m/s for states, 1e-8 s for event/apex time, 1e-7 m / 1e-8 m/s at contact, and 1e-8 m for apex height. The C# local observation had contact time **4.377523765414953 s**, apex time **2.0308091079443686 s**, and apex Z **24.21083340352409 m**. Focused .04/.02/.01 step-halving checks compare against the independent reference and show approximately fourth-order reduction on this smooth fixture. Broader convergence remains Issue #10; empirical baseball validation remains #11.

Local verification after the release-origin correction on November 10, 2026 used SDK 10.0.403: locked restore passed, Release build had zero warnings/errors, all **550 tests passed** (468 Physics including 155 new cases, 82 Visualizer; zero skipped/failed), and the unchanged CLI bootstrap smoke check passed. The baseline 395 tests also passed immediately after shared infrastructure extraction, before drag behavior was introduced. Original test source files were not edited. Optional reference regeneration was byte-identical; local documentation targets and `git diff --check` passed. An independent implementation review verified the original 492-test Phase B baseline and requested the focused corrections below. A second independent review verified both original blockers as resolved and requested the release-origin correction below. Final re-review approved the corrected implementation for publication. PR #21 records successful Linux and Windows CI with the same 550-test total before merge. These are historical #6 results; subsequent changes require their own verification.

Across the independent recorded-state fixtures at 1/2/3/4/6 seconds, observed C# maximum component errors were approximately **1.893e-10 m** and **1.123e-11 m/s**. These measurements supplement fixture assertions, not the public accuracy contract.

Failure regressions include initial penetration, same-step unresolved excursions, exact/near-horizon evidence, full-state coarse-step rejection, precision gates, interior witness, optional analysis underflow, unrepresentable subdivisions, a true 128-trial contact exhaustion, ordinary budget exhaustion, last-permitted-step contact, and protected result ownership.

## Focused blocking-review corrections

`DragReviewRegressionTests` adds 42 cases without changing existing tests/tolerances. The first public reproduction uses `m=π/2, R=1, rho=1e12, Cd=1`, g=0, initial speed 1e-6, and `T=dt=5.248930418956277e-6`. Raw RK4 produces primary/diagnostic speeds about .09442410350563328/.09442410350562983 m/s, versus analytical 1.600273859613601e-7 m/s. Its six-component agreement passes, but completed speed grows roughly 94,424 times and displacement opposes velocity. The new magnitude invariant rejects for both signs on all axes. The same physical parameters succeed with timestep divided by 1024 and retain analytical agreement. Separate tests isolate displacement-direction and maximum-motion failures while sign/magnitude checks pass, test invariant coordinates and unsafe bound arithmetic, and preserve legitimate gravity-driven downward acceleration.

The second public reproduction starts one ULP above H with downward speed .75 ULP/s, g=0, T=dt=1, and `m=.145,R=1,rho=1.225,Cd=.35`. At H=1 and H=2^20 the primary rounds onto H while the diagnostic stays one ULP above. It now rejects for **contact geometry disagreement**, not for an unrelated exception or substituted duration. With two ULP clearance and speed 1.25 ULP/s, the opposite disagreement is also constructible: the primary remains above H while the diagnostic reaches H. Both ordinary directions and both directions encountered during refinement reject. A related case with two ULP clearance and speed 1.75 ULP/s makes **both** endpoints round onto H; the zero-gravity reachability condition rejects that impossible event at both heights. A primary exact root with a below-plane diagnostic must proceed through bounded paired localization; regressions cover both a resulting geometry rejection and a legitimate successful localization. Existing consistent exact endpoints, horizon precedence, same-step launch/return, near-plane airborne completion, independent references, and the original resource/provenance/ownership tests continue to pass.

### Release-origin correction — November 10, 2026

`DragReleaseReachabilityTests` adds 16 regressions without changing previous test expectations. At H=1, H=1.5, and H=2^20, the public reproduction starts 14 ULPs above the plane with downward velocity 3.6 ULP/s, dt=1, and duration `3 + 2.2/3.6 = 3.611111111111111` s. Before correction, clearances rounded `14 → 10 → 6 → 2 → 0`, and paired endpoints falsely established contact. Its latest anchor admits 2.2 ULPs of possible travel over a 2-ULP gap, but release admits only about 13 ULPs over the original 14-ULP gap. The necessary initial-speed lower bound on contact time is `14/3.6 = 3.888888888888889` s. All three cases now throw a contextual **release-origin** reachability exception at ordinary step 4, with no successful partial/contact result.

A second family starts 13 ULPs above the plane. Both reachability checks pass at the fourth ordinary endpoint t=4, but the next refinement candidate at t=3.5 passes its local bound and fails release-origin reachability. This verifies the guard before refinement upper updates, not only before ordinary exact roots.

Positive controls use multi-step zero-gravity vertical and oblique descent with both horizontal signs, comparing against the independent constant-direction quadratic-drag solution. They retain the approved 1e-8 SI event/state tolerances. Boundary tests permit a conservative adjacent represented release time, preserve positive/negative-zero immediate contact and stationary/upward no-contact paths, and diagnose unsafe release-origin arithmetic despite a safe local bound. Previous one-step false contacts, damping/geometry blockers, legitimate exact contacts, positive-gravity descent, same-step launch/return, horizon precedence, resource limits, immutability, provenance, and analytical/high-precision reference tests all remain passing. No solver, tolerance, public API, gravity-only behavior, or disabled-drag delegation changed.

### Separately observed legacy gravity-only corner case

The reviewer and local diagnostic reproduced the one-ULP-clearance false contact through the established gravity-only API at H=1 and H=2^20: its exact evaluated-root path reports contact at one second although the initial velocity could cover only .75 ULP, so physical contact cannot occur before 4/3 seconds. This predates the shared-infrastructure refactor. No gravity-only code, certificates, tests, or public behavior is changed by these focused corrections; disabled drag still delegates to that established path.

The observation challenges interpreting an exact **rounded evaluated** plane root as an independently established physical crossing. It does not invalidate or replace the separate nonexact gravity certificates. [Issue #22](https://github.com/skolins3651/lets-play-baseball/issues/22) records the separately reviewed follow-up to investigate exact-root evidence, coordinate rounding, physical reachability, and compatibility implications before revising gravity behavior or its contract. This note records the finding rather than approving a contract change. The Lab drag-support follow-up preserves this limitation, including disabled-drag inheritance. The new drag geometry, motion, and necessary reachability checks likewise remain numerical safeguards, not universal error certificates.

## Practical performance observations

Local November 9, 2026 diagnostic observations: macOS arm64, SDK 10.0.403, Release build, one warm-up then three measured in-process runs per case. A disposable C# `Stopwatch` harness measured only operations, not process startup; `GC.GetAllocatedBytesForCurrentThread` supplied allocation observations. Tiered compilation was disabled **for this diagnostic process only** to reduce warm-up effects; no repository/runtime setting was changed. No benchmark framework, timing gate, or performance dependency was added.

These observations predate the focused blocking-review corrections; the added scalar checks do not change RK4 call budgets, but the table is not a fresh timing measurement of the corrected implementation. Times below are median milliseconds (observed minimum–maximum). They are not portable targets or a controlled benchmarking study.

| Workload | Gravity | Disabled drag | Enabled drag |
|---|---:|---:|---:|
| Six-second unconstrained flight, dt=.01 | .157 (.157–.159) | .117 (.114–.123) | 1.056 (1.045–1.062) |
| Contact flight plus maximum-height query | .181 (.179–.196) | .133 (.131–.138) | .901 (.899–.944) |
| Unconstrained 100,000 steps, T=10, dt=.0001 | 20.458 (20.137–21.123) | 19.803 (19.289–20.009) | 180.142 (179.745–180.458) |
| Contact-aware 100,000 steps without contact | 22.176 (22.039–22.911) | 22.118 (21.738–22.386) | 182.552 (180.792–182.805) |
| Batch of 1,000 interior queries | .268 (.263–.271) | .271 (.264–.308) | 1.964 (1.955–1.965) |

Use the illustrative oblique launch above for flights; set initial Z=1000 for the contact-aware near-budget case. Query times were `.000123 + i*.005`, `i=0..999`, on the six-second unconstrained result. Disabled drag uses density zero. Contact/maximum scenarios end at their own model's contact, so their work is not identical across models.

All three unconstrained near-budget paths allocated approximately 10.682 MiB; contact-aware near-budget paths approximately 17.668 MiB. Enabled drag allocated approximately .065 MiB for the six-second flight, .089 MiB for contact plus maximum, and .275 MiB for 1,000 queries. Collections retain bounded arrays and a defensive snapshot; contact-aware growth explains its additional allocation. Delegates are reused during ordinary execution, and fixed-anchor gravity refinement closures are allocated only on the event path. Enabled drag adds force/representation/check costs as well as three RK4 calls; elapsed time need not be exactly three times gravity time. Small-query allocation or future tuning remains separate from correctness work.

## Scientific sources and limits

- [NASA drag equation](https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/drag-equation/): force and explicit reference-area convention.
- [NASA sphere drag](https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/drag-of-a-sphere/): Reynolds-number and surface-condition dependence; constant coefficient is an approximation.
- [MLB Official Baseball Rules 2026, Rule 3.01](https://mktg.mlbstatic.com/mlb/official-information/2026-official-baseball-rules.pdf): regulation mass/circumference support the illustrative ball scale, not aerodynamic calibration.
- [Lyu, Smith, Elliott, and Kensrud, 2022](https://baseball.physics.illinois.edu/LyuDragLiftSpin.pdf), DOI 10.1177/17543371221113914: measured spin, speed, and seam-orientation dependence limits interpretation of a single constant Cd.
- [NASA SP-367](https://appel.nasa.gov/wp-content/uploads/2018/08/NASA-SP-367-Intro-to-Aero-of-Flight.pdf): standard-atmosphere scale for illustrative density; no atmosphere solver is implemented here.

Keep numerical consistency, event-localization resolution, floating-point representation, accumulated integration error, physical-model assumptions, and empirical accuracy distinct. No observed residual, narrow bracket, witness, spacing gate, or step-doubling agreement is a universal position/velocity error proof. No empirical calibration has occurred.

Future force models can reuse execution/lookup while supplying their own dynamics/event policies. Future stochastic parameter lifecycle is recorded in architecture only. The separately authorized [Trajectory Lab follow-up](../visualizer.md) selects both existing models using their matching analysis APIs, including for disabled drag; UI equations and gravity evaluation of drag results remain prohibited. No numerical contracts in this specification change as part of that enhancement.
