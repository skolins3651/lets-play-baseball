# Issue #7 aerodynamic models and rotational policies

> Status: A2.1 documentation reviewed and accepted on November 22, 2026. Conceptual research taxonomy; no executable contract. This approval does not constitute empirical validation, adoption of provisional coefficients, or authorization to implement the models.
> Context: [architecture](../../architecture.md), [sources](../../research/aerodynamics/source-register.md), and [decisions](../../research/aerodynamics/decision-register.md).

## Authority and maturity

The recovered taxonomy handoff (H02 in the source register) reconstructs the November 13-14, 2026 discussions and their subsequent narrowing through November 22.
Its descriptions are faithful paraphrases supported partly by recovered summaries, not complete verbatim transcripts.
It establishes historical intent, not seven approved scientific specifications.
The current A2.1 authorization controls scope; merged code and adopted repository contracts continue to govern implemented behavior.
The corrected R1 research record, revision 1.1 (R08), supersedes the original four-page record (R07).

Only **Model A** is selected for the initial Issue #7 aerodynamic implementation.
All models remain unimplemented in the inspected baseline; the existing library implements gravity and constant-coefficient drag.
Models B-G and rotational policies R2/R3 are conceptual, deferred research directions.
A more detailed model is not automatically more accurate: calibration, identifiability, and the intended regime matter.

## Historical scope progression

1. **Original ambition, November 13-14:** A was the baseline; A-D were considered possible early implementations, subject to scientific evidence and feasibility. E-G were primarily exploratory.
2. **Research refinement:** Lift studies exposed seam dependence and unresolved mixed-spin prescriptions. Torque studies exposed sparse calibration, apparatus uncertainty, and limits on identifiable complexity.
3. **Current selection, through November 22:** Implement and investigate A first, with provisional lift and rotational references and explicit comparators. Preserve the remaining models and Experiments II-III for future work.

This narrowing is a deliberate scientific and engineering judgment, not rejection of B-G or evidence that their omitted physics is absent.
Model A's greater detail reflects where research effort was concentrated.
Future compatibility does not authorize speculative B-G state, interfaces, dependencies, or scaffolding now.

## Terms used in this research program

| Term | Meaning | Example or boundary |
| --- | --- | --- |
| Model | Physical representation or abstraction of aerodynamic response | A's instantaneous smooth lift; B's conditional averaging |
| Policy family | A replaceable modeling dimension | Lift, drag, rotational motion, torque, or averaging; illustrative, not a closed inventory |
| Policy | A particular formulation within a family | R0 within rotational motion; Nathan saturation within lift |
| Subpolicy | A subordinate refinement when useful | An R1 formulation or a mixed-spin prescription within a chosen lift treatment |
| Mathematical law | Equations defining a response | A fitted coefficient function; no compulsory software hierarchy is implied |
| Protocol | A compatible model and policy combination | A with declared lift, drag, and rotational choices |
| Experiment | An investigation using protocols, cases, controls, numerical settings, outputs, and provenance | Experiment I compares rotational assumptions within A |

These are scientific distinctions, not mandated C# types or configuration levels.
R1-E, R1-U, and R1-M emerged during later research; they were not a finalized subpolicy set in the original taxonomy.
R1-M is a broader moment framework and can contain the constitutive simplification yielding R1-U.

The historical string `A + R1.NTH + CD.CONST + CL.FIT1` illustrated composition only.
Its tokens are not approved public identifiers, software APIs, configuration schemas, or an implementation requirement.
Likewise, historical "Protocol A" does not mean the same thing as Experiment I or a single coefficient law.

## Aerodynamic representations A-G

### A - Smooth Magnus

**Meaning:** An instantaneous-state, empirical conventional transverse-lift baseline without explicit seam phase, body attitude, or wake memory.
Its scientific inputs include translational state, instantaneous world-frame angular velocity, environmental quantities, and declared coefficient policies.
This statement describes conceptual needs, not a change to current state contracts.

Smooth does not mean a physically seamless baseball: calibration measurements used real seamed balls.
Their coefficients may already incorporate seam-mediated behavior; adding future corrections requires a consistent baseline to avoid double counting.
No unique physical separation of those contributions is established here.

**Maturity:** Selected initial model; Nathan saturation plus ASR-B is the provisional lift reference, with ASR-A an essential comparator.
SHS and Healey-Wang Hill remain alternatives; exact numerical calibration remains open.
Experiment I is the principal investigation.

### B - Cycle-averaged seam model

**Meaning:** Conditional averaging over selected rapid phase effects while retaining physically relevant slower orientation, configuration, or rotational dependencies.
It does **not** universally average every seam effect over one complete revolution.
"Cycle" requires a scientifically selected phase/period interpretation; different periods or multiple phases need not reduce to one revolution.

The averaging operator, relevant periods, retained slow variables, and required state are all unselected.
A full-revolution integral could later be one policy, but is not the general definition.
Whether an approximation needs explicit evolving body orientation depends on the chosen conditional average.
Reduced seam-configuration information and a defensible empirical or other basis for the conditional response would be needed.

**Maturity:** Originally a possible early addition; now deferred. Experiment II compares B with C or D.
No averaging formula, attitude contract, or detailed rotational compatibility rule is approved.

### C - Harmonic or partially phase-resolved seam model

**Meaning:** A mean aerodynamic response plus selected periodic corrections, retaining some effects that B averages away.
It needs enough phase or orientation information to evaluate the retained corrections and data supporting their coefficients.
An evolving axis can make the phase description more demanding.

**Maturity:** Original implementation/research candidate; now deferred.
Experiment II uses C as a possible comparator to B; Experiment III compares C with D.
Harmonic orders, frequencies, truncation, coefficients, and phase-state contracts remain unselected.

### D - Orientation-resolved empirical model

**Meaning:** Aerodynamic response conditioned on full three-dimensional body orientation and appropriate empirical data, with forces and torques only where supported.
It requires full attitude, a defensible attitude trajectory, and an orientation-dependent response dataset.
The handoff recalls quaternion discussion; it does not approve a representation or evolution contract.
Controlled fixed-axis motion could supply an attitude trajectory without a complete general rigid-body model.

**Maturity:** Originally considered potentially implementable subject to evidence and feasibility; now deferred.
It is a comparator in Experiment II and especially Experiment III.
No empirical force/torque surface, interpolation scheme, or implementation contract is selected.

### E - Geometry-informed surrogate

**Meaning:** Approximate aerodynamic responses from physical geometry, relevant operating conditions, and a defensible calibration or training basis.
It could provide response information to other models rather than replace them in a strict sequence.

**Maturity:** Exploratory and deferred.
Inputs, outputs, geometry representation, surrogate architecture, training data, and rotational coupling are unselected.
No dedicated original protocol or numbered experiment was confidently recovered.

### F - Flow-resolved CFD

**Meaning:** Fluid-dynamics research using specified geometry, flow equations, boundary/initial conditions, and numerical methodology.
It could provide comparison or calibration evidence without becoming a required runtime simulator.
A prescribed-motion CFD study need not include coupled free-flight rigid-body motion.

**Maturity:** Exploratory and deferred.
No mesh, solver, turbulence treatment, software dependency, coupling scheme, or CFD interface is selected.
No dedicated original protocol or numbered experiment was confidently recovered.

### G - Wake-memory extension

**Meaning:** Aerodynamic response depends on evolving flow history beyond instantaneous ball state.
It is naturally an extension of an aerodynamic representation rather than the last rung of a universal accuracy ladder.

**Maturity:** Exploratory and deferred.
Wake-state variables, memory laws, timescales, calibration, and rotational coupling remain unselected.
No dedicated original protocol or numbered experiment was confidently recovered.

## Independent rotational-policy dimension

| Family/policy | Scientific meaning | Current research status |
| --- | --- | --- |
| R0 | Constant world-frame angular-velocity vector | Selected Experiment I control; not a claim of physical constancy |
| R1 | Fixed-axis spin-magnitude evolution | R1-U provisionally preferred; fixed direction is a simplification |
| R1-E | Constant-timescale exponential magnitude decay | Contextual sensitivity alternative; near-stationary evidence is not pitch calibration |
| R1-U | Airspeed-dependent linear damping of the spin vector | Provisional physical reference, with coefficient unresolved |
| R1-M | Fixed-axis aerodynamic moment-coefficient framework | Broader framework; no richer universal constitutive law selected |
| R2 | More general vector torque and possible spin-axis evolution | Deferred; no precession law selected |
| R3 | General rigid-body rotational dynamics, including attitude and possibly nontrivial inertia | Deferred; no body-state/inertia contract selected |

R0/R1 fix the **world-frame spin direction**; neither determines an initial seam pose.
R0 does not prohibit a separately specified attitude trajectory from an initial pose.
R1 magnitude decay does not itself supply full rigid-body attitude dynamics.
Constant angular velocity does not imply constant spin factor or active spin ratio as flight velocity changes.
The [decision register](../../research/aerodynamics/decision-register.md) separates reference selection from calibration and empirical validation.

## Supported compatibility statements and unselected combinations

- A can conceptually consume instantaneous angular velocity from R0-R3. Only R0 and provisional R1-U are selected for the initial investigation; this does not select A/R2 or A/R3 implementation.
- B retains whatever slow dependencies its future conditional average requires. It is not categorically restricted to R0/R1; exact combinations remain unselected.
- C needs the phase information required by its retained corrections. Fixed-axis motion is a possible starting point; evolving-axis combinations remain unselected.
- D needs full attitude even with prescribed fixed-axis rotation. R2/R3 can add rotational complexity, but no complete set of D protocols is approved.
- E/F compatibility depends on the chosen geometry/flow formulation. No general R0-R3 interface or coupling matrix is established.
- G needs a justified history representation and coupling to its underlying model; those combinations remain unselected.

These requirements are qualitative. They are not a universal compatibility matrix or a shared software interface hierarchy.

## Preserved experimental intent

| Experiment | Recovered question and comparison | Horizon |
| --- | --- | --- |
| I - Magnus and torque | A with R0 versus R1: how does spin decay alter trajectory and rotational energy? Later lift/ASR sensitivity complements this objective. | Current scientific investigation; execution not authorized by A2.1 |
| II - Seam averaging | B versus C or D under identical torque assumptions and compatible underlying coefficient data: what is lost by removing phase information? | Deferred |
| III - Orientation sensitivity | C versus D, with R1/R2 where supported: does full orientation add predictive value beyond selected periodic corrections? Begin with fixed axis where appropriate. | Deferred |

The handoff's candidate outputs and detailed experimental controls belong in later A2.2 documentation.
No additional numbered experiment is inferred for E-G.
Neither numerical agreement nor higher resolution alone establishes improved empirical accuracy.

## Carry-forward boundaries

A2.2 will specify Model A mathematics, validity limits, invariants, and Experiments I-III; those specifications are not created here.
A2.3 will integrate reviewed guidance into architecture and existing documentation, including stale Issue #22 publication wording.
No source code, tests, public identifiers, speculative contracts, or implementation scaffolding are authorized by this taxonomy.
The next gate is external A2.1 documentation review.
