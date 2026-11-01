# M1 Physics Conventions and Gravity Reference Cases

> **Status:** Provisional M1 working specification — adopted 2026-11-01\
> **Issues:** [#2 — Physical conventions and validation](https://github.com/skolins3651/lets-play-baseball/issues/2); informs [#3](https://github.com/skolins3651/lets-play-baseball/issues/3), [#4](https://github.com/skolins3651/lets-play-baseball/issues/4), and [#5](https://github.com/skolins3651/lets-play-baseball/issues/5)\
> **Architecture:** [`docs/architecture.md`](../architecture.md) summarizes the adopted conventions and links here for scientific details. This field-coordinate convention replaces the earlier provisional center-field-oriented X/Y proposal.

## Purpose and scope

Specify the coordinates, physical quantities, simplifications, and analytically solvable trajectories needed to **verify** an initial 3D, gravity-only projectile solver. Issue #2 establishes these documentation fixtures; numerical implementation and executable trajectory tests belong to Issue #4. This document is intentionally not a comprehensive aerodynamics or rigid-body physics specification. More realistic physical effects are introduced in later M1 issues without discarding these baseline tests.

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

The initial intended numerical method for Issue #4 is fixed-step **fourth-order Runge–Kutta (RK4)** with positive finite configurable timestep `dt`, a positive finite requested duration `T`, and a shortened final step if `T/dt` is not an integer. A timestep larger than `T` is valid and requires a single shortened step. RK4 is an initial baseline, not a permanent restriction against later justified numerical methods. The integration method must not be coupled to rendering or the CLI. RK4 under constant acceleration reproduces the analytical trajectory to approximately floating-point precision; this suite tests correctness but does **not** establish fourth-order convergence for nonconstant forces.

## Canonical analytical fixtures

Use artificial `g = 10 m/s²` to make answers independently checkable; this is a fixture parameter, not a universal or Earth-gravity default. Coordinates below are **ball-center coordinates**, and ground collisions are **disabled** for Issue #4. The plane `z = 0` is only a mathematical reference, not an event boundary in these tests. Run each to `T = 2 s`, e.g. with `dt = 0.25 s`.

| Case | Initial center position `r0` (m) | Initial velocity `v0` (m/s) | Position at 1 s (m) | Position at 2 s (m) | Velocity at 2 s (m/s) |
|---|---|---|---|---|---|
| A. Vertical drop | `(0,0,20)` | `(0,0,0)` | `(0,0,15)` | `(0,0,0)` | `(0,0,-20)` |
| B. Horizontal launch | `(0,0,20)` | `(10,0,0)` | `(10,0,15)` | `(20,0,0)` | `(10,0,-20)` |
| C. Oblique launch | `(0,0,0)` | `(10,0,10)` | `(10,0,5)` | `(20,0,0)` | `(10,0,-10)` |
| D. Three-dimensional launch | `(0,0,0)` | `(6,8,10)` | `(6,8,5)` | `(12,16,0)` | `(6,8,-10)` |

All four reach the reference elevation at 2 s. In Cases C/D, the maximum center height is 5 m above the initial elevation, reached at 1 s. The 3D case has horizontal speed `sqrt(6^2 + 8^2) = 10 m/s`, horizontal displacement `(12,16)` m and horizontal distance 20 m. Cases A/B reach their maximum at release (20 m above reference elevation).

**Future Issue #4 verification tests:** assert starting and intermediate state values as well as final state/time within explicitly documented double-precision absolute tolerances. An absolute tolerance of `1e-9` in the relevant SI unit is a **provisional recommendation for these initial analytical gravity fixtures**, not a universal numerical accuracy guarantee. Actual solver tolerances, numerical accuracy, and convergence testing will be reviewed and established in Issue #4. Also test `g = 0` constant velocity, a downward initial velocity, an end time not divisible by `dt`, and one case using standard gravity `9.80665 m/s²` as an additional reference. For example, dropping from `(0,0,20)` m at rest gives `z(2) = 0.3867 m` and `vz(2) = -19.6133 m/s` under standard gravity. Tests should compare against independent analytical formulas, not another invocation of the solver.

## Ground contact extension (Issue #5; not required for #4)

A flat ground surface has configurable elevation `zg`. A spherical ball with radius `R > 0` first touches it when **center height** reaches `zg + R`, on a relevant downward crossing. An initially touching ball moving upward should be allowed to launch; an initially touching ball moving downward requires explicit termination behavior. Detect the crossing time between numerical steps rather than treating the first below-ground sample as the landing instant.

For contact-aware fixtures, define release height `h` **above contact height** and set `z0 = zg + R + h`. This avoids confusing the center's `z` coordinate with the ball's lowest point. Ground contact, bounce, rolling, and fair/foul rulings are separate physical/rules features: #5 handles only first-contact termination and reporting.

## Deferred capabilities and explicit non-decisions

- Aerodynamic drag, atmosphere, wind, and lift/Magnus force are introduced incrementally in later M1 issues.
- Spin is an eventual 3D angular-velocity vector (rad/s) in the adopted field frame, with direction given by the ordinary right-hand rule. Its representation, persistence, evolution, and aerodynamic coupling will be specified when the spin model requires them. Initial gravity-only state and integration do not need spin.
- Ball orientation, seam geometry, quaternions, rotational torque, seam-shifted wake, and detailed collision dynamics are deferred. Their future introduction should be supported by model and state evolution, **not** by placeholder physics fields in this first solver.
- No assumption is made that all future moving objects (e.g., players) must use this trajectory model. Reuse mathematical components when justified.
- Empirical baseball-flight agreement is **validation** and is not a pass criterion for gravity-only verification. Later calibrated models must be assessed with their own evidence and assumptions.

## Adoption and implementation staging

- The conventions and analytical fixtures above are adopted as the provisional M1 baseline.
- [`docs/architecture.md`](../architecture.md) summarizes the same coordinate convention; mathematical details remain here.
- Issue #3's implemented inputs, validation decisions, and deferred output/interface contracts are documented in [`gravity-only-contract.md`](gravity-only-contract.md).
- Numerical integration and trajectory verification are Issue #4 work; contact-aware termination and measurements are Issue #5 work.
