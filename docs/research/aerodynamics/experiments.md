# LPB aerodynamic experimental program

> Status: A2.2 documentation independently reviewed and accepted on November 22, 2026. Experimental questions and proposed procedures; no execution or publication authorization. Approval does not constitute empirical validation, final coefficient calibration, approval of new public software contracts, or authorization to implement physics.
> Baseline: [model taxonomy](../../physics/spin/model-taxonomy.md), [Model A mathematics](../../physics/spin/model-a.md), [sources](source-register.md), and [decisions](decision-register.md).

## 1. Provenance, horizons, and kinds of evidence

H02 is the recovered conceptual/historical source for Experiments I-III; it includes paraphrased conversation summaries and reports an original screenshot, not a complete verbatim transcript.
H01 preserves later Model A refinements, and corrected R08 revision 1.1 controls rotational-policy interpretation over superseded R07.
The accepted A2.1 source/decision registers establish provenance and statuses; their stage-specific closure language does not prohibit the subsequently authorized A2.2 drafts.
The present document records that program and proposes procedures needed to make comparisons interpretable; it reports no experimental results.

| Experiment | Recovered original question and primary comparison | Current horizon |
| --- | --- | --- |
| I - Magnus and torque | How much does spin decay alter trajectory and rotational energy? Model A + R0 versus Model A + R1. | Current investigation associated with Issue #7; R1-U is the provisional reference. Not executable in the inspected engine. |
| II - Seam averaging | How much does removing rotational-phase information change predictions? Model B versus C or D under identical torque assumptions. | Deferred; averaging treatment, phase information, and compatible data remain unselected/unavailable. |
| III - Orientation sensitivity | Does full orientation add predictive value beyond selected periodic corrections? Model C versus D with R1/R2 where supported. | Deferred; full attitude and the necessary response dataset/dynamics are not established capabilities. |

Originally A-D were possible early implementations, subject to evidence and feasibility; E-G were primarily exploratory.
Research narrowed the first Issue #7 implementation to A without rejecting the deferred models or Experiments II-III.
No additional numbered experiments, E-G protocol set, or universal compatibility matrix is inferred.

The following labels distinguish the provenance of requirements and suggestions:

- **Recovered original:** Questions, primary comparisons, and candidate diagnostics/controls described in H02.
- **Later retained refinement:** Lift/ASR comparisons and Model A scope in H01; R1-U and deterministic beta scenarios in R08; their provisional status persists.
- **A2.2 procedure proposal:** A proposed pairing, reporting, numerical-check, or validation procedure below. These are reviewable research plans, not recovered archival quotations or approved software behavior.

Three kinds of activity must remain separate:

1. **Mathematical/numerical verification:** Does a numerical realization solve the declared equations and events to justified accuracy? Prescribed constant-speed decay is a reference fixture, not a measured baseball flight.
2. **Scientific sensitivity:** How do declared model hypotheses or coefficients change predicted outcomes under controlled conditions? Such differences do not establish which hypothesis is physically correct.
3. **Empirical validation/calibration:** How well do predictions match appropriately characterized independent observations? Data coverage, sensor uncertainty, model fitting, and withheld evidence matter.

A deterministic, converged simulation can still be empirically inaccurate.
No success criterion below assumes a validated percentage of pitch spin loss or an invented threshold for scientific significance.

## 2. Shared control and reporting principles

**A2.2 procedure proposals, applying the retained reproducibility principles:**

- Resolve each physical case before evaluation: release state, ball properties, gravity, environment, selected coefficient/rotation laws, coefficients, duration/termination intent, numerical settings, and code/model/source versions.
- Hold inputs fixed within a paired comparison except the declared intervention. State explicitly whether a changed law also requires a changed source-version parameter set.
- Use the same field coordinates and internal SI units. Distinguish ball-center height from height above the spherical contact plane.
- Keep common initial conditions fixed when investigating a physical effect. Refitting launch state separately to force endpoints to agree would answer a different question.
- Match nominal timestep, integration method, arithmetic policy, and measurement settings for the primary pair, then study numerical error independently.
- Compare state differences at common elapsed times within both actual completed intervals. Report each trajectory's own contact/maximum separately; terminal differences at different times are different observables.
- Keep numerical rejection or unavailable measurements explicit. A failed event query is not zero difference, no contact, or evidence against the physical law.
- Apply no random draws inside derivatives, RK4, or force/torque evaluations. Current coefficient exploration uses deterministic cases. Any later sampling needs separately justified distributions and retained realized parameters/provenance.
- Treat timing and computational cost as descriptive observations with declared workload, environment, and measurement boundary; this document authorizes no benchmark/comparison/export subsystem.

A **model** is the physical representation; a **policy family** identifies a modeling dimension; a **policy/subpolicy** chooses a formulation; a **law** is its mathematics.
A **protocol** combines compatible model/policy choices; an **experiment** adds questions, cases, controls, measurements, and interpretation.
Thus an ASR prescription is not Model A, and an illustrative protocol string is neither Experiment I nor an approved public identifier (H02; D01-D03/D09).

## 3. Experiment I - Magnus and torque

### 3.1 Original objective and current hypothesis

**Recovered original:** Compare A/R0 with A/R1 to determine how much spin decay alters trajectory and rotational energy.
Track spin as well as translation rather than attempting to identify torque from displacement alone.
**Later retained refinement:** The selected R1 reference is R1-U; R0 keeps a constant world-frame angular-velocity vector.
Nathan saturation with ASR-B is the provisional lift reference, and constant explicit drag remains the baseline (H01; R08; D02-D07).

Competing interpretations include a material rotational-policy sensitivity, an effect smaller than lift/ASR/calibration or numerical uncertainty, and regime-dependent behavior.
None is stipulated as the experimental result.
R0 is a control approximation; it is not an assertion that real torque vanishes.
R1-U is a defensible provisional damping reference; small nonzero aerodynamic damping does not prove its linear form or exact coefficient.

### 3.2 Paired comparison and controlled variables

**A2.2 procedure proposal:** Start from one completely declared case and form a pair differing only in R0 versus R1-U.
Hold fixed initial center position, field velocity, world-frame spin vector, mass/radius/inertia assumption, gravity, air conditions, drag/lift law and mixed-spin prescription, numerical settings, and intended measurement/termination definitions.
For the first physical comparison retain stationary air, constant density, and constant \(C_D\); 0.35 is illustrative, not a Physics default or calibrated universal value.
Use the same nonnegative resolved beta for all reference R1-U cases within a declared comparison; leave it explicit and provisional.

R0 does not use inertia for its lift evaluation, but retain the same declared I when comparing rotational energies so a reporting choice does not change the physical comparison.
R1-U's changing speed must be the instantaneous air-relative speed, not an independently frozen pitch speed.
Constant-speed analytical decay belongs to a prescribed-input verification fixture, not a shortcut for the coupled flight.

Do not assume equal completion times, a unique apex, or existing contact-search validity after lift is introduced.
An initial implementation may need to limit comparisons to a scientifically justified supported domain until event/measurement contracts are resolved; this document does not select that implementation boundary.

### 3.3 Later refinements and candidate case families

These are retained questions, not a compulsory full-factorial matrix or a requirement to implement every comparator:

| Refinement | Comparison/control | Status and interpretive limit |
| --- | --- | --- |
| Lift coefficient | Repeat selected R0/R1-U pairs using Nathan versus SHS or Healey-Wang Hill, holding the mixed-spin convention explicit | Later refinement, H01. Alternative data/fit assumptions are confounders to record; implementation optional. |
| Nonlinear mixed spin | Nathan ASR-A versus ASR-B at the same declared rotational policy | Later refinement, H01/S04. B is provisional; this tests sensitivity, not presumed superiority. |
| Spin geometry | Transverse, mixed, and near-/pure-gyro release cases with controlled magnitude and axis | Later Model A refinement. Initial labels need not describe the whole flight. |
| Damping scale | Deterministic beta control/half/nominal/double examples | Later refinement, R08. No fitted confidence interval or distribution. |
| Duration/regime | Pitch-like paths and, where numerically supported, longer fly-ball/popup paths | Later research refinement, H01/R03/R08. Extrapolation uncertainty grows; no direction of model bias is assumed. |
| Contextual torque alternatives | Selected R1-E sensitivity or a documented R1-M hypothesis | Retained alternatives, not mandatory #7 implementation or matched high-speed calibration. |

R08's example set is beta = 0, 0.0045, 0.009, 0.018.
The zero case is the mathematical R0 control; the other values are deliberate scenarios around an illustrative nominal.
R08's declared constant-95-mph, 0.42 s example gives about 1.42% nominal loss for its chosen mass, radius, density, and homogeneous-sphere inertia.
That conditional calculation is not a measured pitch, an expected result for a decelerating trajectory, or an empirical pass target.

**A2.2 procedure proposal:** Change one family first, then repeat selected comparisons where the earlier result suggests informative sensitivity.
Do not duplicate ASR-A/B runs when the chosen law and complete prescribed geometry make them mathematically identical.
Conversely, an initially transverse or gyro release is insufficient to omit the comparison if gravity/lift turns air-relative velocity.
Use prescribed geometry or show equivalence along the actual path before claiming redundancy.
Spin reversal is a derivative-level sign check; gravity/drag coupling means it is not automatically a symmetric whole-trajectory comparison.

### 3.4 Candidate measurements and actual engine support

H02's original candidate outputs included displacement, plate-crossing position or contact time, axis evolution, rotational energy, force/torque histories, computational cost, and initial-seam-phase variation.
H01/R08 later emphasize spin retention, coupled lift response, lift/ASR sensitivity, and correctness checks.
The output choices below organize those ideas; maximum-height timing, common-time differences, and detailed numerical reporting are A2.2 procedure proposals unless identified otherwise.
Original seam-phase variation belongs to the seam-aware experiments: A has no explicit seam phase to sweep.

| Candidate output | Support in the existing engine at the A2.1 repository baseline | Meaning/prerequisite for Experiment I |
| --- | --- | --- |
| Position, linear velocity, final/actual time, displacement and positional differences | Gravity/drag trajectories and displacement measurements exist; model-aware continuous evaluation exists for those models | A trajectories and common-time comparison are future capabilities; compare only shared completed intervals and declare horizontal versus full displacement. |
| Ground-contact time/location | First-contact termination and contact measurements exist for gravity/drag under their contracts | Model A first-contact/event qualification needs new justification; use ball-center coordinates, shared radius, and each case's own event time. |
| Maximum height and timing | Gravity/drag have specialized optional maximum queries | A needs a justified global-maximum method; do not inherit unique-apex assumptions or infer unrecorded peaks from samples alone. |
| Plate-crossing position/time | No plate-crossing event is implemented in the current contracts | Future scenario must define plane/surface, direction, crossing selection, finite geometry if relevant, and numerical support. No field-axis plane or home-plate offset is selected here. |
| Spin magnitude/vector/direction and retention \(\Omega(t)/\Omega_0\) | Spin is not in current retained physical state/results | Requires an authorized spin-capable realization; retention/direction are undefined when their zero denominators prevent meaningful reporting. R0/R1 direction should remain fixed for nonzero spin. |
| Rotational energy and changes | Not currently reported; scientific definition is \(\tfrac12I\Omega^2\) | Use a declared consistent I; R1-U loss follows the model, not a measured energy budget. |
| Lift/drag force or acceleration histories and ASR/S diagnostics | No retained Magnus/ASR history; baseline drag can be calculated from its explicit inputs, not queried as a new force-history API | Proposed diagnostics require matching state/environment/policy; distinguish undefined diagnostic ratios from well-defined zero force. |
| Torque or decay diagnostics | No spin/torque output | Proposed \(\boldsymbol\tau=I\dot{\boldsymbol\omega}\), decay rate, or integrated damping diagnostics must match R1-U and its sign/reference conventions. |
| Convergence, numerical failures, and computational cost | Existing gravity/drag tests and historical cost observations concern their own models | Proposed comparison measurements must state settings, actual work, and failure scope; no new runtime guarantees or cost threshold is selected. |

Existing outputs remain governed by their [gravity](../../physics/gravity-only-contract.md)/[drag](../../physics/drag-model.md) contexts.
Listing a desired measurement does not approve a new public API, result field, history-retention policy, export schema, or comparison interface.
The current Lab remains an ephemeral debugger, not an already available persistence/export experiment system.

### 3.5 Verification, sensitivity, validation, and success

**A2.2 procedure proposals:**

1. Verify Model A's force direction, zero/gyro/mixed-spin limits, reversal/proper-rotation behavior, deterministic evaluation, and R0 recovery before interpreting trajectory differences.
2. Check R1-U against constant-speed and prescribed variable-speed/density analytical references independently of real-flight coefficient accuracy.
3. Use timestep refinement or a sufficiently independent numerical reference to examine translation/spin and each supported event measurement. Record when numerical uncertainty prevents resolving a physical-policy difference.
4. Run the controlled R0/R1-U comparison and selected lift/ASR/beta sensitivities, reporting both small and large differences without requiring a nonzero effect on every output.
5. Reserve empirical agreement claims for appropriately matched observed flights with independently characterized spin, construction/inertia, environment, release state, sensor errors, and force-model assumptions; distinguish calibration data from validation data.

Success means the case, intervention, equations, numerical limits, supported measurements, provenance, and unresolved interpretations are reproducible and clear.
A null or unresolved difference is informative when reported honestly; no minimum displacement, spin-loss percentage, or computational speed is required.
R1-U must show its mathematical dissipation and fixed-axis behavior within justified numerical criteria; this does not settle its beta.

Major confounders are lift-law and ASR choice, drag, density, initial spin/axis uncertainty, inertia, seam-mediated force included in coefficients, ball variability, torque calibration, integration error, event incompleteness, and comparing endpoints at different times.
Trajectory-only residuals can trade off these explanations; they do not uniquely identify a damping coefficient.
Pure-gyro damping is an assumption of R1-U and should be labeled as such, not used as proof that gyrospin torque was measured.
Long flights are sensitivity investigations outside the best-supported short-flight regimes, not automatic validation of batted-ball spin decay.

**Dependencies/horizon:** Issue #7 introduces the physical capability only after separate authorization and numerical/event analysis; #10 covers broader numerical studies, #11 empirical calibration/uncertainty.
Atmosphere/wind remain #8/#9, and general comparison/export infrastructure remains separately scoped.
Revisit the reference when better matched free-flight spin data, apparatus corrections/inertia, regime-dependent discrepancies, or sufficiently discriminating mixed-spin observations warrant it (D02-D06/D10).

## 4. Experiment II - Seam averaging

### 4.1 Recovered objective, control, and Model B meaning

**Status: Deferred.**
**Recovered original:** B versus C or D under identical torque assumptions, asking how much removing rotational-phase information changes predictions (H02; D09).
Use scientifically compatible underlying coefficient data and matched inputs, so unrelated fits/calibrations do not masquerade as an averaging effect.

Model B performs **conditional averaging over selected rapid phase effects while retaining physically relevant slower orientation, configuration, or rotational dependencies**.
It does not universally average every seam effect over one full revolution.
The averaging operator, relevant periods, retained slow variables, and state requirements remain unselected; a full-revolution policy would be one possible future choice, not B's definition.
Multiple periods or changing slow variables may matter, and useful fast/slow separation must be justified rather than assumed.

The competing explanations are that a suitable conditional average retains the observables of interest, that discarded phase affects them materially, or that apparent differences arise from unmatched data/torque/initial conditions or numerical error.
No direction or quantitative threshold of averaging error is stipulated.

### 4.2 Prerequisites and proposed comparison procedure

**Recovered/retained controls:** Match torque assumptions and underlying aerodynamic information; deliberately vary initial seam phase where relevant.
**A2.2 procedure proposals:**

- First identify the common empirical/model evidence, operating regime, seam configuration, and the reduced/phase-resolved variables each side can legitimately represent.
- Specify later which rapid phases are averaged, which slow variables are conditioned on, and the periods/time-scale assumptions; those choices are prerequisites, not selected operators in this document.
- Match translation, spin, environment, geometry, retained configuration, numerical settings, and measurement definitions. Use consistent force/torque information and baseline decomposition.
- Describe how a phase-resolved initial state maps to B's retained information. B need not distinguish all initial rapid phases, while its retained slow configuration must still match.
- Use declared deterministic phase variations or another separately justified comparison design. Do not invent a probability distribution for seam phase merely to obtain an average.
- Distinguish a conditional mean force at fixed state from the mean of integrated trajectories or nonlinear observables; they need not commute. Select the statistic and averaging window later, before interpreting error.
- Demonstrate numerical resolution of retained periodic effects and convergence of the chosen comparison statistic before attributing discrepancies to B.

Candidate diagnostics retain H02's displacement, event positions/times where supported, spin/axis, rotational energy, force/torque histories, cost, and initial-phase dependence.
Additional proposed reporting could compare conditional means, phase-dependent spread, or discarded temporal variation, but no statistical measure, phase grid, period, or error threshold is fixed.
All outputs require suitable event/evaluation contracts; seam-aware force/phase histories do not currently exist.

### 4.3 Limits, success, and reopening

Predicted differences isolate a declared averaging treatment only to the extent data, torque, retained slow state, geometry, and numerical errors are controlled.
Neither a smooth B path nor agreement with a phase-averaged simulation proves empirical validity.
If a conditional average cannot be justified from the dynamics/data, record that failure of the approximation's prerequisites rather than choose a universal revolution by convenience.
Sensitivity to uncertain coefficients must be examined without changing them inconsistently across the pair; the averaging question and calibration uncertainty are distinct.

Success would be a reproducible comparison with justified conditioning, compatible data, resolved numerical error, appropriate observations/statistics, and candid limits on information loss.
Empirical superiority would additionally require suitable independent measurements.
The experiment remains deferred until enough phase/orientation-conditioned response data, a defensible averaging definition, compatible rotation/phase evolution, and measurement machinery are available.
No harmonic order, coefficient surface, phase/attitude contract, or implementation interface is selected.
Reopen B/C/D scope only when those prerequisites and a scientific accuracy need justify it; current A implementation does not require scaffolding them.

## 5. Experiment III - Orientation sensitivity

### 5.1 Recovered objective and staged comparison

**Status: Deferred.**
**Recovered original:** C versus D with R1/R2 where supported, asking whether full orientation adds predictive value beyond selected periodic aerodynamic corrections (H02; D09).
First examine fixed-axis motion where scientifically appropriate; consider evolving-axis motion only when both models, torque assumptions, data, and numerical methods support it.
Initial seam phase and changing rotational-axis direction are distinct interventions and must not be conflated.

C needs the information required by its selected periodic corrections; D needs full body orientation and an appropriate empirical response.
R1 supplies fixed-axis magnitude evolution, not an initial body pose or a complete attitude trajectory by itself.
R2 and its vector torque/axis dynamics are not implemented or selected contracts; R3/full rigid-body dynamics are not an automatic prerequisite for every controlled fixed-axis comparison.
No general compatibility matrix is approved by naming R1/R2 here.

### 5.2 Data, controls, and proposed procedures

Competing explanations are that selected periodic corrections capture the observable response adequately, that omitted orientation dependence matters, or that apparent improvement comes from different calibration, added fitting freedom, noise, or numerical resolution.
Neither the richer representation nor a smaller training residual demonstrates better predictive accuracy.

**A2.2 procedure proposals:**

- Establish compatible C/D coefficient evidence and regime coverage, with a declared reduction from full orientation to C's retained periodic information; the reduction itself remains unselected.
- Supply a common physical initial pose/seam configuration, translation, spin vector, environment, geometry/inertia assumptions, torque treatment, numerical settings, and supported event definitions.
- Begin with matched fixed-axis orientation trajectories and controlled initial-phase variations. Do not infer absolute seam orientation from spin alone or claim that the current engine already propagates attitude.
- Only after that comparison is interpretable, propose axis-evolution cases under a justified common R2 treatment where supported; keep the phase intervention separate.
- Examine trajectory/event, spin/energy, force/torque, phase dependence, and cost diagnostics recovered in H02. Orientation-conditioned residuals and calibration/validation splits are additional A2.2 proposals.
- Distinguish disagreement between simulated representations from predictive value against independent orientation-resolved observations. Any later fitting must separate calibration and evaluation evidence.

Required information includes full initial attitude for D, C's retained phase definition, a justified orientation-evolution method, compatible empirical force/torque data, and sufficient temporal/orientation resolution.
An arbitrary-attitude dataset, full rigid-body solver, reliable precession law, and common phase-state contract are not currently available LPB capabilities.
Numerical convergence of both representations and robust event analysis are prerequisites for attributing a difference to orientation information.
No harmonic order, quaternion contract, empirical interpolation method, torque surface, or software interface is selected here.

### 5.3 Limits, success, and reopening

Initial phase, axis evolution, seam construction, empirical interpolation, incomplete orientation coverage, and unequal fitting freedom can confound the comparison.
Hold uncertain coefficients and shared physical assumptions consistently across a comparison or declare their independent sensitivity.
A D prediction is not automatically closer to reality, and no minimum improvement or confidence claim is invented.
Success would require a reproducible, controlled comparison that identifies what added orientation information predicts, its numerical uncertainty, and the evidence needed to assess empirical value.

The implementation horizon remains after Model A and after adequate orientation-conditioned data, compatible rotational evolution, and measurement support become active requirements.
Revisit when those data/capabilities are sufficient to test a material accuracy need, or when simpler representations show reproducible, regime-specific validation failures.
Otherwise preserve the question without implementing speculative attitude/phase/R2/R3 infrastructure.

## 6. Common numerical and interpretation gates

The [Model A event audit](../../physics/spin/model-a.md) explains why gravity/drag assumptions do not automatically carry over.
Before claiming Model A contact/maximum results, later work must justify vertical sign changes, possible multiple extrema/tangencies, earliest-contact completeness, localization evidence, complete-state consistency, and model-aware continuous evaluation.
The existing gravity-only certificates and drag-only monotonicity/reachability policies remain in force for their own models; they are not generic lift certificates.

Every future experiment must declare:

1. The historical objective, retained refinements, and new procedure choices actually used.
2. Which hypotheses are compared, which parameters are held fixed, and which outcomes could have competing explanations.
3. The input regime, data provenance, source/fit versions, physical assumptions, numerical settings, and actual termination.
4. Supported versus unavailable candidate measurements, their coordinate/time interpretation, and numerical failure handling.
5. Verification/reference evidence separately from sensitivity outputs and empirical calibration/validation evidence.
6. Deterministic uncertain-coefficient cases, their rationale, and any justified later sampling provenance; a scenario range is not a confidence interval.
7. Numerical resolution of the claimed effect, empirical applicability limits, and conditions that would change the conclusion or reopen deferrals.

No complete protocol matrix, public identifier/schema, parameter distribution, comparison engine, or export subsystem is approved here.
The source register's restricted/unrecovered primary material remains a limitation: current bibliography access does not supply missing raw calibration or orientation data.
The original three experiments remain preserved even where execution is deferred.

## 7. Source trace and next review

- H02 sections 5-6: original questions, comparisons, diagnostics, initial-phase controls, and historical scope narrowing.
- H01 sections 3/5/7: later Model A, lift/ASR and rotational comparisons, verification distinctions, and deterministic evaluation.
- R08 sections 3-8: corrected R1-U equations, illustrative beta cases, uncertainty boundaries, analytical verification, and reopening criteria; R07 is superseded.
- S01-S04/S15: lift/ASR law provenance and alternatives; S06/S16-S18: seam/orientation limitations and future comparison motivation.
- S09-S12: supported-ball and bearing-free torque evidence, calibration limitations, and differing low-/high-speed regimes; D01-D10 preserve the associated choices and statuses.

No experiments were executed or empirical results produced in A2.2.
The next action is independent scientific/documentation review of these two drafts.
A2.3, implementation, staging/commit, Git publication, and GitHub modifications are outside this authorization.
