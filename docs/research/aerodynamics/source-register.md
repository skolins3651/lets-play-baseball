# Issue #7 aerodynamics source register

> Status: A2.1 documentation reviewed and accepted on November 22, 2026. Bibliography and evidence map, not an adopted scientific specification. This approval does not constitute empirical validation, adoption of provisional coefficients, or authorization to implement the models.
> Related: [decisions](decision-register.md), [model taxonomy](../../physics/spin/model-taxonomy.md), and [current architecture](../../architecture.md).

## Authority and verification vocabulary

Merged code and adopted contracts govern implemented behavior; explicit approved decisions govern design.
Original publications support scientific claims within their measured or modeled scope.
LPB audits interpret sources and expose limitations; they are not additional experiments.
Historical handoffs establish conceptual intent without establishing executable contracts.
The historical A2.1 authorization selected working references while retaining their provisional scientific status.

**Direct A2.1** means relevant primary text, equations, tables, or figures were inspected during this drafting pass; it does not mean raw data were reprocessed or every assertion independently replicated.
**Archive verification** means a supplied LPB audit reports primary inspection; that claim is attributed to that audit, not presented as new direct access here.
**Metadata/lead only** means bibliographic identification without sufficient primary text to verify detailed claims.
Access failures describe this pass, not permanent unavailability.
References use APA 7 style; S/H/R keys are local document navigation aids, not public model identifiers.

## Actual input inventory and correction order

All eight supplied reports were read (79 PDF pages total), together with H01/H02 and the A2.1 authorization.
Three OneDrive originals initially remained cloud-only and timed out; readable copies of all eight reports and both handoffs were obtained from attachments in the existing chat **LPB - Implementation & Codex**.
After the original folder downloaded, SHA-256 comparison confirmed that all eight inspected PDF copies exactly match the user-named originals.
Original archive filenames are recorded below; attachment copies have incidental numeric suffixes.
The portable review package records the exact inspected copies and their hashes without embedding machine-specific paths in repository guidance.
No new historical quotation-recovery exercise was performed.

| Key | Supplied filename | Pages | Role and current authority |
| --- | --- | ---: | --- |
| R01 | LPB Issue #7 — Deep Literature Review of the Baseball Lift Coefficient _(C_L_).pdf | 12 | Initial lift synthesis; source-access limitations and early candidate recommendations are historical |
| R02 | LPB Issue #7 — Primary-source audit of the SHS and Nathan lift-coefficient laws.pdf | 10 | Refines fit provenance and separates Nathan 2008 measurements from the 2020 fit |
| R03 | LPB Issue #7 — Rotational Dynamics, Aerodynamic Spin-Down, and R0_R1 Readiness.pdf | 16 | Initial rotational review; its pre-Naruo evidence picture and sensitivity-only gate are historical |
| R04 | LPB Issue #7 — Primary-Source Audit of the Alaways Aerodynamic Moment _ Spin-Decay Attribution.pdf | 10 | Audits the unverified Alaways attribution; identifies the Naruo lead |
| R05 | LPB Issue #7 — Primary-Source Audit of Naruo et al. and Baseball Aerodynamic Spin-Down Torque.pdf | 10 | Establishes high-speed supported-ball evidence; graph-derived numbers and its earlier readiness gate need later context |
| R06 | LPB Issue #7 — Bearing-Friction and Apparatus Audit of Naruo-Group Baseball Spin-Down Measurements.pdf | 11 | Adds S11 free-flight corroboration and the published interpolation; apparatus calibration remains unresolved |
| R07 | R1 Spin-Decay Decision (Issue #7).pdf | 4 | **Superseded** original; correction-history evidence only |
| R08 | LPB Issue 7 - R1 Spin Decay Decision Record (Corrected).pdf | 6 | **Controlling corrected research record, revision 1.1, November 22, 2026**; provisional policy, not implementation authorization |

The reports form a succession of research stages, not eight independent scientific experiments or eight approved specifications.
R08 supersedes R07 in full for current R1 interpretation; R07 must not be used to restore corrected claims.
Individual primary-access differences between R01/R02 and R04 reflect later recovery, not contradictory experimental results.

No named individual author or reliable publication date is supplied for R01-R07; do not infer dates from filesystem timestamps.
Their anonymous unpublished-report references are given here by title, using n.d.; R keys above map them to exact files.

- R01: *LPB Issue #7 — Deep literature review of the baseball lift coefficient (C_L)*. (n.d.). [Unpublished LPB research report].
- R02: *LPB Issue #7 — Primary-source audit of the SHS and Nathan lift-coefficient laws*. (n.d.). [Unpublished LPB source audit].
- R03: *LPB Issue #7 — Rotational dynamics, aerodynamic spin-down, and R0/R1 readiness*. (n.d.). [Unpublished LPB research report].
- R04: *LPB Issue #7 — Primary-source audit of the Alaways aerodynamic moment / spin-decay attribution*. (n.d.). [Unpublished LPB source audit].
- R05: *LPB Issue #7 — Primary-source audit of Naruo et al. and baseball aerodynamic spin-down torque*. (n.d.). [Unpublished LPB source audit].
- R06: *LPB Issue #7 — Bearing-friction and apparatus audit of Naruo-group baseball spin-down measurements*. (n.d.). [Unpublished LPB source audit].
- R07: *R1 spin-decay decision (Issue #7)*. (n.d.). [Superseded unpublished LPB decision report].
- R08: *LPB Issue #7 — R1 spin-decay decision record: Corrected research closeout / provisional Model A policy* (Rev. 1.1). (2026, November 22). [Unpublished LPB research record].

## Planning and historical sources

### H01 - Phase A1 handoff

*LPB — Model A documentation-first handoff*. (n.d.). [Unpublished project handoff].

Direct A2.1 access to the supplied handoff text; it records provisional Nathan/ASR-B, R1-U, constant drag, and the three experiments.
Its earlier read-only planning instructions describe A1, not the subsequent A2.1 drafting authorization.
Its phrase "C_D = 0.35 default" is qualified by the implemented contract: an illustrative Lab form value, not an implicit Physics default.

### H02 - Recovered taxonomy

*LPB Issue #7 — Recovered model taxonomy and protocol definitions*. (n.d.). [Unpublished archival reconciliation].

Direct A2.1 access. Stated historical baseline: November 13-14, 2026; subsequent decisions through November 22.
Reconstructs A-G, R0-R3, terminology, and Experiments I-III from summaries and later material, partly paraphrased.
It reports a screenshot as the direct source for the original experiments; that screenshot was not separately supplied or re-audited here.
H02 controls conceptual history, especially B's conditional averaging and the narrowing from possible A-D implementation to A first; it does not establish detailed equations or APIs.

## Primary scientific sources and evidence map

### S01 - SHS bilinear lift synthesis

Sawicki, G. S., Hubbard, M., & Stronge, W. J. (2003). How to hit home runs: Optimum baseball bat swing parameters for maximum range trajectories. *American Journal of Physics, 71*(11), 1152–1162. [https://doi.org/10.1119/1.1604384](https://doi.org/10.1119/1.1604384)

- **Type/access:** Primary modeling article synthesizing earlier experiments; direct A2.1 text/figure context via the [university-hosted copy](https://baseball.physics.illinois.edu/AJP-Nov03.pdf), corroborating R02.
- **Law:** Bilinear lift: `C_L = 1.5 S` below `S = 0.1`, and `C_L = 0.09 + 0.6 S` above it; both give 0.15 at the join.
- **Provenance:** Figure 4 combines Watts/Ferrer, Alaways/Hubbard, and then-unpublished Sikorsky/Lightfoot observations. This is a fitted synthesis, not a new torque or lift experiment establishing the constants independently.
- **Limits:** Mixed speed, spin, and seam regimes; explicitly neglects low-spin seam-orientation differences. R02 located no parameter confidence intervals or reproducible regression objective for the bilinear constants.
- **LPB use:** Historical lift comparator; moderate-spin agreement does not validate the kink or unlimited high-spin extrapolation.

### S02 - Nathan 2008 lift measurements

Nathan, A. M. (2008). The effect of spin on the flight of a baseball. *American Journal of Physics, 76*(2), 119–124. [https://doi.org/10.1119/1.2805242](https://doi.org/10.1119/1.2805242)

- **Type/access:** Primary free-flight experiment; direct A2.1 [article](https://baseball.physics.illinois.edu/ajpfeb08.pdf), with detailed method/error audit in R02.
- **Measured/inferred:** Marker positions and flight information; lift/drag coefficients inferred by dynamical least-squares fitting, not a force-balance reading. Twenty-two two-seam pitches, nominal transverse topspin/backspin, approximately 5 m tracked at 700 frames/s.
- **Regime:** 50-110 mph, 1,500-4,500 rpm, `S = 0.090-0.595`, `Re = (1.1-2.4) × 10^5`.
- **Supports:** Rising lift with spin factor and agreement with SHS at moderate S. Weak extra speed dependence was observed in a restricted spin-factor band, not established globally.
- **Limits:** Small sample, one seam configuration, assumed transverse geometry, and constant fitted coefficients over the short observed segment. Per-point error estimates are not a global coefficient-law confidence interval.
- **LPB use:** Empirical foundation for smooth lift. The later constants 0.336 and 6.041 are not the principal law published by this experiment.

### S03 - Nathan 2020 saturating fit

Nathan, A. M. (2020, May 25). *Determining the 3D spin axis from Statcast data* (Updated version) [Unpublished technical note]. University of Illinois Urbana-Champaign. [Technical note](https://baseball.physics.illinois.edu/trackman/spinaxis.pdf)

- **Type/access:** Primary author fit/method note, not a new experimental dataset; direct A2.1 relevant fit and uncertainty passages, audited in R02.
- **Law/provenance:** `f(S) = 0.336[1 - exp(-6.041 S)]`, nonlinear least-squares fit to selected Alaways/Nathan transverse-spin motion-capture data, described near 80-100 mph; selected datasets exclude some noisier tracking and low-speed tunnel results.
- **Correction:** Nathan points used in the later fit were adjusted upward about 3% following reanalysis of density and ball diameter; they are not an independent replication of S02.
- **Limits:** No formal confidence region or covariance for the fitted constants was located in R02. The roughly ±20% practical warning concerns inversion to infer transverse spin, not a 95% coefficient confidence interval.
- **LPB use:** Provisional smooth reference; the functional shape and fit-data selection remain modeling choices, without a universal validated domain.

### S04 - Nathan ASR alternatives

Nathan, A. M. (2020, August 30). *The 3D spin axis: An addendum* [Unpublished technical note]. University of Illinois Urbana-Champaign. [Addendum](https://baseball.physics.illinois.edu/trackman/SpinAxisAddendum.pdf)

- **Type/access:** Primary proposed mixed-spin extensions; direct A2.1 equations 6-10, Figure 1, discussion, and references.
- **Date caveat:** The PDF is dated August 30; the author's index lists August 31. This reference uses the inspected PDF date.
- **Prescriptions:** With ASR the transverse/total spin ratio, prescription a is `f(S) ASR`; prescription b is `f(S ASR)`. LPB calls these ASR-A and ASR-B.
- **Supports/limits:** They coincide for pure transverse spin, zero active spin, or linear f. Existing laboratory geometry discussed in the note does not distinguish the nonlinear mixed-spin prescriptions.
- **LPB use:** ASR-B is provisionally selected, ASR-A remains essential. This note supplies no experimental proof of B's superiority or universal exclusion of other prescriptions.

### S05 - Alaways dissertation

Alaways, L. W. (1998). *Aerodynamics of the curve-ball: An investigation of the effects of angular velocity on baseball trajectories* [Doctoral dissertation, University of California, Davis]. [UC Davis author record](https://research.engineering.ucdavis.edu/biosport/sample-page/lab-members/leroy-alaways/)

- **Type/access:** Primary dissertation; institutional identity directly checked, complete archival dissertation not obtained or read here. R04 likewise documents incomplete full-text recovery and restricted archival routes.
- **Supports:** Existence and research identity; recoverable summaries describe marker/trajectory parameter estimation of forces and spin.
- **Limits:** No verified dissertation moment law, fitted torque table, or calibration may be inferred from its title or a later citation. Incomplete access also prevents asserting that it contains no moment discussion anywhere.
- **LPB use:** Lift-method antecedent; alleged Alaways spin-down attribution remains unresolved.

### S06 - Alaways and Hubbard spin/lift experiment

Alaways, L. W., & Hubbard, M. (2001). Experimental determination of baseball spin and lift. *Journal of Sports Sciences, 19*(5), 349–358. [https://doi.org/10.1080/02640410152006126](https://doi.org/10.1080/02640410152006126)

- **Type/access:** Primary experiment/parameter-estimation article. Complete text not reobtained in A2.1; R04 reports full-text inspection, especially equation 15 and printed p. 354. R01/R02 had earlier, more limited access.
- **Evidence:** Surface-marker and center trajectories constrain aerodynamic coefficients and initial spin. R04 identifies explicitly constant fitted angular velocity; this is not integration of a measured spin-down ODE.
- **Seams/limits:** Reported four-/two-seam lift differences diminish at higher spin factor. Do not assign the entire dissertation sample to this article or infer a torque law from the force fit.
- **LPB use:** Empirical lift ancestry and precedent for a constant-spin approximation, not proof that real spin is constant.

### S07 - Alaways, Mish, and Hubbard trajectory estimation

Alaways, L. W., Mish, S. P., & Hubbard, M. (2001). Identification of release conditions and aerodynamic forces in pitched-baseball trajectories. *Journal of Applied Biomechanics, 17*(1), 63–76. [https://doi.org/10.1123/jab.17.1.63](https://doi.org/10.1123/jab.17.1.63)

- **Type/access:** Primary trajectory-estimation study; archive bibliographic/abstract verification in R04, not full primary access in A2.1.
- **Scope:** Translational flight, release conditions, and aerodynamic force estimation. Reported translational speed loss is not rotational spin loss.
- **LPB use/limits:** Context for historical drag/lift extraction; no verified aerodynamic moment law established by the accessible record.

### S08 - Watts and Ferrer wind-tunnel lift

Watts, R. G., & Ferrer, R. (1987). The lateral force on a spinning sphere: Aerodynamics of a curveball. *American Journal of Physics, 55*(1), 40–44. [https://doi.org/10.1119/1.14969](https://doi.org/10.1119/1.14969)

- **Type/access:** Primary force-measurement experiment; original detailed data not re-extracted here. R02 checked journal identity/abstract; S01/S02 reproduce its role in the lift evidence.
- **Regime/provenance:** Nathan summarizes low-speed tunnel measurements below roughly 37 mph and larger `S ≈ 0.4-1.0`.
- **Limits/LPB use:** Antecedent of SHS, not direct high-speed validation of all SHS branches. Detailed original uncertainty remains unverified in this pass.

### S09 - Sakamoto-group supported hard-baseball rotation

Sakamoto, S., Hasegawa, J., Tada, T., Naruo, T., & Mizota, T. (2011). C20 Wind tunnel testing of new ball, U.S and Japanese baseball balls. *The Proceedings of the Symposium on Sports and Human Dynamics, 2011*, 532–535. [https://doi.org/10.1299/jsmeshd.2011.532](https://doi.org/10.1299/jsmeshd.2011.532)

- **Type/access:** Primary Japanese proceedings experiment; direct A2.1 [laboratory copy](https://www.fit.ac.jp/~mizota/pdf/ronbun2011_03.pdf) and J-STAGE identity; interpreted in R05/R06.
- **Observable/regime:** Supported-ball angular-speed decay using tachometry, three hard-ball constructions, four-seam backspin, 30/35/40/44 m/s and analyzed rotation approximately 17-60 rps.
- **Moment convention:** Signed resisting coefficients use frontal area and **diameter** reference length; Figure 6 gives order `10^-3` magnitude, not a universal fit.
- **Limits:** Wire-force correction is described, but that does not establish bearing-torque subtraction. Ball/rotating-assembly inertia calibration and torque uncertainty are not reproducible from the published account.
- **LPB use:** Approximate high-speed torque anchor. Shared laboratory/apparatus lineage with S10 does not give independent checks on mechanical bias.

### S10 - Naruo hard-/rubber-ball comparison

Naruo, T., Kusunoki, T., Ito, A., Kanda, S., & Mizota, T. (2012). Comparison aerodynamic characteristics between rubber-ball and baseball ball. *The Proceedings of the Symposium on Sports and Human Dynamics, 2012*, 79–82. [https://doi.org/10.1299/jsmeshd.2012.79](https://doi.org/10.1299/jsmeshd.2012.79)

- **Type/access:** Primary Japanese proceedings experiment; direct A2.1 [paper](https://www.jstage.jst.go.jp/article/jsmeshd/2012/0/2012_79/_pdf), with R05/R06 apparatus/figure audit.
- **Observable:** Supported rotational decay; Figure 6's wind-off/on traces are explicitly for the **rubber ball**. Hard-baseball moment data appear graphically in Figure 8.
- **Limits:** No published analytic hard-ball regression or complete bearing-friction/inertia/replicate uncertainty chain. A matched hard-ball wind-off subtraction cannot be reconstructed from the rubber-ball trace.
- **LPB use:** Resisting torque evidence at substantial airflow; R05's `beta ≈ 0.005-0.007` is an LPB visual approximation, not a Naruo fit or confidence band.
- **Calibration caution:** Wind-off decay includes genuine aerodynamic rotational drag as well as mechanics. Subtracting all wind-off torque would not automatically recover total free-flight aerodynamic damping (R06; S12).

### S11 - Nagaoka-group bearing-free observations

Nagaoka, H., Ogino, N., Tsuboi, K., Noda, S., & Himeno, R. (2019). Measurement of rotational property for a baseball in flight with acceleration sensors. *Transactions of the JSME (in Japanese), 85*(876), Article 18-00440. [https://doi.org/10.1299/transjsme.18-00440](https://doi.org/10.1299/transjsme.18-00440)

- **Type/access:** Primary free-flight experiment; direct A2.1 [paper](https://www.jstage.jst.go.jp/article/transjsme/85/876/85_18-00440/_pdf), especially Tables 4-6 and equation 16; R06/R08 give the corrected interpretation.
- **Regime:** Eighteen throws (nine straight, nine curve), 10 m, 161 g instrumented baseball-like ball; average-flight speeds about 12.5-21.7 m/s. Reported group spin scales are 12.56/19.22 rps, not 300-350 rpm.
- **Evidence:** Two reconstructions from the same sensor data show mean endpoint spin decrease, with zero/slightly negative losses in individual trials. Predicted decreases around 0.1-0.2 rps and observed changes have the same order, not precision agreement.
- **Published interpolation:** Equation 16 gives signed `C_m = 0.0223 S^2 - 0.0205 S + 0.0015`, interpolating S09's tunnel data; these are not newly measured free-flight torque coefficients.
- **Limits/LPB use:** Bearing-free corroboration supports damping and approximate scale. Nonregulation construction, assumed inertia, lower speeds, and shared-data algorithms limit calibration; inferred axis changes do not validate perfectly fixed axes.

### S12 - Koss near-stationary rotation

Koss, M. B., Booth, T. M., & Sullo, K. M. (2011). On the measurement of the characteristic rotation decay time for a baseball. *International Journal of Sports Science and Engineering, 5*(1), 17–26. [Archived article lead](https://citeseerx.ist.psu.edu/document?doi=f15ebf6fd5df9fd715d8a1a5ad4bec107a9d2ea7&repid=rep1&type=pdf)

- **Type/access:** Primary experiment; archive verification in R03/R04. The linked copy returned 403 during A2.1; no new full-primary inspection is claimed and no DOI was established.
- **Regime/evidence:** One usable 1.2 s microgravity sequence, approximately 800 rpm, two-seam rotation, translation below 0.5 m/s, 420 frames/s; reported characteristic time roughly `7 ± 2 s`.
- **Limits:** Short record relative to the timescale, noise, and insufficient discrimination among decay shapes. The reported uncertainty is not a pitch-regime parameter distribution.
- **LPB use:** R1-E context and evidence against universal zero damping at zero translation. It neither calibrates high-speed R1-U nor requires adding a low-speed term now.

### S13 - Nathan spin-down heuristic

Nathan, A. M. (2008). *The effect of spin-down on the flight of a baseball* [Unpublished technical note]. University of Illinois Urbana-Champaign. [Technical note](https://baseball.physics.illinois.edu/spindown.pdf)

- **Type/access:** Primary heuristic/model analysis; archive verification in R03-R05, not newly re-audited in A2.1.
- **Provenance:** Assumed offset lift and approximate linear lift scaling yield airspeed-dependent damping; parameter guidance comes from golf-ball evidence, not direct high-speed baseball torque calibration.
- **Limits/LPB use:** Structural motivation for R1-U and sensitivity studies. Do not identify its lift-offset parameter with LPB beta or treat golf-ball constants as measured baseball constants. The archive-reviewed version is intended here; later revisions require separate comparison.

### S14 - Kimura moment-law attribution

Kimura, A., Nakashima, H., Kuroyanagi, S., Ando, Y., Liao, P., & Sakurai, S. (2023). Estimating flight trajectories of breaking balls from four-seam fastballs. *Frontiers in Sports and Active Living, 5*, Article 1092520. [https://doi.org/10.3389/fspor.2023.1092520](https://doi.org/10.3389/fspor.2023.1092520)

- **Type/access:** Primary contemporary trajectory-model paper; direct A2.1 [article access](https://www.frontiersin.org/journals/sports-and-active-living/articles/10.3389/fspor.2023.1092520/full), with detailed equation/attribution audit in R04.
- **Claim/limits:** Uses a spin-parameter-dependent moment coefficient attributed to Alaways 1998, without publishing a reproducible numerical constitutive law. R04 flags the printed torque direction/sign ambiguity.
- **LPB use:** Evidence that moment frameworks are used in trajectory modeling, not verification of an Alaways torque calibration. Similar normalization to Naruo does not establish a data lineage between them.

### S15 - Healey-Wang Hill comparator

Healey, G., & Wang, L. (2020). *Combining radar, weather, and optical measurements to model the dependence of baseball lift on spin and surface roughness* [Preprint]. viXra. [Version 1](https://vixra.org/pdf/2008.0131v1.pdf)

- **Type/access:** Primary empirical modeling preprint; direct A2.1 sections on radar/optical synthesis and the Hill function, equation 18. H01 names the alternative without complete bibliographic details.
- **Evidence:** Combines large radar pitch groups, weather corrections, optical constraints, and robust estimation; coefficients are model-based inferences, not direct force measurements.
- **Limits/LPB use:** Alternative Hill-shaped lift fit; calibration/sensor bias, missing radar spin geometry, and mixed-spin assumptions need comparison before adoption. Its statistical precision cannot be transferred to Nathan parameters or LPB beta.
- **Version caveat:** A related journal DOI, [10.1109/ACCESS.2021.3114372](https://doi.org/10.1109/ACCESS.2021.3114372), was located as a lead but publisher text was unavailable in this pass. No identity of preprint and final coefficients is assumed; no executable Hill constants are selected here.

### S16 - Lyu seam-sensitive lift and drag

Lyu, B., Smith, L., Elliott, J., & Kensrud, J. (2025). The dependence of baseball lift and drag on spin. *Proceedings of the Institution of Mechanical Engineers, Part P: Journal of Sports Engineering and Technology, 239*(2), 235–241. [https://doi.org/10.1177/17543371221113914](https://doi.org/10.1177/17543371221113914)

- **Type/access:** Primary experiment; direct A2.1 [2022 online article copy](https://baseball.physics.illinois.edu/LyuDragLiftSpin.pdf). Final pagination was verified against the publisher record during A2.1 review corrections; the inspected early copy has local pages 1-7, so use section/table locators rather than equating its pages to final pagination.
- **Regime/evidence:** Six MLB balls, still-air backspin measurements, approximately `Re = 144,000`, `0 < S < 0.5`, two-/four-seam axes, plus flow observations. Substantial low-spin seam sensitivity narrows the universality of a scalar lift curve.
- **Uncertainty:** R01 distinguishes instrumental `C_L` uncertainty near 0.0009 from larger repeatability/ball variability near 0.007 across 416 measurements; neither is a universal policy confidence interval.
- **LPB use/limits:** Boundary evidence for A and future B-D; selected seam axes and flow regime do not supply an arbitrary-attitude force/torque surface.

### S17 - Seam height and carry

Kensrud, J. R., Smith, L. V., Nathan, A. M., & Nevins, D. (2015). Relating baseball seam height to carry distance. *Procedia Engineering, 112*, 406–411. [https://doi.org/10.1016/j.proeng.2015.07.216](https://doi.org/10.1016/j.proeng.2015.07.216)

- **Type/access:** Primary long-flight experiment; R01 reports full-text access, not newly re-audited here.
- **Evidence/limits:** Seam-height/drag association in its tested balls; a reported null lift association in that experiment is not universal absence of seam-sensitive lift.
- **LPB use:** Supports retaining drag refinement as research and avoiding unqualified transfer across ball constructions.

### S18 - Computational orientation studies

Issakhanian, E. (2025). Numerical investigation of the aerodynamic force variations during rotation of a pitched baseball. *Discover Mechanical Engineering, 4*, Article 27. [https://doi.org/10.1007/s44245-025-00111-9](https://doi.org/10.1007/s44245-025-00111-9)

Seydel, J. (2026). The influence of seam orientation on the lift and drag of a rotating baseball in flight. *Proceedings of the Institution of Mechanical Engineers, Part P: Journal of Sports Engineering and Technology, 240*(2), 319–330. [https://doi.org/10.1177/17543371241241846](https://doi.org/10.1177/17543371241241846)

Erratum to “The influence of seam orientation on the lift and drag of a rotating baseball in flight”. (2026). *Proceedings of the Institution of Mechanical Engineers, Part P: Journal of Sports Engineering and Technology, 240*(2), 592–594. [https://doi.org/10.1177/17543371241255787](https://doi.org/10.1177/17543371241255787)

- **Type/access:** Primary computational studies, not independent physical experiments. R01 reports study access; Seydel's publisher record and published erratum were directly checked during A2.1 review corrections, including their 2026 issue metadata.
- **Correction/provenance:** The erratum, first published online May 19, 2024, identifies inadvertently swapped Figures 9, 10, and 13–17 in Seydel's OnlineFirst article and supplies the correct figures and captions. The publisher states that the OnlineFirst article has been corrected; subsequent figure-based comparisons should use the corrected figures and captions.
- **Evidence/limits:** Configuration-dependent forces and periodic response within selected numerical models. Geometry, flow modeling, and resolution assumptions constrain interpretation.
- **LPB use:** Motivation for future orientation/geometry/flow research, not authorization of harmonic orders, surrogate choices, CFD interfaces, or universal accuracy claims.

## Missing calibration and provenance obligations

- The 2005 FIT master's thesis attributed to Daisuke Nishikiori, with translated title *Aerodynamic study of "magic" pitches of hard baseballs*, is an **unrecovered lead** from R05/R06; title translation, exact bibliographic record, apparatus, and independence are unverified. No fabricated APA reference or URL is supplied.
- Hard-ball wind-off traces, bearing identity/load/preload/friction, rotating-assembly inertia, raw decay data, repeat counts, and uncertainty budgets remain unavailable in the audited calibration chain. Absence of published details does not prove no laboratory correction occurred.
- Neither a guessed mechanical fraction nor the rubber-ball wind-off/on ratio is a measured hard-baseball bearing correction. Hypothetical contamination multipliers are sensitivity cases, not probabilities.
- R08's beta near 0.009 is a rounded local approximation to S11's interpolation near a declared pitch state. The earlier 0.005-0.007 graph reading and the later local estimate are different derivations, not bounds of an empirical distribution.
- Exact lift/torque coefficients, validity domains, gyrospin damping, low-speed torque, and long-fly-ball extrapolation remain research questions. Numerical verification cannot fill these empirical gaps.
- APA identity is recorded only to available evidence. Restricted originals, final-version comparisons, and missing raw calibration are retained as explicit obligations, not silently supplied by report summaries.
