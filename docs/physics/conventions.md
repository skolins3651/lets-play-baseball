# M1 Physics Conventions and Gravity Reference Cases

> **Status:** Provisional M1 working specification — adopted 2026-11-01; contact and measurement verification updated 2026-11-03\
> **Issues:** [#2 — Physical conventions and validation](https://github.com/skolins3651/lets-play-baseball/issues/2); informs [#3](https://github.com/skolins3651/lets-play-baseball/issues/3), [#4](https://github.com/skolins3651/lets-play-baseball/issues/4), and [#5](https://github.com/skolins3651/lets-play-baseball/issues/5)\
> **Architecture:** [`docs/architecture.md`](../architecture.md) summarizes the adopted conventions and links here for scientific details. This field-coordinate convention replaces the earlier provisional center-field-oriented X/Y proposal.

## Purpose and scope

Specify the coordinates, physical quantities, simplifications, and analytically solvable trajectories needed to **verify** an initial 3D, gravity-only projectile solver. Issue #2 established these documentation fixtures; Issue #4 implements the solver and executable trajectory verification. This document is intentionally not a comprehensive aerodynamics or rigid-body physics specification. More realistic physical effects are introduced in later M1 issues without discarding these baseline tests.

## Coordinates and units

- Use a **right-handed, orthonormal Cartesian frame** fixed to the baseball field. Looking outward from home plate: **+X** follows the first-base/right-field foul-line direction; **+Y** follows the third-base/left-field foul-line direction; **+Z** is vertically upward.
- The origin `(0, 0, 0)` is the **rear tip of home plate at the field's reference ground elevation**. These X/Y directions correspond to idealized perpendicular baseline rays, not the finite-width painted markings.
- For an idealized point in the field's horizontal plane, the fair-territory wedge is `x >= 0 && y >= 0`, including boundary rays. **This is not a fair/foul ruling algorithm**: ball geometry, contact, timing, and baseball rules are separate concerns.
- The ideal center-field direction is along `X = Y` in positive coordinates. This orientation must not assume that a real outfield fence is symmetric.
- Use **SI units internally**, with `double` precision: position m, velocity m/s, acceleration m/s², duration s, mass kg, radius m, angular velocity rad/s (when introduced), and angles rad.
- Any mph, feet, inches, rpm, degrees, or other baseball-friendly representations belong at input/output or debug/presentation boundaries. Conversion does **not** change authoritative state.
- Time is elapsed **simulation time**, not rendering or wall-clock time. A standalone trajectory starts at `t = 0 s` for this initial contract.

## Gravity-only motion (Issue #4)

Let the initial center position and linear velocity be `r0 = (x0, y0, z0)` and `v0 = (vx0, vy0, vz0)`. Let `g >= 0` be a finite, constant **magnitude** in m/s². Gravity acts along `-Z`:

```
a = (0, 0, -g)

dr/dt = v
dv/dt = a

r(t) = r0 + v0*t + 0.5*a*t^2
v(t) = v0 + a*t
```

This model has **no air resistance**, Magnus force, spin evolution, orientation, wind, bounce, or rule adjudication. A gravity-only solution does not need baseball mass or radius. Starting below `z = 0` is mathematically valid when ground-contact handling is disabled.

The implemented initial numerical method is fixed-step **fourth-order Runge–Kutta (RK4)** with positive finite nominal timestep `dt`, positive finite requested duration `T`, and a shortened final interval when the indexed grid does not end at `T`. A timestep larger than `T` is valid and requires a single shortened step. RK4 is an initial baseline, not a permanent restriction against later justified numerical methods. The implementation is independent of rendering, wall-clock time, and the CLI; gravity derivative evaluation is separate from RK4 integration. RK4 under constant acceleration reproduces the analytical trajectory to approximately floating-point precision; this suite tests correctness but does **not** establish fourth-order convergence for nonconstant forces.

### Executable timing and numerical limits

`GravityOnlySimulator.Simulate(FlightRequest)` records every step, starting at exactly zero and ending at the exact requested duration. Nominal boundaries are `B(i) = (double)i * dt`; choose the first boundary reaching/exceeding `T`, use earlier indexed boundaries as samples, and assign the terminal timestamp directly from `T`. Integrate each actual endpoint difference. Floating-point rounding makes ordinary intervals differ slightly from nominal `dt`; this is expected, not accumulated timestamp drift.

The provisional limit is 100,000 integration steps and 100,001 samples. Bounded binary-search preflight occurs before allocating samples, using the same indexed multiplication as execution. Excessive requests throw `InvalidOperationException`; null invocation throws `ArgumentNullException`. Unsafe time progression or RK4 arithmetic throws `ArithmeticException`, never a successful partial trajectory. See [the contract](gravity-only-contract.md) for ownership, exact boundary rules, and failure details.

Conventional double arithmetic checks finite derivatives, intermediate states, weighting, increments, and outputs. RK4 also requires positive representable half/sixth coefficients and a strictly interior midpoint. Some finite requests therefore fail: subnormal intervals can lose coefficients, adjacent timestamps may have no midpoint, and large values can overflow intermediate expressions even when a mathematical endpoint is finite. Ordinary small state changes lost below local state precision are allowed. No special scaled arithmetic or universal extreme-input accuracy is promised. Ground contact remains disabled even after a path crosses `z = 0`.

## Canonical analytical fixtures

Use artificial `g = 10 m/s²` to make answers independently checkable; this is a fixture parameter, not a universal or Earth-gravity default. Coordinates below are **ball-center coordinates**, and ground collisions are **disabled** for Issue #4. The plane `z = 0` is only a mathematical reference, not an event boundary in these tests. Run each to `T = 2 s`, e.g. with `dt = 0.25 s`.

| Case | Initial center position `r0` (m) | Initial velocity `v0` (m/s) | Position at 1 s (m) | Position at 2 s (m) | Velocity at 2 s (m/s) |
|---|---|---|---|---|---|
| A. Vertical drop | `(0,0,20)` | `(0,0,0)` | `(0,0,15)` | `(0,0,0)` | `(0,0,-20)` |
| B. Horizontal launch | `(0,0,20)` | `(10,0,0)` | `(10,0,15)` | `(20,0,0)` | `(10,0,-20)` |
| C. Oblique launch | `(0,0,0)` | `(10,0,10)` | `(10,0,5)` | `(20,0,0)` | `(10,0,-10)` |
| D. Three-dimensional launch | `(0,0,0)` | `(6,8,10)` | `(6,8,5)` | `(12,16,0)` | `(6,8,-10)` |

All four reach the reference elevation at 2 s. In Cases C/D, the maximum center height is 5 m above the initial elevation, reached at 1 s. The 3D case has horizontal speed `sqrt(6^2 + 8^2) = 10 m/s`, horizontal displacement `(12,16)` m and horizontal distance 20 m. Cases A/B reach their maximum at release (20 m above reference elevation).

**Implemented Issue #4 verification:** NUnit checks all nine sample times and all six state components for Cases A–D against independently calculated analytical expectations, including the documented one- and two-second values. The accepted absolute tolerance is `1e-9` in metres or metres/second for these fixtures; timestamps, ordering, count, initial state, completion reason, and final-value consistency are checked exactly. This is a fixture-specific tolerance, not a universal accuracy guarantee.

The separate standard-gravity reference uses `g = 9.80665 m/s²`: dropping from `(0,0,20)` m at rest gives `z(1) = 15.096675 m`, `z(2) = 0.3867 m`, and `vz(2) = -19.6133 m/s`, verified within the same absolute tolerance. Additional tests cover zero-gravity constant velocity, downward/negative velocities, below-ground continuation, nondivisible duration, and a timestep greater than duration.

A test-only manufactured evaluator `y' = y` independently verifies four classical RK4 evaluations. At unit step from a component value of one, stage values are `1`, `1.5`, `1.75`, and `2.75`, at times `0`, `0.5`, `0.5`, and `1`; the final value is `65/24`. All six components are checked with absolute tolerance `1e-13`. This distinguishes the algorithm from an analytical constant-gravity shortcut. These checks verify software and the stated gravity equations; they do not establish broad fourth-order convergence or empirical baseball accuracy. Broader convergence and validation remain later work, particularly Issue #10.

## Ground contact and measurement verification (Issue #5)

A flat ground surface has configurable elevation `zg`. A spherical ball with radius `R > 0` first touches it when **center height** reaches `zg + R`, on a relevant downward crossing. An initially touching ball moving upward is allowed to launch. An initially touching stationary or descending ball terminates immediately. Initial penetration is rejected using exact comparisons, without a geometric tolerance. Detect the crossing time between numerical steps rather than treating the first below-ground sample as the landing instant.

Geometry must have finite elevation, finite positive radius, and a finite rounded center-contact height strictly above ground elevation. Reject a radius absorbed by floating-point addition. For a zero center-contact plane, use `GroundContactSettings(-0.05, 0.05)`, not zero ground elevation with positive radius.

For contact-aware fixtures, define release height `h` **above contact height** and set `z0 = zg + R + h`. This avoids confusing the center's `z` coordinate with the ball's lowest point. Ground contact, bounce, rolling, and fair/foul rulings are separate physical/rules features: #5 handles only first-contact termination and reporting.

### Independent contact and apex references

For test references only, let `h = z0 - (zg + R)`. For positive gravity, the descending contact root is `(vz0 + sqrt(vz0² + 2*g*h)) / g`; use `2*h / (sqrt(vz0² + 2*g*h) - vz0)` for a downward release to avoid cancellation. An upward release at contact returns at `2*vz0/g`. For zero gravity and downward velocity, contact time is `h / -vz0`. Compute X/Y and velocity from the independent equations above.

For positive gravity and upward initial velocity, the analytical apex is at `vz0/g`, restricted to the actual simulated interval. Initially descending paths maximize at release. Constant-height paths use the earliest time, zero. These equations are verification references, never production shortcuts.

Contact fixtures include the four canonical launch directions translated by contact height, nonzero ground elevation/radius, standard gravity, and coarse steps spanning both launch and return. Small fixtures use `1e-10 s` event/apex time and `1e-9` SI state/height comparisons; exact timestamps, geometry, ownership, and completion invariants remain exact. These do not promise universal accumulated integration accuracy.

Near-horizon fixtures use zero center-contact height, zero gravity, `vz0 = -1`, `T = 1`, and release center heights `1 - 2^-32`, `1`, and `1 + 2^-32`. They distinguish contact before/at/after duration; the last stays airborne even though its final height is below the localization height tolerance. A stationary near-plane flight also remains airborne. See [the contract](gravity-only-contract.md) for numerical refinement, bounded projection, and resource policies.

## Deferred capabilities and explicit non-decisions

- Aerodynamic drag, atmosphere, wind, and lift/Magnus force are introduced incrementally in later M1 issues.
- Spin is an eventual 3D angular-velocity vector (rad/s) in the adopted field frame, with direction given by the ordinary right-hand rule. Its representation, persistence, evolution, and aerodynamic coupling will be specified when the spin model requires them. Initial gravity-only state and integration do not need spin.
- Ball orientation, seam geometry, quaternions, rotational torque, seam-shifted wake, and detailed collision dynamics are deferred. Their future introduction should be supported by model and state evolution, **not** by placeholder physics fields in this first solver.
- No assumption is made that all future moving objects (e.g., players) must use this trajectory model. Reuse mathematical components when justified.
- Empirical baseball-flight agreement is **validation** and is not a pass criterion for gravity-only verification. Later calibrated models must be assessed with their own evidence and assumptions.

## Adoption and implementation staging

- The conventions and analytical fixtures above are adopted as the provisional M1 baseline.
- [`docs/architecture.md`](../architecture.md) summarizes the same coordinate convention; mathematical details remain here.
- Issue #3's implemented inputs and Issue #4's executable solver/output contracts are documented in [`gravity-only-contract.md`](gravity-only-contract.md). Historical deferrals are labeled there.
- Numerical integration and focused trajectory verification are implemented in Issue #4; contact-aware termination and optional measurements are implemented in Issue #5. Minimum useful visualization is a separate follow-up before drag; broader plotting/comparisons remain later work.
