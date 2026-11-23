# Model A - Smooth Magnus scientific reference

> Status: A2.2 documentation independently reviewed and accepted on November 22, 2026. Mathematical and scientific documentation only; approval does not constitute empirical validation, final coefficient calibration, approval of new public software contracts, or authorization to implement physics.
> Baseline: [accepted taxonomy](model-taxonomy.md), [source register](../../research/aerodynamics/source-register.md), [decision register](../../research/aerodynamics/decision-register.md), and [experimental program](../../research/aerodynamics/experiments.md).

## 1. Authority, scope, and scientific status

Model A is a three-dimensional, instantaneous-state representation of conventional transverse Magnus lift, combined with gravity and configurable constant-coefficient quadratic drag.
The provisional reference is Nathan saturation with ASR-B and R1-U; R0 is the experimental control, ASR-A an essential comparator.
SHS and Healey-Wang Hill remain lift alternatives; R1-E is contextual and R1-M the broader moment framework.
These selections retain the statuses in decisions D01-D08; mathematical precision below does not upgrade their empirical status.

The source register owns bibliographic identities, access qualifications, and source keys S01-S18/H01-H02/R01-R08.
R08 revision 1.1 controls R1 interpretation and supersedes R07, including its incorrect numerical, sign, frame, and regime statements.
The historical A2.2 authorization covered only this scientific specification and the experimental-program document.
Existing merged code and [gravity](../gravity-only-contract.md)/[drag](../drag-model.md) contracts remain unchanged.

There is no explicit seam phase, full body attitude, geometry-resolved flow, or wake-memory state in A.
Empirical coefficients from real seamed balls can contain seam-mediated effects implicitly; "smooth" does not mean a physically seamless ball.
Conventional Magnus lift is only the force prescribed below, not all possible transverse force on a real baseball.
No additional seam-shifted/non-Magnus force is selected, and future seam corrections must avoid double counting the empirical baseline (S06/S16, D01/D10).

## 2. Physical quantities and representation boundary

Use the [adopted right-handed field frame](../conventions.md): +X toward first base/right field, +Y toward third base/left field, +Z upward.
The origin remains the rear tip of home plate at reference ground elevation; time is elapsed simulation time.
Cross products use the ordinary right-hand rule, so \(\mathbf e_X\times\mathbf e_Y=\mathbf e_Z\).

| Quantity | Symbol and SI units | Scientific role/assumption |
| --- | --- | --- |
| Ball-center position and field velocity | \(\mathbf x\), m; \(\mathbf v\), m/s | Evolving translation; position also locates any prescribed environment |
| World-frame angular velocity | \(\boldsymbol\omega\), rad/s | Right-hand axial vector; magnitude \(\Omega=\|\boldsymbol\omega\|\), rad/s |
| Mass, radius, diameter | \(m>0\), kg; \(r>0\), m; \(d=2r\), m | Fixed ball parameters during the reference flight |
| Reference area | \(A=\pi r^2\), m² | Frontal area, not total sphere surface area |
| Scalar rotational inertia | \(I>0\), kg m² | Fixed spherical/isotropic approximation for R1 energy/torque; not needed by R0 lift alone |
| Gravity | \(g\ge0\), m/s² | Reference field acceleration \(-g\mathbf e_Z\), constant |
| Air velocity and density | \(\mathbf w\), m/s; \(\rho\ge0\), kg/m³ | Still air and constant explicit density are the initial baseline |
| Drag/lift coefficients | \(C_D\ge0\), \(C_{L,\mathrm{eff}}\ge0\) | Dimensionless selected response policies |
| R1-U damping parameter | \(\beta\ge0\) | Dimensionless, resolved and fixed for a reference flight; calibration provisional |

All supplied physical values and evaluated quantities need finite, meaningful numerical representations.
These physical dependencies do not approve constructor validation rules, new public types, state layouts, APIs, or configuration identifiers.
The current engine retains position/velocity, not spin; this document does not pretend those scientific dependencies already have executable representations.
Mass and radius remain shared physical quantities, including spherical contact geometry when applicable.

The notation \(\mathbf w(\mathbf x,t)\), \(\rho(\mathbf x,t)\) below permits discussing prescribed air conditions mathematically.
It does not select an environmental interface, atmosphere derivation, spatial variability model, or wind implementation.
Those remain chiefly Issues #8/#9; use \(\mathbf w=\mathbf0\) and constant \(\rho\) for the present reference comparison unless a separately declared verification fixture prescribes otherwise.

## 3. Air-relative kinematics and active spin

Define air-relative velocity and speed by

\[
\mathbf u=\mathbf v-\mathbf w,\qquad U=\|\mathbf u\|.
\tag{1}
\]

For \(U>0\), let \(\widehat{\mathbf u}=\mathbf u/U\) and decompose spin:

\[
\boldsymbol\omega_G=(\boldsymbol\omega\cdot\widehat{\mathbf u})\widehat{\mathbf u},
\qquad
\boldsymbol\omega_T=\boldsymbol\omega-\boldsymbol\omega_G,
\qquad
\Omega_T=\|\boldsymbol\omega_T\|.
\tag{2}
\]

Gyrospin is the parallel/antiparallel component; transverse spin is perpendicular to the air-relative motion.
For \(U>0,\Omega>0\), define total spin factor, transverse spin factor, and active spin ratio:

\[
S=\frac{r\Omega}{U},\qquad
\alpha=\mathrm{ASR}=\frac{\Omega_T}{\Omega}
 =\frac{\|\boldsymbol\omega\times\mathbf u\|}{\Omega U},
\qquad
S_T=\frac{r\Omega_T}{U}=S\alpha.
\tag{3}
\]

All three are dimensionless; \(0\le\alpha\le1\) in exact arithmetic.
When defined, \(\alpha=\sin\theta\), where \(\theta\) is the angle between spin and air-relative velocity.
Spin in rpm must be converted to rad/s at a boundary; an extra factor \(2\pi\) must not be inserted into equation (3) after that conversion.
Equations (2)-(3) use Nathan's transverse/gyro distinction, expressed in LPB's field frame and air-relative notation (S03/S04).

At \(\Omega=0\), ASR and spin direction are undefined; at \(U=0\), the air-direction decomposition and spin factors are undefined.
Do not report an invented physical ASR or evaluate an undefined ratio there.
The force/torque extensions below specify the derivative without requiring those diagnostics.

## 4. Lift-coefficient hypotheses

### 4.1 Provisional Nathan transverse-spin reference

\[
f_N(S)=0.336\,[1-\exp(-6.041S)],\qquad S\ge0.
\tag{4}
\]

This is Nathan's 2020 nonlinear least-squares fit to selected Alaways/Nathan transverse-spin observations, not a first-principles law or the principal new equation in Nathan's 2008 experiment (S02-S04; R02; D02).
The 2008 measurements and the later fit share evidence; they are not independent replications.
S02 covers approximately 50-110 mph and \(S=0.090-0.595\) in 22 nominally transverse, two-seam pitches; S03 describes selected motion-capture fit data near 80-100 mph.
Those ranges describe the evidence, not approved hard cutoffs or a validated envelope for arbitrary mixed-spin or batted-ball flight.
The source register records selected-data provenance, density/diameter reanalysis, absence of formal parameter confidence regions, and limited calibration regimes.
Do not interpret the fitted asymptote or printed digits as universally calibrated physical constants.

### 4.2 Mixed-spin prescriptions

\[
C_{L,\mathrm{eff}}^{\mathrm{ASR-A}}=f_N(S)\alpha,
\qquad
C_{L,\mathrm{eff}}^{\mathrm{ASR-B}}=f_N(S\alpha)=f_N(S_T).
\tag{5}
\]

ASR-B is the provisional reference; ASR-A remains essential (S04 equations 6-10; D03).
Neither the selected transverse dataset nor this choice establishes experimentally superior ASR-B predictions.
For pure transverse spin they coincide; for pure gyrospin both prescribe zero lift.
They also coincide for a response exactly linear through the origin; Nathan's low-S linear approximation gives approximate, not exact, coincidence away from zero.
For finite \(S>0\) and \(0<\alpha<1\), Nathan's strictly concave saturation gives \(f_N(S\alpha)>\alpha f_N(S)\).
This inequality follows mathematically from equation (4); a larger predicted coefficient is not evidence of greater empirical accuracy.

The prescription must use instantaneous spin and air-relative velocity, not an ASR frozen at release.
An initially transverse or gyro case need not stay in that geometry as velocity turns.
Comparisons may be omitted as duplicates only when equivalence holds throughout the relevant path or prescribed fixture.

### 4.3 Retained alternatives, not mandatory implementations

SHS's historical bilinear law is

\[
f_{\mathrm{SHS}}(S)=
\begin{cases}
1.5S,&0\le S\le0.1,\\
0.09+0.6S,&S>0.1.
\end{cases}
\tag{6}
\]

Both branches give 0.15 at the join; the derivative has a kink.
S01/R02 trace the heterogeneous antecedent experiments and neglected low-spin seam differences.
Moderate-spin support does not validate unlimited high-spin linear extrapolation.

The Hill-shaped alternative in the inspected Healey-Wang 2020 preprint is

\[
f_H(S)=\frac{a_H S^{n_H}}{s_H^{\,n_H}+S^{n_H}},
\qquad a_H>0,\ s_H>0,\ n_H>0.
\tag{7}
\]

S15, version 1, equation 18 reports \(a_H=0.370,\ s_H=0.137,\ n_H=1.651\) under its fitting procedure; these are source-version parameters, not LPB-selected calibration.
The final journal/preprint comparison remains unverified as recorded in S15.
The inspected preprint multiplies its scalar response by the transverse/total-spin factor (its equation 5), an ASR-A-style extension; applying its Hill curve through ASR-B would be a separately declared LPB comparison, not reproduction of that source's complete prescription.
Its radar/weather/optical inference and mixed-spin assumptions need separate assessment; a transverse curve alone does not specify a universal three-dimensional lift law.
Any proposed use of an alternative must declare its mixed-spin extension and source version rather than silently inheriting Nathan's status.
No requirement to implement either alternative follows from retaining its equations.

## 5. Magnus force and complete translational derivative

For \(U>0\) and nonzero \(\boldsymbol\omega\times\mathbf u\), define

\[
\widehat{\mathbf n}_M
=\frac{\boldsymbol\omega\times\mathbf u}{\|\boldsymbol\omega\times\mathbf u\|},
\qquad q=\tfrac12\rho U^2,
\qquad
\mathbf F_M=qA C_{L,\mathrm{eff}}\widehat{\mathbf n}_M,
\qquad \|\mathbf F_M\|=qA C_{L,\mathrm{eff}}.
\tag{8}
\]

The cross-product **direction is normalized** because equation (5) already accounts for ASR.
Multiplying by an additional unnormalized \(\widehat{\boldsymbol\omega}\times\widehat{\mathbf u}\) would introduce a second factor of ASR and change the hypothesis (S04 equation 1).
Coefficient sign and force direction are separate: positive coefficients give the conventional Magnus direction specified here.

Extend \(\mathbf F_M=\mathbf0\) when \(U=0\), \(\Omega=0\), or \(\Omega_T=0\); also return zero aerodynamic force when \(\rho=0\).
For fixed finite spin, Nathan's bounded coefficient makes the zero-speed force limit zero despite undefined S and direction.
At fixed nonzero U, equations (4)-(5) make force vanish continuously as spin or transverse spin vanishes.
These are force limits; they do not select an artificial epsilon speed, empirical coefficient at zero speed, or diagnostic ASR value.
SHS's linear growth also gives a zero-force limit as U tends to zero at fixed finite spin, but that mathematical extension supplies no empirical low-speed validity.
Any later alternative needs its own limiting-behavior and applicability assessment.

With the same reference area and constant \(C_D\), the full model is

\[
\dot{\mathbf x}=\mathbf v,\qquad
\mathbf F_D=-\tfrac12\rho A C_D\,U\mathbf u,\qquad
\dot{\mathbf v}=-g\mathbf e_Z+\frac{\mathbf F_D+\mathbf F_M}{m}.
\tag{9}
\]

Thus zero air-relative speed gives zero translational aerodynamic force.
Equation (9) reduces to the existing stationary-air drag equation when lift is absent and \(\mathbf w=\mathbf0\); illustrative \(C_D=0.35\) remains explicit and uncalibrated (D07).
Zero spin removes Magnus lift, not drag; disabling drag alone or setting beta to zero does not remove Magnus lift.

### Direction and symmetry checks

- With \(\mathbf u=U\mathbf e_X\), spin \(+\Omega\mathbf e_Y\) gives lift toward \(-\mathbf e_Z\); spin \(-\Omega\mathbf e_Y\) gives upward lift. Spin \(+\Omega\mathbf e_Z\) gives lift toward \(+\mathbf e_Y\).
- Pure transverse spin has \(\alpha=1\); pure gyrospin, parallel or antiparallel to \(\mathbf u\), has \(\alpha=0\) and zero conventional lift at that instant.
- Mixed spin follows equation (5); the gyro component influences ASR-A through total S but is omitted from ASR-B's coefficient argument.
- For fixed translation/environment, spin reversal preserves coefficient magnitude and reverses \(\mathbf F_M\); it does not reverse gravity or drag, nor imply a universal mirrored whole trajectory.
- Exactly, \(\mathbf F_M\cdot\mathbf u=0\) and \(\mathbf F_M\cdot\boldsymbol\omega=0\). With wind, \(\mathbf F_M\cdot\mathbf v\) need not vanish.
- For a proper rotation Q, rotate position, velocity, spin, wind, and gravity together: \(\mathbf F_M(Q\boldsymbol\omega,Q\mathbf u)=Q\mathbf F_M(\boldsymbol\omega,\mathbf u)\) with matching scalar inputs. Rotating only spin/velocity while leaving gravity fixed is a different physical problem; reflections require axial-vector care.

In still air with constant gravity, conventional lift does no translational work, and
\(\frac{d}{dt}(\frac12m\|\mathbf v\|^2+mgz)=-\frac12\rho A C_D U^3\le0\).
This is a mathematical consequence of equation (9), not empirical validation or a universal field-frame energy test in moving air.

## 6. Rotational policies and corrected moment convention

### 6.1 R0 - constant world-frame vector

\[
\dot{\boldsymbol\omega}=\mathbf0,\qquad
\boldsymbol\omega(t)=\boldsymbol\omega_0.
\tag{10}
\]

R0 is Experiment I's control, not a claim of physically constant spin (D04).
It requires neither scalar inertia nor body attitude to evaluate conventional lift.

### 6.2 R1-M framework and R1-U simplification

With fixed scalar I, R1 restricts torque to the spin axis.
For \(\Omega>0,U>0\), use a **nonnegative resisting-magnitude** coefficient \(C_{m,d}\):

\[
\boldsymbol\tau=-qAd\,C_{m,d}\frac{\boldsymbol\omega}{\Omega},
\qquad
I\dot\Omega=-qAd\,C_{m,d},\qquad d=2r.
\tag{11}
\]

This matches the diameter reference length used in the audited wind-tunnel studies (S09/S10; R08 section 3).
A moment signed along positive spin has \(C_{m,\mathrm{signed}}=-C_{m,d}\) for damping.
If the same q and A are used with a radius reference length, the corresponding coefficient is twice the diameter-based coefficient.
Check the dynamic-pressure prefactor as well as reference length before comparing other conventions; identical coefficient symbols need not imply identical normalization.
Equation (11) alone is not an all-domain law: zero-spin regularity, zero-speed behavior, and the constitutive function remain additional questions.

The provisional local approximation \(C_{m,d}=\beta S\) gives

\[
\dot{\boldsymbol\omega}
=-\frac{\rho A\beta r^2}{I}\,U\boldsymbol\omega
=-\gamma(t)U(t)\boldsymbol\omega,\qquad
\gamma(t)=\frac{\rho(t)A\beta r^2}{I}.
\tag{12}
\]

The factor one-half in q cancels against \(d=2r\); there is no extra one-half in equation (12).
Gamma has units m⁻¹ and \(\gamma U\) has units s⁻¹; \(\boldsymbol\tau=I\dot{\boldsymbol\omega}\) has units N m.
This is R1-U, the provisional physical reference (R08; D04-D06), not a newly calibrated general moment surface.
Scalar I is a declared spherical/isotropic simplification; \(I=(2/5)mr^2\) is a homogeneous-sphere illustration, not a measured inertia of every baseball.
Under that illustration \(\gamma=5\rho A\beta/(2m)\).

For nonnegative rho/beta and U, spin magnitude cannot increase in the exact model; nonzero spin direction remains fixed in the **world frame**.
With finite integrated damping it remains positive for positive initial magnitude and does not cross through zero.
At zero spin the derivative is zero without normalizing the spin vector; at zero U the R1-U derivative is zero without forming S.
Disabling beta, or any interval of zero rho/U, gives the R0 derivative on that interval.
Pure gyrospin still damps under equation (12) when rho, beta, U, and spin are nonzero, despite zero instantaneous conventional lift.
That gyrospin response is a provisional extrapolation, not direct gyrospin-specific calibration.

### 6.3 Analytical references and rotational energy

For constant m, r, I, beta, density, and prescribed constant U:

\[
\boldsymbol\omega(t)=\boldsymbol\omega_0 e^{-\gamma Ut}.
\tag{13}
\]

For constant density and a prescribed nonnegative speed history:

\[
\boldsymbol\omega(t)=\boldsymbol\omega_0
\exp\!\left[-\gamma\int_0^t U(s)\,ds\right].
\tag{14}
\]

With prescribed density and speed along the path, keeping A, beta, r, and I fixed:

\[
K=\frac{A\beta r^2}{I},\qquad
\boldsymbol\omega(t)=\boldsymbol\omega_0
\exp\!\left[-K\int_0^t\rho(s)U(s)\,ds\right].
\tag{15}
\]

K has units m²/kg, so the exponent is dimensionless.
A single constant gamma must not be carried unchanged between different densities.
Equations (14)-(15) also hold along the actual coupled path, but do not independently solve that path: its U and rho histories may depend on translation and lift.

Two proposed mathematical fixtures make independent checks possible:

- With constant rho and prescribed \(U(t)=U_0+ct\ge0\), equation (14)'s exponent is \(-\gamma(U_0t+ct^2/2)\), with c in m/s².
- With \(\rho(t)=\rho_0(1+bt)\) and \(U(t)=U_0(1+at)\), a and b in s⁻¹ and nonnegative prescribed density/speed over the fixture, the integral in (15) is \(\rho_0U_0[t+(a+b)t^2/2+ab\,t^3/3]\).

These are prescribed-input verification cases, not atmosphere contracts, measured baseball histories, or approval to replace coupled RK4 with an analytical/split integrator.

\[
E_{\mathrm{rot}}=\tfrac12I\Omega^2,\qquad
\dot E_{\mathrm{rot}}=I\boldsymbol\omega\cdot\dot{\boldsymbol\omega}
=-I\gamma U\Omega^2
=-\rho A\beta r^2 U\Omega^2\le0.
\tag{16}
\]

Fixed I is essential to this derivative. No conversion of the lost rotational energy into translational energy is prescribed; the empirical damping represents exchange with the air.

### 6.4 Coefficient provenance and contextual alternatives

R08 section 4 derives beta's local scale from S11 equation 16, a **signed interpolation of S09's wind-tunnel measurements**:
\(C_{m,\mathrm{signed}}=0.0223S^2-0.0205S+0.0015\).
Near 95 mph, 2,500 rpm, and \(r=0.0366\) m, \(S\approx0.2256\) and \(-C_{m,\mathrm{signed}}/S\approx0.00882\).
The rounded \(\beta\approx0.009\) is an illustrative nominal candidate, not a universal constant or finalized calibration.
The polynomial is not selected for extrapolation: it is nonzero at zero S and changes sign outside its useful middle regime.
The older 0.005-0.007 graph reading is neither a measured confidence interval nor a distribution to combine with the later local estimate.

R08's beta values 0, 0.0045, 0.009, and 0.018 are deterministic control/half/nominal/double examples; they do not bound empirical uncertainty.
With its declared \(m=0.145\) kg, \(r=0.0366\) m, \(\rho=1.225\) kg/m³, homogeneous-sphere I, and hypothetical constant 95 mph for 0.42 s, nominal loss is approximately 1.42%.
This is an analytical model illustration, not a measured pitch or an Experiment I pass criterion.

R1-E is the contextual alternative \(\dot{\boldsymbol\omega}=-\boldsymbol\omega/\tau\), \(\tau>0\), with solution \(\boldsymbol\omega_0e^{-t/\tau}\).
It damps even at zero U; the roughly seven-second Koss timescale comes from near-stationary rotation, not matched high-speed pitch calibration (S12; D05).
R1-M remains the broader fixed-axis moment framework, not a requirement for richer constitutive implementation.
R2/R3, directional torque, precession, full attitude, and seam-dependent torque remain deferred.

## 7. Coupling, determinism, and interpretation

Translation changes U and its direction; these change S, ASR, lift, drag, and the R1-U rate.
R1-U changes spin magnitude and therefore lift; altered lift feeds back into the subsequent velocity history.
R0 fixes world spin, not S or ASR. R1-U's fixed axis also leaves ASR sensitive to changing air-relative direction.
An initially gyrospinning ball can acquire transverse spin relative to a turning velocity without any spin-axis precession.

A fixed state, prescribed environment, selected policies, and resolved parameters must produce a deterministic derivative.
No random coefficient draws belong inside force calculations, derivative evaluations, or RK4 stages.
Later uncertainty realizations belong outside the deterministic kernel with reproducible realized parameters and sampling provenance; no distribution or stochastic infrastructure is selected (D08).
Inputs and provenance must preserve the actual executed physical case and distinguish it from pending edits or another policy.
Those scientific expectations do not specify an immutable type layout, result schema, export format, or public identifier.

## 8. Mathematical and numerical verification obligations

The following are verification targets for later authorized implementation, not a report that tests exist or pass.
Exact model properties must be distinguished from floating-point residuals and explicitly justified tolerances.
No empirical coefficient is validated by passing these checks.

| Check | Exact/reference target | Numerical interpretation |
| --- | --- | --- |
| Dimensions | Forces N; accelerations m/s²; moments N m; angular derivatives rad/s²; exponents dimensionless | Audit normalizations and boundary conversions independently |
| Zero cases | Zero rho removes aerodynamic forces and R1-U torque; zero spin/active spin removes Magnus lift; zero U removes translational aerodynamic forces and R1-U torque | Do not form undefined ratios or normalize zero vectors |
| Direction/orthogonality | Equation (8), cardinal examples, dot products with u and spin zero | Use component-sensitive comparisons; near-zero force needs absolute as well as relative reasoning |
| Reversal/proper rotations | Lift and damping reverse with spin; jointly rotated vector problem transforms consistently | Test derivatives first; trajectory comparisons must transform all physical vectors |
| R0 recovery | Beta zero gives identical rotational ODE to R0 | Later implementation must establish its actual arithmetic/disabled-policy behavior |
| Fixed axis/energy | Nonzero R1-U direction preserved; (16) nonpositive; spin never increases | Small roundoff residuals require declared numerical criteria, not an empirical tolerance |
| Prescribed decay | Equations (13)-(15), including the two proposed time-varying fixtures | Compare all spin components to an independent reference and refine timestep |
| Translational limits | Zero lift with stationary air recovers the mathematical gravity/drag baseline | Preserve established regressions, identity, and numerical guarantees for their existing models |
| Determinism/provenance | Same resolved case gives repeatable derivatives and replayable inputs | No hidden draws; bitwise cross-platform trajectory identity is not assumed |
| Coupled convergence | States and applicable measurements stabilize against smaller steps/independent reference | Separate integration, event, arithmetic, and physical-model error |

Classical RK4 is the existing numerical baseline, not an analytical solution of equations (9)/(12).
Fourth-order convergence is an investigation on sufficiently smooth cases, not a universal theorem for every selected law/trajectory.
SHS's kink, environmental discontinuities, and vector-force regularity at degenerate spin/speed cases require separate examination.
Even the smooth scalar Nathan curve does not by itself certify arbitrary-order smoothness of every vector expression at zero norms.
Step-doubling agreement is an observed consistency check, not a certified global-error bound.
Position/velocity tolerances cannot simply be reused as angular-velocity tolerances; complete-state numerical policy and budgets remain later design work.

## 9. Existing contact, apex, and evaluation assumptions to revisit

This audit concerns the existing stationary-air [gravity contract](../gravity-only-contract.md) and [drag contract](../drag-model.md).
It preserves their guarantees in their original regimes and identifies missing justification for Model A; it selects no replacement event machinery.

| Existing conclusion or mechanism | Force-law dependency and Model A implication |
| --- | --- |
| Constant-gravity concavity and monotonically decreasing vertical velocity | With lift, \(\dot v_z=-g-(\rho A C_D/2m)Uu_z+F_{M,z}/m\). The added vertical term removes the general concavity argument. |
| Drag's unique upward-to-downward apex for positive g | In still air, drag alone gives \(\dot v_z=-g\) at \(v_z=0\). Model A instead includes \(F_{M,z}/m\), whose magnitude/sign are not constrained to preserve that crossing direction. Multiple extrema cannot be ruled out. |
| Initially descending paths maximize at release; zero-g drag has no future apex | Neither is a general Model A conclusion. A global maximum must consider all relevant extrema and interval boundaries, with earliest-time tie semantics justified separately. |
| Horizontal signs, nonincreasing component magnitudes, invariant zero components | Magnus can turn velocity, accelerate a component, or change its sign. Drag-only component damping, displacement envelopes, and precision exemptions are not generic Magnus invariants. |
| Contact found from a descending on/below-plane endpoint | With no established single-excursion property, above-plane endpoints can miss an interior excursion. A sign bracket alone does not prove the earliest qualifying contact. |
| Tangency/launch exclusion | Lift does not provide the old at-most-one-return proof. A plane touch can have zero vertical velocity; whether/how such contact is qualified needs explicit later analysis. |
| Flat spherical geometry and initial contact conventions | Center contact height \(H=z_g+r\) remains geometric. Existing exact penetration rejection, touching-upward launch, and immediate stationary/downward contact are established conventions, not a proof about subsequent lift-driven motion. Model A applicability needs explicit review. |
| Gravity's position/velocity rounding certificates | They use constant accelerations and a specific RK4 expression without force feedback through intermediate states. State-dependent lift invalidates direct transfer of their derivation. |
| Issue #22 ordinary-anchor/release polynomial evidence | Constant-gravity clearance and timing polynomials are not the Model A path. Both origins remain important provenance, but their old proof cannot certify lift contact. |
| Zero-g drag reachability based on initial downward speed | Lift can change downward speed; the existing necessary bound is not established for the combined force. |
| Full/two-half-step consistency, paired geometry, exact roots, and terminal projection | Preserve their caution about rounded equality, but rejustify checks for coupled translation/spin. Small residuals or paired agreement alone do not certify physical occurrence or first-contact timing. Projection is not a repair for failed evidence. |
| Unique-apex binary search and interior height witness | A witness near one stationary point cannot prove a global maximum if other extrema were not excluded. Sampling alone can miss peaks. |
| Continuous evaluation and actual completion interval | Model-aware provenance, finite states, matching inputs, and no invented post-termination measurements remain principles. Actual Model A evaluation needs a justified coupled-state path; the current six-component evaluator is not already that capability. |

An instantaneous zero lift or speed is not proof that lift stays disabled over a step or flight.
Beta zero removes torque, not lift; \(C_D=0\) removes drag, not lift.
The mathematical gravity-only reduction requires translational aerodynamic forces identically absent over the relevant interval; unchanged legacy delegation/provenance cannot be assumed for a newly introduced model.
Optional measurement failure must remain distinguishable from successful flight and from empirical model error.
Event qualification, completeness of first-contact/apex searches, localization/certification, spin consistency, representation limits, and work budgets remain later numerical-contract questions, not silently selected algorithms here.

## 10. Empirical applicability and open decisions

| Evidence or assumption | Supported interpretation and retained limit |
| --- | --- |
| Nathan 2008 / 2020, S02-S04 | Transverse lift evidence and a later fitted curve; restricted speed/spin/seam coverage, no universal parameter confidence region, unresolved nonlinear mixed-spin choice |
| SHS and Healey-Wang, S01/S15 | Alternative empirical syntheses/inferences with different data selection and assumptions; exact fit/version/domain choices matter |
| Seam-sensitive observations, S06/S16/S17 | Real-ball coefficients can include seam effects; construction/ball variability and flow regime limit transfer, without uniquely separating seam-free Magnus lift |
| Sakamoto/Naruo, S09/S10 | Supported four-seam rotation at 30-44 m/s in tested spin configurations anchors resisting moment scale; bearing friction, assembly inertia, raw data, and uncertainty chain remain incomplete |
| Nagaoka, S11 | Bearing-free mean damping and order-of-magnitude corroboration using a 161 g instrumented ball at approximately 12.5-21.7 m/s; not precision calibration of regulation high-speed flights or fixed axes |
| Koss, S12 | Near-stationary damping prevents treating zero-U R1-U behavior as universal physics; no low-speed correction is selected now |
| Fixed axis and scalar I | Intentional reductions in complexity; not evidence that perpendicular torque, layered-ball inertia, or attitude effects are absent |
| Long-duration/low-speed trajectories | Sensitivity may accumulate outside the best-supported regime; neither direction nor size of extrapolation bias is established |

D10's simplicity judgment matches model complexity to identifiable evidence; it does not establish omitted physics as negligible.
Reconsider the laws or parameter values when matched spin/trajectory measurements, apparatus corrections, independently measured inertia, or material regime-dependent residuals warrant it.
Scientific calibration remains chiefly Issue #11, with numerical studies under #10; beta, lift uncertainty, coefficient compatibility, and any distributions remain unresolved.
No new physical-confidence bounds, fly-ball accuracy promise, or public parameter contract is created here.

## 11. Source trace and documentation status

- Equations (1)-(3), (5), (8) and transverse/gyro interpretation: S03/S04, re-expressed using the adopted field frame and air-relative velocity; zero-case extensions and symmetry/energy deductions are explicit LPB mathematical consequences.
- Equation (4) and fit provenance: S03/S04 and R02; alternatives (6)/(7): S01 and S15 version 1, respectively.
- Equations (11)-(16), sign/diameter convention, and beta illustration: corrected R08 sections 3-7; S09-S11 empirical ancestry; D04-D06 preserve reference/calibration distinctions.
- R1-E context: S12; heuristic high-speed ancestry: S13, without treating golf-ball scaling or an alleged Alaways torque law as calibrated baseball evidence.
- Experimental purpose and deferral: H01/H02, taxonomy, D01/D09/D10. Numerical/event audit: existing gravity/drag contracts and LPB analysis of their stated assumptions.

A2.3 completed repository integration of this A2.2 specification, which neither amends the A2.1 decisions nor implements their models.
Executable software contracts, implementation, and Git publication/merging require separate authorization.
