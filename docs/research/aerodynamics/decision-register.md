# Issue #7 aerodynamics decision register

> Status: A2.1 documentation reviewed and accepted on November 22, 2026. Records research decisions with their existing provisional qualifications. This approval does not constitute empirical validation, adoption of provisional coefficients, or authorization to implement the models.
> Related: [source register](source-register.md), [model taxonomy](../../physics/spin/model-taxonomy.md), and [current architecture](../../architecture.md).

## Reading the decisions

Dates below identify the established research stage; exact earlier decision dates are not inferred when unrecovered.
**Adopted** describes a current scope or established principle, not newly merged physics.
**Provisional reference** is a selected working hypothesis; **scientific comparator** is retained for comparison without reference status.
**Proposed**, **deferred**, **superseded**, and **unresolved** preserve distinctions between suggestions, future work, historical corrections, and missing evidence.
Source keys S01-S18, H01/H02, and R01-R08 resolve in the source register; they distinguish primary evidence from LPB synthesis.
No numerical verification result is empirical validation, and no reference selection makes a coefficient a universal constant.

## D01 - Model A first

- **Decision/status:** Adopted scope: only Smooth Magnus A is selected for the initial [Issue #7](https://github.com/skolins3651/lets-play-baseball/issues/7) aerodynamic implementation. B-D are deferred; E-G remain exploratory and deferred.
- **Stage/evidence:** Original November 13-14 taxonomy, subsequent research through November 22, and current A2.1 authorization; H01/H02, R01-R06, and S02/S16 delimit the evidence.
- **Rationale:** Establish an interpretable empirical baseline before spending complexity on insufficiently calibrated seam/attitude/history models.
- **Alternatives:** The original possible early implementation of A-D; direct orientation-resolved or geometry/flow approaches.
- **Not established:** A does not contain all real-baseball lift, and choosing A does not show that omitted physics is absent or unimportant in every regime.
- **Dependencies/revisit:** #7 and [#11 empirical validation](https://github.com/skolins3651/lets-play-baseball/issues/11). Revisit scope when usable data and material prediction errors justify a scoped extension; future compatibility alone does not justify scaffolding.

## D02 - Nathan saturation as the lift reference

- **Decision/status:** Provisional reference: Nathan's saturating transverse-spin fit. SHS bilinear and Healey-Wang Hill are scientific comparators/alternatives, not required implementations in this batch.
- **Stage/evidence:** Model A research recorded in H01/H02 through November 22; S01-S04 and S15; R02 distinguishes 2008 measurements from the 2020 fit.
- **Rationale:** Smoothness, zero-spin behavior, saturation, and explicit selected-data fit provenance provide a useful compact baseline.
- **Alternatives:** SHS's historical bilinear synthesis; the differently inferred Hill fit; future refitting to suitable data.
- **Not established:** Unique physical correctness, seam independence, universal saturation, exact parameter precision, or superiority over every alternative. No confidence interval for 0.336/6.041 is invented.
- **Dependencies/revisit:** #7, #11, and [#10 numerical studies](https://github.com/skolins3651/lets-play-baseball/issues/10). Revisit using matched empirical comparisons, domain coverage, fit-version reconciliation, and material trajectory sensitivity.

## D03 - Mixed-spin ASR-B and ASR-A

- **Decision/status:** ASR-B is the provisional reference; ASR-A is an essential scientific comparator. For the chosen transverse curve f, B uses `f(S ASR)` and A uses `f(S) ASR`.
- **Stage/evidence:** Later Model A selection in H01/H02; Nathan's S04 gives both prescriptions and their unresolved discrimination.
- **Rationale:** Choose one explicit, revisable nonlinear mixed-spin hypothesis so comparisons are reproducible while preserving the alternative.
- **Alternatives:** ASR-A; a different empirically supported three-dimensional lift relation if recovered later.
- **Not established:** No experimentally proven ASR-B superiority is claimed. Pure-transverse agreement cannot discriminate mixed-spin laws; seam-mediated movement complicates inference from total displacement.
- **Dependencies/revisit:** #7/#11 and D02/D09. Revisit with matched measurements of full spin vector, trajectory, environment, and separable non-Magnus contributions. Mathematical equivalence in limiting cases is not independent validation.

## D04 - R0 control and R1-U provisional physical reference

- **Decision/status:** R0 is adopted as Experiment I's control. R1-U is the provisional physical reference: fixed world-frame spin direction with airspeed-dependent linear damping of magnitude.
- **Stage/evidence:** Research evolved from R03-R05 sensitivity recommendations to R06's free-flight corroboration and R08 revision 1.1; H01/H02 retain this selection. S09-S11 support damping and approximate scale.
- **Rationale:** Use a small nonzero damping model supported by baseball evidence, with one empirical parameter commensurate with calibration resolution. Preserve R0 to isolate the effect of adding decay.
- **Alternatives:** R0 alone, constant-timescale R1-E, or a richer R1-M constitutive law.
- **Not established:** Exact linear dependence, universally fixed axes, gyrospin-specific damping magnitude, or quantitative long-flight validity. The selected law predicts zero damping at zero airspeed, whereas S12 shows damping near stationary translation.
- **Dependencies/revisit:** #7/#10/#11, D05/D06. Revisit for better high-speed free-flight data, characterized mechanical bias/inertia, systematic fly-ball mismatch, or demonstrated material seam/axis dependence.

## D05 - R1-E and R1-M remain distinct alternatives

- **Decision/status:** R1-E is a scientific sensitivity alternative, not the pitch reference or a mandatory #7 implementation. R1-M is the retained broader moment framework; a richer constitutive policy is deferred.
- **Stage/evidence:** R03-R08; S12's near-stationary experiment, S13's heuristic, and S09-S11's diameter-normalized moments.
- **Rationale:** Keep experimentally and mathematically different hypotheses visible without claiming that their calibrations apply to the same regime. R1-U is obtainable from a moment coefficient locally proportional to spin factor.
- **Alternatives:** Full published signed interpolation, a new calibrated coefficient surface, or a justified future low-/high-speed formulation.
- **Not established:** Koss's approximately seven-second timescale is not pitch calibration or an empirical error bound on R1-U. The 2019 quadratic is an interpolation, has unsuitable all-domain limits, and is not uniquely selected.
- **Dependencies/revisit:** #7/#11 and D04/D06. Revisit when richer moment dependence can be independently identified or low-speed accuracy becomes an active requirement; R2/R3 remain deferred.

## D06 - Beta remains uncalibrated at final precision

- **Decision/status:** `beta ≈ 0.009` is an illustrative provisional nominal candidate under R08's nonnegative damping-magnitude convention; final numerical calibration and any uncertainty distribution are unresolved.
- **Stage/evidence:** R08, section 4, following R06 and S11 equation 16. The local ratio is approximately 0.00882 near a declared 95 mph/2,500 rpm example with radius 0.0366 m, rounded to a convenient trial value.
- **Rationale:** Supply a transparent local scale for future controlled scenarios without implying more calibration information than the evidence contains.
- **Alternatives:** Refit with recovered apparatus/raw data; explicitly labeled deterministic sensitivity multipliers; later justified inference of regime/ball-dependent parameters.
- **Not established:** A measured universal constant, confidence interval, posterior, normal/uniform distribution, or nonzero lower calibration bound. R05's 0.005-0.007 graph-read range is not an independent statistical interval.
- **Dependencies/revisit:** #10/#11, D04/D05/D08. Revisit even if the functional law survives, before declaring M1 coefficients settled; account for ball/assembly inertia, density, geometry, mechanical bias, and measurement error.

## D07 - Retain constant drag for the initial investigation

- **Decision/status:** Adopted current baseline: retain configurable constant-C_D drag, including illustrative `C_D = 0.35`. Drag refinement is deferred research, not a prerequisite to Experiment I.
- **Stage/evidence:** Existing [drag contract](../../physics/drag-model.md), H01, and current A2.1 authorization; S01/S16/S17 show why universality would be unjustified.
- **Rationale:** Hold a known baseline while isolating lift and rotational effects; avoid changing multiple uncertain coefficient laws simultaneously.
- **Alternatives:** Reynolds-, spin-, construction-, or seam-dependent drag; joint coefficient calibration when evidence supports it.
- **Not established:** A universal calibrated drag value. Physics requires caller-supplied parameters and has no implicit default; 0.35 is the Lab's illustrative form value. The H01 wording "default" must not change that contract.
- **Dependencies/revisit:** #7/#11 and later [#8 atmosphere](https://github.com/skolins3651/lets-play-baseball/issues/8)/[#9 wind](https://github.com/skolins3651/lets-play-baseball/issues/9). Revisit when matched drag evidence materially affects conclusions; compatibility with future air-relative inputs does not authorize implementing those workstreams now.

## D08 - Keep the numerical kernel deterministic

- **Decision/status:** Adopted principle: no random sampling inside derivative evaluations or RK4. Stochastic coefficient modeling is deferred.
- **Stage/evidence:** Current architecture's stochastic-parameter boundary, existing immutable explicit parameters, H01, and R08 section 6.
- **Rationale:** A resolved physical case must have reproducible dynamics and numerical comparisons. Parameter uncertainty, physical heterogeneity, and time-dependent variability are different modeling questions.
- **Alternatives:** Later external parameter sampling, deterministic scenario grids now, or separately specified physical-event sampling if justified and authorized.
- **Not established:** No probability distribution, coefficient covariance, draw frequency, RNG choice, or stochastic infrastructure is selected; integrator noise does not represent calibration uncertainty.
- **Dependencies/revisit:** #10/#11 and D06/D07. Future sampling must preserve exact realized parameters and provenance of the model/calibration, sampling procedure/version, RNG/version, seed/state and stream, event/scope, and code. A seed alone is insufficient; detailed storage schemas remain deferred.

## D09 - Preserve all three experiments

- **Decision/status:** Experiment I is the current scientific investigation; Experiments II-III are deferred and preserved. Their execution and detailed specification are outside A2.1.
- **Stage/evidence:** Original experimental program as recovered in H02, carried forward in H01; later lift/ASR comparisons supplement I's original objective.
- **Rationale:** I compares A/R0 with A/R1 to investigate trajectory and rotational energy. II compares B with C or D under identical torque assumptions and compatible underlying data to isolate averaging. III compares C with D, using R1/R2 where supported, to test added orientation information.
- **Alternatives:** Earlier broader near-term A-D program or a larger comparison matrix; neither is currently selected. Start fixed-axis comparisons where appropriate before adding precession.
- **Not established:** No new experiment numbers, calibrated expected trajectory differences, complete measurements, or implementation protocol matrix are inferred from the names.
- **Dependencies/revisit:** #7/#10/#11; later comparison/export work remains separately scoped. A2.2 must retain original objectives and distinguish later proposed measurements. Revisit II/III when their coefficient data, phase/attitude treatment, and controls can be specified defensibly.

## D10 - Complexity follows identifiable evidence

- **Decision/status:** Adopted modeling discipline: a simpler provisional formulation may be selected when added complexity cannot be calibrated or experimentally discriminated. Each concrete simplification needs its own rationale and reconsideration criteria.
- **Stage/evidence:** Current authorization and H02; R02/R06/R08 demonstrate missing fit/torque information alongside evidence of real physical effects.
- **Rationale:** Additional terms without identified parameters can create apparent precision. Scientific defensibility and experimental validation are distinct standards.
- **Alternatives:** Richer nonlinear/state-dependent models when justified; sensitivity comparisons where data cannot choose uniquely.
- **Not established:** Simplicity is not proof of higher predictive accuracy or absence of omitted physics. A small modeled pitch effect does not establish insignificance over multi-second fly-ball flight.
- **Dependencies/revisit:** #7/#11, D01/D04/D09. Revisit each simplification when its effects exceed the intended accuracy needs, new data identify the extra parameters, or controlled model comparisons reveal material systematic error.

## D11 - Correct the R1 historical record

- **Decision/status:** R07 is superseded by R08 revision 1.1, dated November 22, 2026. Retain R07 solely for correction history; earlier recommendation stages do not override the later selected reference.
- **Evidence:** R08 Appendix A, with primary corroboration in S11's regime, Tables 4-6, and signed interpolation; R06 exposes the apparatus limitations.
- **Corrections:** The free-flight speed/spin figures were misstated in R07; damping magnitudes must be distinguished from signed moments; its 0.8% example is arithmetically wrong; R1-E does damp at zero airspeed; the R1 direction is fixed in the world frame, not a defined seam/body pose; its mph conversion was wrong.
- **Limits:** Fly-ball extrapolation bias has no established direction; not all individual reconstructed trials show positive loss; beta was not finalized. R08's constant-speed spin-loss examples are conditional model calculations, not measured pitches.
- **Rationale/alternatives:** Preserve a checkable correction trail instead of merging contradictory numbers or averaging incompatible claims. The original statements are not eligible current alternatives.
- **Dependencies/revisit:** #7/#10/#11 and D04-D06. Further corrections require source-specific evidence and explicit revision history; no return to the original numerical statements without such evidence.

## D12 - Do not canonize an unverified Alaways torque law

- **Decision/status:** Alleged Alaways moment-law attribution is unresolved; no Alaways-derived numerical spin-down policy is selected.
- **Stage/evidence:** R04 after R03's initial lead; S05/S06/S14. The recoverable journal methodology fits constant angular velocity, while Kimura cites an unspecified coefficient to the incompletely recovered dissertation.
- **Rationale:** A later citation and a dimensional framework cannot supply missing original measurements, constants, signs, or calibration.
- **Alternatives:** Recover the archival dissertation or author clarification; use the independently traceable S09-S11 torque evidence with explicit limits in the meantime.
- **Not established:** This does not prove the dissertation has no separate moment discussion, nor prove Kimura obtained data from Naruo. Its printed sign ambiguity cannot be silently repaired and attributed as a verified law.
- **Dependencies/revisit:** #7/#11 and D04/D05. Revisit on primary equation/table recovery, demonstrable data lineage, and sign/reference-length reconciliation; unresolved attribution does not block provisional R1-U documentation.

## D13 - Preserve current numerical contracts and stage integration separately

- **Decision/status:** Adopted A2.1 scope: three standalone documentation drafts only. Model A equations/experimental specifications are deferred to A2.2; repository-wide integration is deferred to A2.3; executable design/implementation needs separate authorization.
- **Evidence/stage:** Current authorization; merged baseline `0cfcf23cbd33483d4f09ded7340ae3b3d48e9a75`, [PR #24](https://github.com/skolins3651/lets-play-baseball/pull/24), current [gravity](../../physics/gravity-only-contract.md)/[drag](../../physics/drag-model.md) contracts.
- **Rationale/alternatives:** Keep reviewable batches and retain established regressions and numerical guarantees. Immediate integration or speculative common interfaces would exceed this batch.
- **Not established:** Existing gravity/drag concavity, single-apex, contact, and event-sign assumptions do not automatically extend to lift. A2.2 must assess them; no replacement is designed here.
- **Dependencies/revisit:** #7 follows completed [#22](https://github.com/skolins3651/lets-play-baseball/issues/22). Existing wording calling #22 unpublished is stale: it was merged through #24; correcting existing files remains A2.3 work. Advance only through the authorized review gates.

## Deliberate deferrals and review gate

Still deferred: B-G mathematical/implementation contracts; R2/R3 and full attitude; precise lift/torque calibration and uncertainty distributions; advanced drag; full atmosphere/wind; low-speed torque unification; detailed experiment protocols and outputs; public policy identifiers/configuration schemas; stochastic/persistence infrastructure; and visualizer/export changes.
Restricted or unrecovered primary sources and apparatus calibration gaps remain visible in the source register; they limit scientific claims but do not block this bounded documentation draft.
External A2.1 review is next. These files remain local and uncommitted; no A2.2/A2.3 work, code changes, publication, or GitHub modifications are authorized by their existence.
