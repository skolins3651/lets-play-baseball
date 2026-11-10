"""Independent Issue #6 fixtures; Python standard library only, never a build/CI input.

Run at repository root:
  python3 research/generate_drag_reference.py > tests/Lpb.Physics.Tests/Fixtures/drag-reference.json

Modified midpoint with endpoint smoothing has an even-power error expansion.
Extrapolate in 1/n² over independent substep counts. No production RK4, dynamics,
or event code is imported. This is focused verification, not empirical calibration.
"""

from decimal import Decimal as D, localcontext
import json

PI = D("3.141592653589793238462643383279502884197169399375105820974944592307816406286208998628")


def generate(precision, macro_step, counts):
    with localcontext() as context:
        context.prec = precision
        k = D("1.225") * D(".35") * PI * D(".0366") ** 2 / (2 * D(".145"))
        g = D("9.80665")
        radius = D(".0366")

        def derivative(y):
            speed = sum(v * v for v in y[3:]).sqrt()
            return y[3:] + tuple(-k * speed * v - (g if i == 2 else 0) for i, v in enumerate(y[3:]))

        def midpoint(y, interval, count):
            h = interval / count
            previous = y
            current = tuple(v + h * rate for v, rate in zip(y, derivative(y)))
            for _ in range(1, count):
                previous, current = current, tuple(v + 2 * h * rate for v, rate in zip(previous, derivative(current)))
            return tuple((a + b + h * rate) / 2 for a, b, rate in zip(previous, current, derivative(current)))

        def advance(y, interval):
            rows = []
            for i, count in enumerate(counts):
                row = [midpoint(y, interval, count)]
                for j in range(1, i + 1):
                    ratio = (D(count) / counts[i - j]) ** 2 - 1
                    row.append(tuple(a + (a - b) / ratio for a, b in zip(row[j - 1], rows[i - 1][j - 1])))
                rows.append(row)
            return rows[-1][-1]

        def event(anchor_time, anchor, interval, value):
            lower, upper = D(0), interval
            for _ in range(160):
                trial = (lower + upper) / 2
                state = advance(anchor, trial)
                if value(state) > 0:
                    lower = trial
                else:
                    upper = trial
                if upper - lower <= D("1e-32"):
                    time = (lower + upper) / 2
                    return anchor_time + time, advance(anchor, time)
            raise ArithmeticError("Independent event reference did not converge")

        h = D(macro_step)
        y = tuple(map(D, ("0", "0", "1.0366", "30", "20", "25")))
        time = D(0)
        output = {"samples": [], "events": {}}
        for index in range(1, int(D(6) / h) + 1):
            next_time = index * h
            next_y = advance(y, next_time - time)
            if y[5] > 0 >= next_y[5]:
                output["events"]["apex"] = event(time, y, next_time - time, lambda state: state[5])
            if y[2] > radius >= next_y[2]:
                output["events"]["contact"] = event(time, y, next_time - time, lambda state: state[2] - radius)
            if next_time in (D(1), D(2), D(3), D(4), D(6)):
                output["samples"].append((next_time, next_y))
            y, time = next_y, next_time
        return output


def main():
    coarse = generate(55, ".1", (4, 6, 8, 10, 12, 14))
    fine = generate(70, ".05", (4, 6, 8, 10, 12, 14, 16))
    state_difference = max(abs(a - b) for (_, first), (_, second) in zip(coarse["samples"], fine["samples"])
                           for a, b in zip(first, second))
    event_time_difference = max(abs(coarse["events"][name][0] - fine["events"][name][0]) for name in fine["events"])
    event_state_difference = max(abs(a - b) for name in fine["events"]
                                 for a, b in zip(coarse["events"][name][1], fine["events"][name][1]))
    if max(state_difference, event_time_difference, event_state_difference) > D("1e-20"):
        raise ArithmeticError("Independent reference settings disagree beyond 1e-20")
    def encode(item):
        time, state = item
        return {"time": float(time), "state": [float(v) for v in state]}
    output = {
        "method": "decimal extrapolated modified midpoint; fixed decimal SI inputs; independent event bisection",
        "parameters": {"mass": .145, "radius": .0366, "density": 1.225, "coefficient": .35, "gravity": 9.80665},
        "initialState": [0, 0, 1.0366, 30, 20, 25],
        "verification": {"settings": "55 digits/h=.1/n=4..14 versus 70 digits/h=.05/n=4..16",
                         "maximumStateDifference": str(state_difference), "maximumEventTimeDifference": str(event_time_difference),
                         "maximumEventStateDifference": str(event_state_difference)},
        "samples": [encode(item) for item in fine["samples"]],
        "events": {name: encode(item) for name, item in fine["events"].items()},
    }
    print(json.dumps(output, indent=2, sort_keys=True, allow_nan=False))


if __name__ == "__main__":
    main()
