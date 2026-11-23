# Model A software contracts and internal mathematics — B2-I1/I2

> **Status:** B2-D architecture and B2-N numerical design approved on November 23, 2026. B2-I1 contracts independently reviewed and approved. B2-I2 internal mathematical machinery has passed independent review and awaits PR/CI review and separate merge authorization; no checked Model A runtime is implemented.\
> **Scientific authority:** [Model A](model-a.md), [physical conventions](../conventions.md), and the [decision register](../../research/aerodynamics/decision-register.md).\
> **Architecture:** [project overview](../../architecture.md).

## Implemented capability and boundary

B2-I1 introduces immutable, engine-independent inputs and sample/result contracts in `Lpb.Physics`. The independently reviewed B2-I2 implementation adds internal coupled derivatives and typed nine-component classical RK4 advancement. Existing gravity/drag public APIs, `FlightModelKind`, B1 mathematical kernels, six-component integration, numerical guards, events, and visualizer behavior remain unchanged. There is no `ModelASimulator`, checked duration producer, continuous evaluation, contact, or maximum-height query.

The independently approved B2 numerical policy governs later checked execution. Its approval does not imply that the acceptance machinery has been implemented or run. Scientific fit constants and beta remain provisional; contract and mathematical tests do not establish empirical validation.

## Internal coupled mathematics — B2-I2

All I2 machinery is internal to `Lpb.Physics`; it adds no public type or member and does not produce `ModelASimulationResult` instances.

`ModelADynamics(double gravityMagnitude, ModelAParameters parameters)` composes the unchanged `GravityDragDynamics`, `ModelAMagnusKernel`, and `ModelARotationKernel`. Its `Evaluate(double time, SpinFlightState state)` returns a `SpinFlightDerivative` containing three position rates (m/s), three velocity rates (m/s²), and three angular-velocity rates (rad/s²). The derivative is an immutable scratch-rate structure, consistent with the existing `FlightDerivative`; the integrator checks its nine components before using them as physical state.

Each evaluation passes the same instantaneous velocity and angular velocity to the B1 kernels. Position rates equal that velocity. Translation retains the existing gravity-plus-drag calculation, then adds Magnus componentwise; rotation uses the explicitly selected R0 or R1-U kernel. Zero density, zero drag, zero spin, zero speed, and zero-beta R1-U retain their distinct meanings. R0 does not infer or require inertia. No release geometry, ASR, speed, or damping rate is frozen.

Constituent arithmetic failures remain failures, with their original exception preserved as the inner cause and additional coupled-dynamics context. Nonfinite componentwise acceleration sums reject explicitly. Ordinary finite addition is retained; I2 does not promise to recover a finite combined result when an unchanged constituent kernel cannot represent its own output. No cancellation-based exemption, clipping, projection, or physical cutoff is added.

`SpinRk4Integrator.Step(double startTime, double endTime, SpinFlightState state, Func<double, SpinFlightState, SpinFlightDerivative> evaluate)` is the typed nine-component companion to the unchanged six-component `Rk4Integrator.Step`. A companion avoids ambiguity in existing calls using default states and lambdas; no generic state or ODE framework is introduced.

The step evaluates four complete states at the supplied start, midpoint, midpoint, and end times. It advances all nine components together with the classical weights 1, 2, 2, 1 and the actual interval divided by six, including shortened intervals. Scalar advancement and weighting follow the existing six-component operation order without changing that path.

Null evaluators use `ArgumentNullException`. Nonfinite, negative-start, or nonprogressing times, unrepresentable positive half/sixth steps, and absent interior midpoints use `ArithmeticException`. Every derivative component, scaled increment, intermediate state, weighted sum, final increment, and completed state must be finite. Arithmetic failures identify the stage and component; exceptions from the derivative delegate additionally retain the evaluation time and original inner cause. Unexpected non-arithmetic exceptions are not reclassified.

These checks establish representable classical RK4 arithmetic only. Ordinary rounding or finite increments lost at state precision are not B2-N acceptance decisions. I2 implements no full-step/two-half-step comparison, exact-dyadic runtime comparison, 128-bit interval machinery, rate restriction, completed-path speed/energy acceptance, adaptive stepping, spin normalization, or policy-confirmed result. I3 can wrap the derivative delegate to inspect each complete stage state and rates, then check the completed mathematical step without replacing its coupling.

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

The constructor does not authenticate intervening dynamics, enforce an indexed sample schedule, preflight a requested integration horizon, or run numerical checks. Those responsibilities belong to the later authorized producer. I1 internal tests use fixed contract data, not a production simulation or a claimed validated trajectory.

Private immutable `ModelAFlightProvenance` retains the original request and parameter graph, including prescription, rotational kind, and applicable physical values. The result exposes none of that graph publicly. Internal matching methods are value checks only, not evidence that samples were generated using the supplied equations.

The approved future numerical-policy identifier is declared internally as:

```text
classical-rk4/indexed-v1;model-a-full-state-v1
```

The per-provenance `NumericalPolicyIdentity` remains null in I1, and its constructor has no argument for claiming numerical execution. Declaring the approved identifier is not an execution attestation. A later authorized checked producer must establish how the completed policy identity is recorded; I1 manufactures no such evidence.

## Verification and later responsibilities

The I1 NUnit contract tests cover composition/equality, scalar boundaries, explicit factories, distinct disabled cases, exact retained-input matching, immutable ownership, finite/ordered samples, completion/count limits, and the approved public API inventory. Existing gravity/drag tests retain their meanings.

I2 adds independent coupled-force/torque references, derivative-level energy and prescribed-circle identities, stage-specific reactivation/coupling, nine-component manufactured ODEs, analytical rotations, and fourth-order convergence checks. Test-only exact rational/interval references are reused from B1; no such arithmetic is added to the runtime. Tests cover every derivative component at every RK4 stage, intermediate/final arithmetic failures, and preservation of constituent error context. Zero-spin R0 translation is also compared bitwise with the unchanged six-component path. These are mathematical-unit checks, not B2-N acceptance or I4 full-runtime verification.

| Stage | Current implementation status |
| --- | --- |
| B2-I1 | Contracts and private provenance implemented, independently approved, and merged through PR #27. |
| B2-I2 | Internal coupled dynamics and typed nine-component RK4 independently reviewed and approved; awaiting PR/CI review and separate merge authorization. |
| B2-I3 | Checked duration runtime and B2-N acceptance machinery remain unimplemented and require separate authorization. |
| B2-I4 | Independent verification of the complete checked trajectory producer remains pending. |

The approved numerical design includes exact-decimal budgets, bounded arithmetic, rate restrictions, and completed-path diagnostics; I2 does not implement them. Later physical-rate enclosures must account for both scientific coefficient values and the rounded constants used by B1. The provenance policy identity remains null; I2 neither changes the I1 contracts nor attests to policy execution.

Complete states and private original inputs are retained for later reconstruction. B3 must separately approve coupled evaluation and event guarantees, including original-anchor retention, earlier-contact exclusion, tangencies, unresolved intervals, and optional-query failures. Recorded samples alone cannot establish between-sample ground clearance. ASR-A remains separately authorized after ASR-B verification and need not depend on B3.
