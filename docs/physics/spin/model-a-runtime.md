# Model A contracts, mathematics, and checked duration runtime

> **Status:** B2-D architecture and B2-N numerical design approved on November 23, 2026. B2-I1/I2/I3 independently approved and merged through PR #27/#28/#29. B2-I4 verification passed independent review and awaits PR and CI review and separate merge authorization.\
> **Scientific authority:** [Model A](model-a.md), [physical conventions](../conventions.md), and the [decision register](../../research/aerodynamics/decision-register.md).\
> **Architecture:** [project overview](../../architecture.md).

## Implemented capability and boundary

B2-I1 introduces immutable, engine-independent inputs and sample/result contracts in `Lpb.Physics`. The independently approved and merged B2-I2 implementation adds internal coupled derivatives and typed nine-component classical RK4 advancement. The independently reviewed I3 implementation adds `ModelASimulator.Simulate(ModelAFlightRequest request, ModelAParameters parameters)`, returning a duration-complete `ModelASimulationResult` only after the fixed B2-N checks pass. Existing gravity/drag public APIs, `FlightModelKind`, B1 mathematical kernels, six-component integration, legacy numerical guards, events, and visualizer behavior remain unchanged. Model A has no continuous evaluation, contact, or maximum-height query.

The independently approved B2 numerical policy governs checked execution. The I3 implementation passed independent review and merged through [PR #29](https://github.com/skolins3651/lets-play-baseball/pull/29). [I4 independent trajectory verification](model-a-verification.md) measures accuracy on specified fixtures. Scientific fit constants and beta remain provisional.

## Internal coupled mathematics — B2-I2

All I2 machinery is internal to `Lpb.Physics`; it adds no public type or member and does not produce `ModelASimulationResult` instances.

`ModelADynamics(double gravityMagnitude, ModelAParameters parameters)` composes the unchanged `GravityDragDynamics`, `ModelAMagnusKernel`, and `ModelARotationKernel`. Its `Evaluate(double time, SpinFlightState state)` returns a `SpinFlightDerivative` containing three position rates (m/s), three velocity rates (m/s²), and three angular-velocity rates (rad/s²). The derivative is an immutable scratch-rate structure, consistent with the existing `FlightDerivative`; the integrator checks its nine components before using them as physical state.

Each evaluation passes the same instantaneous velocity and angular velocity to the B1 kernels. Position rates equal that velocity. Translation retains the existing gravity-plus-drag calculation, then adds Magnus componentwise; rotation uses the explicitly selected R0 or R1-U kernel. Zero density, zero drag, zero spin, zero speed, and zero-beta R1-U retain their distinct meanings. R0 does not infer or require inertia. No release geometry, ASR, speed, or damping rate is frozen.

Constituent arithmetic failures remain failures, with their original exception preserved as the inner cause and additional coupled-dynamics context. Nonfinite componentwise acceleration sums reject explicitly. Ordinary finite addition is retained; I2 does not promise to recover a finite combined result when an unchanged constituent kernel cannot represent its own output. No cancellation-based exemption, clipping, projection, or physical cutoff is added.

`SpinRk4Integrator.Step(double startTime, double endTime, SpinFlightState state, Func<double, SpinFlightState, SpinFlightDerivative> evaluate)` is the typed nine-component companion to the unchanged six-component `Rk4Integrator.Step`. A companion avoids ambiguity in existing calls using default states and lambdas; no generic state or ODE framework is introduced.

The step evaluates four complete states at the supplied start, midpoint, midpoint, and end times. It advances all nine components together with the classical weights 1, 2, 2, 1 and the actual interval divided by six, including shortened intervals. Scalar advancement and weighting follow the existing six-component operation order without changing that path.

Null evaluators use `ArgumentNullException`. Nonfinite, negative-start, or nonprogressing times, unrepresentable positive half/sixth steps, and absent interior midpoints use `ArithmeticException`. Every derivative component, scaled increment, intermediate state, weighted sum, final increment, and completed state must be finite. Arithmetic failures identify the stage and component; exceptions from the derivative delegate additionally retain the evaluation time and original inner cause. Unexpected non-arithmetic exceptions are not reclassified.

These integrator checks establish representable classical RK4 arithmetic only. Ordinary rounding or finite increments lost at state precision are not I2 acceptance decisions. I3 wraps the derivative delegate to inspect each actual complete stage state and rate. A narrow internal overload passively observes the actual interval, half-step, and sixth-step coefficients before the first derivative. The original four-stage arithmetic expressions, evaluation order, errors, and evaluation count are retained; no derivative is recomputed at a counterfactual state.

## Checked duration execution — B2-I3

The public simulator is static, with the single `Simulate` method described above. It validates null arguments and reuses `SimulationTimeGrid` to preflight the complete indexed horizon before allocating samples. It retains the original release state, accepted full endpoints, and exact requested terminal timestamp, including shortened intervals. Limits remain 100,000 ordinary intervals, 100,001 retained samples, and 1,200,000 physical derivative evaluations. `DurationReached` is the only successful completion; there are no ground restrictions.

For each ordinary anchor A, `ModelANumericalEvaluation` validates the full interval and both diagnostic subdivisions before any integration. Each requires finite progressing times, positive representable half/sixth coefficients, and an interior midpoint. It executes full endpoint F, first-half endpoint M from A, and second-half endpoint H from M, in that order, using each call's actual representable interval. Successful intervals use exactly three RK4 calls and twelve physical evaluations. Only the original F is retained. No retry, adaptation, Richardson correction, projection, clipping, normalization, fallback, or successful partial trajectory is available.

Stage checks run in order: finite state and release-established invariants; bounded damping and coupled-rate restrictions; physical derivative evaluation; finite derivatives and zero derivatives for invariant components. After all branches complete, the criterion order is agreement, spacing, arithmetic loss, angular conditions, and completed-path physical compatibility. Component order is position, velocity, angular velocity, each X/Y/Z. Streaming summaries retain per-component maxima and the first context attaining each maximum, with an exact weighted-rate accumulator and previous derivative; no persistent stage history is stored.

### Exact budgets and bounded arithmetic

`ModelANumericalArithmetic` is separate from the unchanged nearest-rounded B1 helper. Finite binary64 inputs, including subnormals, decode to canonical exact dyadics N·2^E. Exact addition, subtraction, multiplication, cancellation, and comparison decide budgets and arithmetic loss without conversion back to double. With exact integer J = 10,000,000,000, T = 100 + S, and B = T/J, the scales are:

| Component | Exact scale S |
| --- | --- |
| Position i | max(abs(F_i − A_i), abs(H_i − A_i), h abs(A_velocity,i)) |
| Velocity i | max(abs(A_i), abs(F_i), abs(H_i)) |
| Angular velocity i | abs(A_i) |

Agreement requires J abs(F_i − H_i) ≤ T_i. Changing-state spacing across anchors, stages, and endpoints requires 8J MaxSpacing_i ≤ T_i. Encoded-exponent spacing handles both signs, powers of two, zero/subnormals (2^-1074), and `double.MaxValue` (2^971 using virtual successor 2^1024).

For each call, record binary64 h, c2, c6, actual stage states, derivatives k1…k4, and endpoint. The audit uses exact classical 1/2 and 1/6 with recorded h, rather than rounded c2/c6. With anchor a, observed y2, y3, y4, yf and exact K = k1 + 2k2 + 2k3 + k4:

| Observation | Six times exact arithmetic loss |
| --- | --- |
| Stage 2 | 3 abs(2(y2 − a) − h k1) |
| Stage 3 | 3 abs(2(y3 − a) − h k2) |
| Stage 4 | 6 abs((y4 − a) − h k3) |
| Endpoint | abs(6(yf − a) − h K) |

The maximum over all three calls must satisfy 8J MaxLoss6_i ≤ 6T_i. This includes coefficient rounding, multiplication, accumulation, and anchor addition, conditional on recorded derivatives. It does not certify derivative error, nonlinear feedback, timestamp error, truncation error, or accumulated trajectory error. Production intermediate overflow still rejects despite a finite ideal endpoint.

Division, norms, and rate/compatibility expressions use fixed 128-significant-bit outward dyadic intervals constructed with integer floor/ceiling. Division checks the denominator excludes zero before bounding all four endpoint quotients. Norms sum exact squares before bounding the root; interval squares account for zero crossings. A restoring root uses at most a 256-bit radicand and 128 two-bit digit iterations. Acceptance requires discrepancy.upper ≤ allowance.lower; unresolved overlap rejects without precision escalation. Abstract bounds can exceed binary64 range or remain nonzero below its subnormal range.

Shifts, exponents, and potentially growing significands are guarded before excessive integer allocation: 65,536 significand bits and exponents ±32,768. Resource rejection uses contextual `InvalidOperationException`; numerical rejection uses contextual `ArithmeticException` with its original cause. Invalid arguments retain their argument exceptions. Messages identify the ordinary interval, branch, stage, component, and criterion where applicable. Smaller steps do not repair every state-scale or B1 component-resolution failure.

### Stage-local rates, invariants, and angular checks

Cached rate coefficients enclose both exact scientific decimals 0.336 = 42/125 and 6.041 = 6041/1000 and their actual B1 binary64 representations, exact binary64 physical inputs, and π between neighboring doubles around `Math.PI`. Independent tests justify π using the existing Machin rational enclosure. B1 kernels and coefficients remain unchanged.

At every actual stage of each call require h γU ≤ 1 and h L ≤ 1. Here B = ρπr²/(2m), k = B C_D, C = B(0.336)(6.041)r, γ = ρπr⁴β/I for R1-U (zero for R0), and L = max(2kU + 3CΩ + CU/r, γ(rΩ + U)). The first is sufficient for the ideal variable-rate angular RK4 recurrence, with endpoint multiplier in [3/8, 1]; it is not a coupled floating-point stability proof. The second is a conservative engineering sensitivity restriction. Norm Lipschitz bounds apply at zero speed without v/U. Zero instantaneous lift and gyro alignment supply no perturbation-coupling exemption.

The closed invariant catalogue is classified from release inputs and selected physics:

- Zero gravity and zero release velocity: verify the entire stationary state; bypass changing-state representation and perturbation-rate restrictions, retaining time checks and ordinary execution.
- R0, zero density, or zero-beta R1-U: all angular components are constant.
- Any zero release angular component: that component stays zero.
- Permanently absent lift (zero density or entirely zero release spin): an axis with zero release velocity and zero gravity component has constant velocity and position.
- Permanently absent lift and drag: velocity is constant on zero-gravity axes; position is constant only if that velocity is zero.

Exempt values and zero derivatives are checked at every actual observation; exemption never repairs the state. Positive-gravity release from rest and initially gyro motion receive no extra invariants. Other components undergo normal representation checks.

Completed nonconstant angular components retain sign, remain nonzero, and do not increase in magnitude. Scratch angular states remain in the call anchor's signed range, without monotone stage ordering. Dominant-axis ties choose X then Y then Z. Axis checks use exact J abs(ω_b,i ω_a,d − ω_b,d ω_a,i) ≤ T_ω,i abs(ω_a,d), for all actual completed calls and recorded F against release. Zero spin requires zero preservation; no normalization is performed.

### Completed-path physical compatibility and limits

Only A→F, A→M, and M→H receive speed/energy diagnostics; M→F is not a physical advance. Translational scratch states receive neither diagnostic. With exact Q_v = sum(T_velocity,i²), speed requires J U_b ≤ J U_a + JgΔt + sqrt(Q_v), using outward bounds. Energy compares exact J²[sum((v_b,i − v_a,i)(v_b,i + v_a,i)) + 2g(z_b − z_a)] to a lower bound for 2J(U_a + gΔt)sqrt(Q_v) + Q_v + 2JgT_position,z. This avoids subtracting separately enormous energies.

Exact-model restrictions motivate these checks; their numerical slack is engineering compatibility allowance, not certified endpoint error. Acceptance does not establish accumulated error, global convergence, empirical lift/beta calibration, useful relative accuracy beneath absolute floors, or between-sample event completeness. I4 independently reviewed coupled-trajectory results are documented in the [verification report](model-a-verification.md).

## Public inputs and samples

| Type | Construction | Get-only properties |
| --- | --- | --- |
| `SpinFlightState` | `SpinFlightState(FlightState translation, Vector3d angularVelocity)` | `Translation`, `AngularVelocity` |
| `ModelAFlightRequest` | `ModelAFlightRequest(SpinFlightState initialState, double gravityMagnitude, double duration, double timeStep)` | `InitialState`, `GravityMagnitude`, `Duration`, `TimeStep` |
| `ModelARotationSettings` | `R0()` or `R1U(double rotationalInertia, double beta)` | `Kind`, nullable `RotationalInertia`, nullable `Beta` |
| `ModelAParameters` | `NathanAsrB(AerodynamicSettings aerodynamics, ModelARotationSettings rotation)` | `Aerodynamics`, `Rotation` |
| `SpinTrajectorySample` | `SpinTrajectorySample(double time, SpinFlightState state)` | `Time`, `State` |

State and sample are non-positional `readonly record struct` types with explicit constructors. All other input types are sealed immutable classes. `ModelARotationKind` contains only `R0` and `R1U`; neither settings class has a public constructor or a general policy selector.

Translation uses the existing ball-centre position in m and field velocity in m/s. Angular velocity is a world-frame axial vector in rad/s, using the right-hand rule and the adopted field coordinates. Negative coordinates, including below-ground positions, are valid. Finite vectors are provided by the existing `Vector3d` contract; no spin magnitude, axis, ASR, or other derived diagnostic is retained. Default state/sample values are valid zeros.

`ModelAFlightRequest` internally composes a `FlightRequest`, reusing its validation and scalar storage: gravity is finite and nonnegative in m/s²; duration and timestep are finite and strictly positive in seconds. A nondivisible horizon or timestep greater than duration is valid. Construction performs no integration-work preflight or numerical acceptance. Finite valid inputs need not be executable by a later solver.

R0 carries null inertia and beta. R1-U requires finite positive scalar inertia in kg m² and finite nonnegative dimensionless beta, including when beta or density is zero. No inertia is inferred from mass/radius. A future rotational-energy comparison must declare a common analytical inertia separately for R0 and retain that assumption in its own provenance.

`ModelAParameters.NathanAsrB` retains the supplied immutable settings objects and the internal fixed prescription identity `model-a/nathan-asr-b-v1`. There is no generic public constructor, ASR-A factory, configurable prescription string, or registry. Zero drag coefficient removes drag only; zero-beta R1-U remains a distinct selection from R0. Zero density and every original aerodynamic value remain retained rather than normalized into another model.

Scalar range failures use parameter-specific `ArgumentOutOfRangeException`; null settings use `ArgumentNullException`. Sample timestamps must be finite and nonnegative, in elapsed seconds.

## Equality and exact input retention

State/sample equality is exact ordinary .NET value equality; adjacent distinct finite component values are distinct. Signed zeros compare equal, while their original bits are preserved by input construction. No approximate equality or physical zero cutoff is introduced.

Request, parameters, rotation settings, and result retain ordinary reference equality. Internal provenance matching separately compares complete input values exactly. Equivalent separately constructed inputs match; changed state components, gravity/time inputs, aerodynamic values, rotational kind, inertia, or beta do not. Equal derived drag/damping coefficients are insufficient. No public comparison or context-validation API is introduced.

## Result ownership and private provenance

`ModelASimulationResult` has no public constructor. It exposes only:

- `Samples: IReadOnlyList<SpinTrajectorySample>`;
- `FinalState: SpinFlightState`;
- `FinalTime: double`;
- `CompletionReason`;
- fixed read-only `ModelIdentity: string`, equal to `model-a/nathan-asr-b-v1`.

Its internal constructor is `ModelASimulationResult(SpinTrajectorySample[] samples, ModelAFlightProvenance source, CompletionReason completionReason)`. Only `DurationReached` is supported. Ground-contact completion and unknown enum values reject with `ArgumentOutOfRangeException` naming `completionReason`.

Construction clones the array before validating its contents and exposes a read-only wrapper. Generic and nongeneric collection mutation reject; `ICollection.SyncRoot` returns the wrapper rather than its backing array. `FinalTime` and `FinalState` derive from the terminal sample.

The structural checks require:

- 2–100,001 samples, using the existing shared sample-count ceiling;
- finite nonnegative timestamps and all nine finite state components;
- strictly increasing time, starting at exactly zero and ending at exactly the request duration;
- exact initial-state agreement with the retained complete release state.

No geometric restriction is imposed on sample positions. Invalid sample structure or initial-state mismatch uses `ArgumentException` naming `samples`; null samples or provenance use `ArgumentNullException`.

The constructor does not authenticate intervening dynamics, enforce an indexed sample schedule, preflight a requested integration horizon, or run numerical checks. Those responsibilities belong to the checked simulator. I1 internal tests use fixed contract data, not a production simulation or a claimed validated trajectory.

Private immutable `ModelAFlightProvenance` retains the original request and parameter graph, including prescription, rotational kind, and applicable physical values. The result exposes none of that graph publicly. Internal matching methods are value checks only, not evidence that samples were generated using the supplied equations.

The approved numerical-policy identifier is declared internally as:

```text
classical-rk4/indexed-v1;model-a-full-state-v1
```

The two-argument internal provenance constructor still records null, preserving unchecked I1 contract fixtures. Only the checked simulator calls the narrow internal completed-duration factory, after every interval has passed B2-N, to attest `NumericalPolicyIdentity`. There is no public attestation selector or additional result property. Original request and parameter objects and contextual value matching remain intact. Declaring the identifier alone is not evidence of execution.

## Verification and later responsibilities

The I1 NUnit contract tests cover composition/equality, scalar boundaries, explicit factories, distinct disabled cases, exact retained-input matching, immutable ownership, finite/ordered samples, completion/count limits, and the approved public API inventory. Existing gravity/drag tests retain their meanings.

I2 adds independent coupled-force/torque references, derivative-level energy and prescribed-circle identities, stage-specific reactivation/coupling, nine-component manufactured ODEs, analytical rotations, and fourth-order convergence checks. Its independent test-only rational reference remains separate from I3 production arithmetic. Tests cover every derivative component at every RK4 stage, intermediate/final arithmetic failures, and preservation of constituent error context. Zero-spin R0 translation is also compared bitwise with the unchanged six-component path. These are mathematical-unit checks, not I4 full-runtime verification.

I3 adds independent rational certificates for the approved budget ties, spacing/loss boundaries, subnormal loss, cancellation, quotient remainder, square-root inequalities, bounded-work guards, and coefficient enclosures. Policy/runtime tests cover the closed invariants, rates, angular/axis conditions, physical diagnostics, amplification counterexamples, full endpoint retention, twelve evaluations, clock/subdivision boundaries, immutable results, and checked versus unchecked provenance. Targeted constant-speed gyro, prescribed-polynomial damping, and analytical-circle fixtures remain I3 checks.

I4 adds all-component recorded-time comparisons against six independent high-precision trajectories, analytical reductions and tilted circular motion, energy-balance and convergence evidence, and Windows runtime measurements. See the [I4 verification report](model-a-verification.md) for regeneration and results.

| Stage | Current implementation status |
| --- | --- |
| B2-I1 | Contracts and private provenance implemented, independently approved, and merged through PR #27. |
| B2-I2 | Internal coupled dynamics and typed nine-component RK4 independently approved and merged through PR #28. |
| B2-I3 | Checked duration runtime and complete fixed B2-N machinery independently approved and merged through PR #29. |
| B2-I4 | Independent verification of the checked trajectory producer independently approved and awaiting PR and CI review and separate merge authorization. |

B2 completion remains subject to I4 PR and CI review and successful merge closeout.

Complete states and private original inputs are retained for later reconstruction. B3 must separately approve coupled evaluation and event guarantees, including original-anchor retention, earlier-contact exclusion, tangencies, unresolved intervals, and optional-query failures. Recorded samples alone cannot establish between-sample ground clearance. ASR-A remains separately authorized after ASR-B verification and need not depend on B3.
