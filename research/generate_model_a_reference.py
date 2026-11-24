"""Offline Model A reference data. Run from the repository root with Python 3.11+.

    python research/generate_model_a_reference.py --output tests/Lpb.Physics.Tests/Fixtures/model-a-reference.json

Uses only the standard library. Neither the simulator nor its acceptance policy
is an input to the reference calculation.
"""

import argparse
from decimal import Context, Decimal as D, ROUND_HALF_EVEN, localcontext
import json
from pathlib import Path


MASS, RADIUS, DENSITY, DRAG, GRAVITY = .145, .0366, 1.225, .35, 9.80665
INERTIA, BETA = (.4 * MASS) * (RADIUS * RADIUS), .009
INITIAL_TRANSLATION = (0., 0., 1.0366, 30., 20., 25.)
SPINS = {"transverse": (-100, 150, 0), "mixed": (-50, 200, 80),
         "initiallyGyro": (150, 100, 125)}
PI = D("3.141592653589793238462643383279502884197169399375105820974944592307816"
       "406286208998628034825342117067982148086513282306647")
STEPS = (.04, .02, .01)
DURATION = 2.
SETTINGS = [(60, "0.1", range(4, 15, 2)), (80, "0.05", range(4, 17, 2)),
            (90, "0.025", range(4, 19, 2))]
STABILIZATION_TARGET = D("1e-16")


def norm(v):
    return sum(x * x for x in v).sqrt()


def rhs(damping, binary64_coefficients):
    m, r, rho, cd, g, inertia, beta = map(
        D.from_float, (MASS, RADIUS, DENSITY, DRAG, GRAVITY, INERTIA, BETA))
    pi, amplitude, slope = (PI, D(".336"), D("6.041"))
    if binary64_coefficients:
        pi, amplitude, slope = (D.from_float(float(c)) for c in (pi, amplitude, slope))
    area = pi * r**2
    density_area = rho * area
    gamma = density_area * beta * r**2 / inertia if damping else D(0)

    def evaluate(y):
        vx, vy, vz, wx, wy, wz = y[3:9]
        velocity, spin = (vx, vy, vz), (wx, wy, wz)
        speed = norm(velocity)
        q_area = density_area * speed**2 / 2
        lift = (D(0),) * 3
        if speed and any(spin):
            cross = (wy * vz - wz * vy, wz * vx - wx * vz, wx * vy - wy * vx)
            transverse = norm(cross)
            if transverse:
                exponent = -slope * r * transverse / speed**2
                lift = tuple(q_area * amplitude * (1 - exponent.exp()) * c / transverse
                             for c in cross)
        drag_rate = density_area * cd * speed / (2 * m)
        acceleration = tuple(lift[i] / m - drag_rate * v - (g if i == 2 else 0)
                             for i, v in enumerate(velocity))
        angular_rate = tuple(-gamma * speed * w for w in spin)
        # Two passive quadratures integrate positive dissipated energy in joules.
        loss = (q_area * cd * speed, inertia * gamma * speed * sum(w*w for w in spin))
        return velocity + acceleration + angular_rate + loss

    return evaluate


def modified_midpoint(y, interval, count, evaluate):
    delta = interval / count
    previous = y
    current = tuple(a + delta * v for a, v in zip(y, evaluate(y)))
    for _ in range(1, count):
        previous, current = current, tuple(a + 2 * delta * v
                                          for a, v in zip(previous, evaluate(current)))
    return tuple((a + b + delta * v) / 2 for a, b, v in zip(previous, current, evaluate(current)))


def extrapolate(y, interval, counts, evaluate):
    rows = []
    for i, count in enumerate(counts):
        row = [modified_midpoint(y, interval, count, evaluate)]
        for j in range(1, i + 1):
            prior_count = counts[i - j]
            factor = D(prior_count**2) / (count**2 - prior_count**2)
            row.append(tuple(a + factor * (a - b) for a, b in zip(row[-1], rows[-1][j - 1])))
        rows.append(row)
    return rows[-1][-1]


def output_times():
    # The union of recorded binary64 timestamps for the three requested grids.
    return sorted({0., DURATION} | {i * h for h in STEPS for i in range(1, round(DURATION / h))})


def integrate(settings, spin, damping, *, binary64_coefficients=False):
    precision, step, counts = settings
    with localcontext(Context(prec=precision, rounding=ROUND_HALF_EVEN)):
        y = tuple(map(D.from_float, (*INITIAL_TRANSLATION, *spin))) + (D(0), D(0))
        evaluate = rhs(damping, binary64_coefficients)
        h = D(step)
        index, anchor = 0, D(0)
        output = []
        for binary_time in output_times():
            time = D.from_float(binary_time)
            while (index + 1) * h <= time:
                y = extrapolate(y, h, counts, evaluate)
                index += 1
                anchor = index * h
            # Evaluate at the exact binary64 timestamp from the preceding reference
            # anchor, without changing the reference mesh or interpolating states.
            output.append(y if time == anchor else extrapolate(y, time - anchor, counts, evaluate))
        return output


def differences(first, second):
    with localcontext(Context(prec=100, rounding=ROUND_HALF_EVEN)):
        return [max(abs(a[i] - b[i]) for a, b in zip(first, second)) for i in range(11)]


def generate():
    scenarios = []
    for name, spin in SPINS.items():
        for damping in (False, True):
            coarse = integrate(SETTINGS[0], spin, damping)
            fine = integrate(SETTINGS[1], spin, damping)
            comparisons = {"60vs80": differences(coarse, fine)}
            selected, selected_settings = fine, SETTINGS[1]
            if name == "initiallyGyro":
                selected_settings = SETTINGS[2]
                selected = integrate(selected_settings, spin, damping)
                comparisons["80vs90"] = differences(fine, selected)
            if any(max(diff) > STABILIZATION_TARGET for diff in comparisons.values()):
                raise ArithmeticError(f"Reference stabilization failed for {name}, damping={damping}: {comparisons}")
            binary = integrate(selected_settings, spin, damping, binary64_coefficients=True)
            scenarios.append({
                "name": name,
                "rotation": "R1U" if damping else "R0",
                "spin": spin,
                "selectedPrecision": selected_settings[0],
                "settingDifferences": {key: list(map(str, diff)) for key, diff in comparisons.items()},
                "binary64CoefficientDifferences": list(map(str, differences(selected, binary))),
                "samples": [[time, *map(float, state)] for time, state in zip(output_times(), selected)],
            })
    return {
        "method": "Decimal extrapolated modified midpoint with endpoint smoothing",
        "inputs": {"mass": MASS, "radius": RADIUS, "density": DENSITY, "drag": DRAG,
                   "gravity": GRAVITY, "analyticalInertia": INERTIA, "beta": BETA,
                   "initialTranslation": INITIAL_TRANSLATION, "duration": DURATION},
        "inertiaBinary64": INERTIA.hex(),
        "timesteps": STEPS,
        "coefficients": {"pi": str(PI), "liftAmplitude": "0.336", "liftSlope": "6.041"},
        "settings": [{"precision": p, "macroStep": h, "counts": list(n)} for p, h, n in SETTINGS],
        "stabilizationTarget": str(STABILIZATION_TARGET),
        "sampleColumns": ["time", "x", "y", "z", "vx", "vy", "vz", "wx", "wy", "wz",
                          "translationalDissipation", "rotationalDissipation"],
        "scenarios": scenarios,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(json.dumps(generate(), indent=2, allow_nan=False) + "\n",
                           encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
