# Model A independent trajectory verification (B2-I4)

B2-I4 is implemented locally on the B2-I3 baseline `1e13f90554c4ffd61a9e16b7532502804c1aae91` and passed independent review. PR and CI review and separate merge authorization remain pending. The [runtime contract](model-a-runtime.md) describes acceptance. The [scientific specification](model-a.md) governs the equations. This verification measures errors against analytical and independently integrated solutions of those equations. The scientific coefficients remain provisional.

## Reference data and regeneration

Run the optional standard-library generator from the repository root with an existing Python 3.11+ interpreter. Windows verification used CPython 3.14.2 x64 by its installed path. The ordinary .NET test command reads the embedded fixture directly.

```powershell
py -3.14 research/generate_model_a_reference.py --output tests/Lpb.Physics.Tests/Fixtures/model-a-reference.json
```

Regeneration to a separate file took 48.66 s on the Windows host and matched the checked-in bytes. Fixture SHA-256 is `995c758d8a5c1dd5f5c374ac14f631c77bd3af997c813bfbc048355dc51192e8`.

The six cases use 0.145 kg mass, 0.0366 m radius, 1.225 kg/m³ density, drag coefficient 0.35, gravity 9.80665 m/s², release position (0, 0, 1.0366) m, velocity (30, 20, 25) m/s, and duration 2 s. Transverse, mixed, and initially gyro spins are respectively (−100, 150, 0), (−50, 200, 80), and (150, 100, 125) rad/s. Each runs with R0 and R1-U. R1-U uses beta 0.009 and explicit fixture inertia `(.4 * m) * (r * r)`, with each multiplication rounded to binary64. It yields `7.769448e-05` kg m², exactly `0x1.45dfd00b22fa5p-14` in hexadecimal binary64 notation. The same analytical inertia defines the R0 energy comparison.

All physical and timestamp inputs are decoded exactly from binary64 with `Decimal.from_float`. The oracle uses the scientific decimal numbers 0.336 and 6.041 and a reproducible decimal expansion of π with 120 fractional digits. A separate run decodes the three nearest binary64 counterparts and records the maximum resulting change for each column. That sensitivity run still uses the scientific differential equations and independent integrator. It does not model B1's rounded operation sequence. The largest state change from the coefficient representation comparison was $4.85\times10^{-16}$ in the respective SI units.

With $U=|v|$, $c=\omega\times v$, and $A=\pi r^2$, the generator uses $C_L=0.336(1-\exp(-6.041r|c|/U^2))$ and $a_M=\rho A U^2 C_L c/(2m|c|)$. It returns zero lift before division when speed, spin, or the cross product vanishes. Drag acts along $-v$ and gravity along $-Z$. R1-U uses $\dot\omega=-(\rho A\beta r^2/I)U\omega$. Two passive quadratures integrate the positive dissipations $\rho A C_D U^3/2$ and $\rho A\beta r^2 U|\omega|^2$ in joules, with zero rotational dissipation for R0.

For macro interval $H$, the modified-midpoint recurrence starts with $y_1=y_0+\delta f(y_0)$, then $y_{j+1}=y_{j-1}+2\delta f(y_j)$ with $\delta=H/n$. The estimate is $(y_n+y_{n-1}+\delta f(y_n))/2$. For even $n$, extrapolate in $1/n^2$ to zero using successive midpoint estimates. The Decimal context uses round-to-nearest ties-to-even. The output times are the union of the exact indexed binary64 sample times for 0.04, 0.02, and 0.01 s plus the exact endpoints, 201 times per case. Between reference macro boundaries, a separate reference advance reaches the requested time from the preceding macro anchor without changing the main reference mesh. No interpolation or production solver is involved.

| Precision | Reference macro step (s) | Even subdivision counts |
| --- | --- | --- |
| 60 digits | 0.1 | 4–14 |
| 80 digits | 0.05 | 4–16 |
| 90 digits | 0.025 | 4–18, initially gyro cases |

Use the 80-digit result for transverse and mixed trajectories and the 90-digit result for initially gyro trajectories. At matching timestamps, 60 versus 80 digits differed by at most $5.53\times10^{-25}$ on transverse/mixed state components and $7.76\times10^{-18}$ on initially gyro state components. The extra 80 versus 90 comparison differed by at most $3.00\times10^{-20}$. The fixture records each comparison maximum per state component and work integral. Both state and work comparisons must pass the generator's $10^{-16}$ stabilization check. These are measured differences across reference settings.

## Recorded errors and refinement

All 18 checked coupled runs were accepted. Each of the nine recorded components at 0.01 s met the approved fixture tolerance $10^{-9}$ in its own SI unit. Each error triple below gives the largest absolute component error within the position, velocity, and spin groups, in m, m/s, and rad/s. The energy columns give maximum absolute mechanical-balance residuals at the matched samples, in joules.

| Case | Step (s) | Recorded maximum x / v / ω | Final x / v / ω | Energy residual trans / rot |
| --- | --- | --- | --- | --- |
| transverse/R0 | 0.04 | 3.826e-8 / 5.388e-9 / 0.000e+0 | 3.826e-8 / 5.388e-9 / 0.000e+0 | 7.525e-8 / 0.000e+0 |
| transverse/R0 | 0.02 | 2.374e-9 / 3.334e-10 / 0.000e+0 | 2.374e-9 / 3.334e-10 / 0.000e+0 | 4.668e-9 / 0.000e+0 |
| transverse/R0 | 0.01 | 1.479e-10 / 2.074e-11 / 0.000e+0 | 1.479e-10 / 2.074e-11 / 0.000e+0 | 2.907e-10 / 0.000e+0 |
| transverse/R1U | 0.04 | 4.002e-8 / 5.416e-9 / 1.726e-9 | 4.002e-8 / 5.242e-9 / 1.726e-9 | 7.685e-8 / 2.757e-11 |
| transverse/R1U | 0.02 | 2.484e-9 / 3.323e-10 / 1.059e-10 | 2.484e-9 / 3.243e-10 / 1.059e-10 | 4.769e-9 / 1.692e-12 |
| transverse/R1U | 0.01 | 1.547e-10 / 2.058e-11 / 6.622e-12 | 1.547e-10 / 2.018e-11 / 6.622e-12 | 2.970e-10 / 1.053e-13 |
| mixed/R0 | 0.04 | 3.828e-8 / 6.255e-9 / 0.000e+0 | 3.828e-8 / 6.255e-9 / 0.000e+0 | 7.516e-8 / 0.000e+0 |
| mixed/R0 | 0.02 | 2.377e-9 / 3.878e-10 / 0.000e+0 | 2.377e-9 / 3.878e-10 / 0.000e+0 | 4.666e-9 / 0.000e+0 |
| mixed/R0 | 0.01 | 1.480e-10 / 2.415e-11 / 0.000e+0 | 1.480e-10 / 2.415e-11 / 0.000e+0 | 2.906e-10 / 0.000e+0 |
| mixed/R1U | 0.04 | 4.004e-8 / 6.087e-9 / 2.970e-9 | 4.004e-8 / 6.087e-9 / 2.970e-9 | 7.683e-8 / 5.354e-11 |
| mixed/R1U | 0.02 | 2.486e-9 / 3.773e-10 / 1.825e-10 | 2.486e-9 / 3.773e-10 / 1.825e-10 | 4.770e-9 / 3.290e-12 |
| mixed/R1U | 0.01 | 1.549e-10 / 2.349e-11 / 1.125e-11 | 1.549e-10 / 2.349e-11 / 1.125e-11 | 2.971e-10 / 2.031e-13 |
| initiallyGyro/R0 | 0.04 | 2.990e-8 / 3.603e-9 / 0.000e+0 | 2.990e-8 / 3.603e-9 / 0.000e+0 | 3.631e-8 / 0.000e+0 |
| initiallyGyro/R0 | 0.02 | 1.854e-9 / 2.199e-10 / 0.000e+0 | 1.854e-9 / 2.199e-10 / 0.000e+0 | 2.245e-9 / 0.000e+0 |
| initiallyGyro/R0 | 0.01 | 1.154e-10 / 1.358e-11 / 0.000e+0 | 1.154e-10 / 1.358e-11 / 0.000e+0 | 1.395e-10 / 0.000e+0 |
| initiallyGyro/R1U | 0.04 | 3.004e-8 / 3.550e-9 / 4.171e-9 | 3.004e-8 / 3.550e-9 / 4.171e-9 | 3.669e-8 / 9.896e-11 |
| initiallyGyro/R1U | 0.02 | 1.864e-9 / 2.167e-10 / 2.585e-10 | 1.864e-9 / 2.167e-10 / 2.585e-10 | 2.269e-9 / 6.135e-12 |
| initiallyGyro/R1U | 0.01 | 1.160e-10 / 1.338e-11 / 1.606e-11 | 1.160e-10 / 1.338e-11 / 1.606e-11 | 1.410e-10 / 3.800e-13 |

Step halving produced measured ratios 16.00–16.38 for the recorded and terminal state errors. These satisfy the approved 12–20 window for these runs. R0 retains the spin vector exactly, so its spin ratio is undefined. The initially gyro case starts at zero transverse spin. Results on these fixtures do not imply fourth-order behavior at every degeneracy or convergence below the rounding floor.

The mixed R1-U 0.04 s run passed local checks and still accumulated $4.00\times10^{-8}$ m of position error. The $10^{-8}$ m runtime absolute floor participates in a local component budget. The independent trajectory result measures accumulated error.

## Analytical and energy evidence

For vacuum, constant gravity gives (x(t)=x_0+v_0t-gt^2e_Z/2), (v(t)=v_0-gte_Z). At zero density the test also checks all spin components and agreement with the legacy gravity-only runtime on the shared accepted fixture. For zero gravity and zero spin, quadratic drag with `k = rho*A*Cd/(2*m)` preserves direction and gives speed `U0/(1+k*U0*t)` and travel `log(1+k*U0*t)/k`. An oblique zero-spin descent also matches the unchanged independent drag fixture at each stored drag reference time and the legacy runtime at every recorded time. The two acceptance domains need not coincide.

For axial damping, `omega(t) = omega(0) exp[-(A*beta*r²/I) integral(rho U dt)]`. The existing cardinal gyro test uses constant speed and the independent exponential. The test-only linear fixtures use `U(t)=2(1+at)` and `rho(t)=.75(1+bt)`, with $a=.25$ and $b=0$ or (0.5). The integral is `1.5*(t+(a+b)*t²/2+ab*t³/3)`. Both nonzero components attenuate by the same factor and the zero component remains zero. Existing tests cover large finite spin, release-based invariants, and zero beta equivalent to R0 with distinct provenance.

For the tilted circle, take $g=C_D=0$, unit release velocity $e_x$, and R0 spin $(0,3,4)$. The constant spin direction is $n=(0,.6,.8)$, so $n\times e_x=(0,.8,-.6)$. Both speed and transverse spin stay constant in the exact model. With angular frequency $\nu=\rho A C_L U/(2m)$, $v=U(e_x\cos\nu t+(n\times e_x)\sin\nu t)$. Integrating yields displacement $U[e_x\sin(\nu t)+(n\times e_x)(1-\cos(\nu t))]/\nu$. The test follows this solution at every 0.02 s sample through 6 s, including the X velocity reversal and the corresponding X position turn. The observed maximum component error was $3.77\times10^{-10}$. The test checks unit speed, the plane, rotation direction, and exact spin retention.

For stationary air $v\cdot(\omega\times v)=0$, so Magnus does no work. Gravity contributes $-mgv_z$ to kinetic power and $+mgv_z$ to potential power. Their cancellation leaves $-\rho A C_D U^3/2$ for translational mechanical power. Angular power is $I\omega\cdot\dot\omega=-\rho A\beta r^2 U|\omega|^2$ for R1-U and zero for R0 with the explicit analytical inertia. Existing I2 derivative tests compare these identities with independent rational arithmetic. The recorded trajectory residuals above use differences of endpoint squared norms and add the independent positive integrated dissipation. The residuals improve under refinement. This evidence does not impose exact discrete conservation or monotonicity on RK4 stages or outputs.

## B2-D and B2-N coverage

| Obligation | Evidence |
| --- | --- |
| Independent stage laws, lift-only work, drag and gravity work, and torque power | Existing `ModelADynamicsTests`, `ModelAMagnusKernelTests`, and `ModelARotationKernelTests` use independent arithmetic. |
| Gravity, zero-spin drag, circular path including reversal, and prescribed damping | `ModelATrajectoryTests` plus the existing constant-speed and polynomial damping cases in `ModelAI3AnalyticalTests`. |
| Six coupled trajectories, all nine components, matched time, convergence, energy | `ModelAReferenceTests` and the offline fixture. |
| B0 constant-damping coincidence and coarse Magnus amplification with subfloor discrepancy | Existing `ModelASimulatorTests` rejects both with stage and criterion context. The latter's unchecked witness grows speed by over 200×. |
| Equal endpoints despite lost motion, required intermediate overflow, B1 component loss, and failures returning no partial result | Existing `ModelASimulatorTests` with independent equality, exact-loss and finite-endpoint controls. |
| Subnormal increments/extinction, cancellations, directed interval and finite-work bounds, budget ties and neighboring values | Existing `ModelANumericalArithmeticTests` and `ModelANumericalPolicyTests`. |
| Closed invariant controls, unsafe time subdivisions, exact horizon and bounds, full-step retention and twelve evaluations | Existing `ModelANumericalPolicyTests`, `ModelASimulatorTests`, and `SimulationTimeGridTests`. |
| Legacy gravity/drag and I1/I2/I3 API/behavior | Complete baseline suite and explicit shared accepted reductions. |

Mathematical identities, numerical-method verification, and software regression results address the documented model and its implementation. Empirical calibration needs experimental evidence. This study supplies no certified global error or between-sample event completeness.

## Windows runtime observations

Windows 11 Pro build 26300 x64, Intel Core i9-14900K (24 cores, 32 logical processors), approximately 63.7 GiB reported RAM. SDK 10.0.403, .NET 10.0.14 Release, workstation GC, default tiered compilation setting. Policy `classical-rk4/indexed-v1;model-a-full-state-v1`.

The standalone harness measures synchronous `ModelASimulator.Simulate` on the transverse 2 s fixture above. It uses three warmups and five measured invocations per configuration, following the method described in [I3 PR #29](https://github.com/skolins3651/lets-play-baseball/pull/29). Exact Mac harness source was unavailable locally. Inputs are specified here so later comparisons can reproduce these runs. The timings exclude fixture generation, JSON, startup, and test-framework overhead. Allocation is cumulative bytes on the executing thread, including result construction. GC deltas are summed over the five measured calls per row.

| Rotation | Steps | Step (s) | Median ms | Median allocated bytes | Bytes / step | Collections Gen 0 / 1 / 2 |
| --- | --- | --- | --- | --- | --- | --- |
| R0 | 100 | 0.020 | 91.94 | 102,771,656 | 1027717 | 27 / 0 / 0 |
| R0 | 200 | 0.010 | 118.33 | 205,452,128 | 1027261 | 55 / 0 / 0 |
| R0 | 400 | 0.005 | 231.83 | 410,865,744 | 1027164 | 109 / 0 / 0 |
| R1U | 100 | 0.020 | 61.87 | 109,384,240 | 1093842 | 29 / 0 / 0 |
| R1U | 200 | 0.010 | 125.15 | 218,759,928 | 1093800 | 58 / 0 / 0 |
| R1U | 400 | 0.005 | 250.14 | 437,448,112 | 1093620 | 117 / 0 / 0 |

The process peak working set reached 63,049,728 bytes across the harness. This is a process-lifetime high-water mark, including the runtime and warmups. It is neither retained trajectory size nor cumulative allocation. The first configuration was still variable across trials, so timing is a descriptive observation. Windows allocation was about 1.027–1.094 MB per step, similar in scale to the reported Mac observation. Different hosts and runtime environments prevent attributing timing differences to code changes. Allocation remains a significant engineering concern. The harness and raw trial records accompany the I4 review packet.
