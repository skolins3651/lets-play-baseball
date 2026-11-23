# Model A software contracts — B2-I1

> **Status:** B2-D architecture and B2-N numerical design approved on November 23, 2026. B2-I1 contracts independently reviewed and approved; no Model A runtime is implemented.\
> **Scientific authority:** [Model A](model-a.md), [physical conventions](../conventions.md), and the [decision register](../../research/aerodynamics/decision-register.md).\
> **Architecture:** [project overview](../../architecture.md).

## Implemented capability and boundary

B2-I1 introduces immutable, engine-independent inputs and sample/result contracts in `Lpb.Physics`. It preserves the existing gravity/drag public APIs, `FlightModelKind`, B1 mathematical kernels, integration, numerical guards, events, and visualizer behavior. There is no `ModelASimulator`, coupled derivative evaluator, nine-component RK4 advancement, continuous evaluation, contact, or maximum-height query.

The independently approved B2 numerical policy governs later execution. Its approval does not imply that the acceptance machinery has been implemented or run. Scientific fit constants and beta remain provisional; contract tests do not establish empirical validation.

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

Focused NUnit contract tests cover composition/equality, scalar boundaries, explicit factories, distinct disabled cases, exact retained-input matching, immutable ownership, finite/ordered samples, completion/count limits, and the approved public API inventory. Existing gravity/drag tests retain their meanings. No Model A integration or numerical-policy test is introduced here.

B2-I2 will separately add coupled mathematical machinery; I3 the checked duration runtime; I4 independent trajectory verification. The approved numerical design includes exact-decimal budgets, bounded arithmetic, rate restrictions, and completed-path diagnostics; none is implemented by this contract stage. Later physical-rate enclosures must account for both scientific coefficient values and the rounded constants used by B1.

Complete states and private original inputs are retained for later reconstruction. B3 must separately approve coupled evaluation and event guarantees, including original-anchor retention, earlier-contact exclusion, tangencies, unresolved intervals, and optional-query failures. Recorded samples alone cannot establish between-sample ground clearance. ASR-A remains separately authorized after ASR-B verification and need not depend on B3.
