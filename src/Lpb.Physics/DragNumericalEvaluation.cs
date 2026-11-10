namespace Lpb.Physics;

// Observed consistency/representation checks, not a truncation or uniform rounding certificate.
internal sealed class DragNumericalEvaluation
{
    internal const double PositionTolerance = 1e-8;
    internal const double VelocityTolerance = 1e-8;
    private readonly GravityDragDynamics dynamics;
    private readonly FlightState initial;
    private readonly double gravity;
    private readonly Func<double, FlightState, FlightDerivative> guardedEvaluate;

    internal DragNumericalEvaluation(FlightRequest request, AerodynamicSettings aerodynamics)
    {
        initial = request.InitialState;
        gravity = request.GravityMagnitude;
        dynamics = new GravityDragDynamics(gravity, aerodynamics);
        guardedEvaluate = Evaluate;
    }

    internal FlightState Step(TrajectorySample anchor, double time) => EvaluateStep(anchor, time).Full;

    // Keep the diagnostic endpoint available for descending contact evidence, without re-evaluation.
    internal (FlightState Full, FlightState Diagnostic) EvaluateStep(TrajectorySample anchor, double time)
    {
        CheckState(anchor.State);
        var full = Rk4Integrator.Step(anchor.Time, time, anchor.State, guardedEvaluate);
        var middle = NumericalEventInterval.InteriorTime(anchor.Time, time);
        var half = Rk4Integrator.Step(anchor.Time, middle, anchor.State, guardedEvaluate);
        var diagnostic = Rk4Integrator.Step(middle, time, half, guardedEvaluate);
        CheckState(full);
        CheckState(half);
        CheckState(diagnostic);
        CheckTransition(anchor.State, full, time - anchor.Time);
        CheckTransition(anchor.State, half, middle - anchor.Time);
        CheckTransition(half, diagnostic, time - middle);
        CheckTransition(half, full, time - middle);
        CheckAgreement(full, diagnostic);
        return (full, diagnostic);
    }

    private FlightDerivative Evaluate(double time, FlightState state)
    {
        CheckState(state);
        return dynamics.Evaluate(time, state);
    }

    internal void CheckState(FlightState state)
    {
        CheckValue(state.Position.X, initial.Position.X, initial.Velocity.X == 0, PositionTolerance, "position.X");
        CheckValue(state.Position.Y, initial.Position.Y, initial.Velocity.Y == 0, PositionTolerance, "position.Y");
        CheckValue(state.Position.Z, initial.Position.Z, gravity == 0 && initial.Velocity.Z == 0, PositionTolerance, "position.Z");
        CheckValue(state.Velocity.X, 0, initial.Velocity.X == 0, VelocityTolerance, "velocity.X");
        CheckValue(state.Velocity.Y, 0, initial.Velocity.Y == 0, VelocityTolerance, "velocity.Y");
        CheckValue(state.Velocity.Z, 0, gravity == 0 && initial.Velocity.Z == 0, VelocityTolerance, "velocity.Z");
    }

    private static void CheckValue(double value, double invariant, bool constant, double tolerance, string component)
    {
        if (!double.IsFinite(value))
            throw new ArithmeticException($"Drag {component} is nonfinite.");
        if (constant)
        {
            if (value != invariant)
                throw new ArithmeticException($"Drag {component} lost its structural invariant.");
            return;
        }
        var magnitude = Math.Abs(value);
        var spacing = UpperDifference(Math.BitIncrement(magnitude), magnitude);
        if (spacing > tolerance / 8)
            throw new ArithmeticException($"Drag {component} precision cannot support the required numerical resolution.");
    }

    internal void CheckTransition(FlightState from, FlightState to)
    {
        CheckDamping(from.Velocity.X, to.Velocity.X, "velocity.X");
        CheckDamping(from.Velocity.Y, to.Velocity.Y, "velocity.Y");
        if (gravity == 0)
            CheckDamping(from.Velocity.Z, to.Velocity.Z, "velocity.Z");
        else if (from.Velocity.Z <= 0 && to.Velocity.Z > 0)
            throw new ArithmeticException("Drag vertical motion reversed from descent to ascent; reduce timestep.");
    }

    internal void CheckTransition(FlightState from, FlightState to, double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed <= 0)
            throw new ArithmeticException("Drag motion checks require a finite positive interval.");
        CheckTransition(from, to);
        CheckMotion(from.Position.X, to.Position.X, from.Velocity.X, elapsed, "position.X");
        CheckMotion(from.Position.Y, to.Position.Y, from.Velocity.Y, elapsed, "position.Y");
        if (gravity == 0)
            CheckMotion(from.Position.Z, to.Position.Z, from.Velocity.Z, elapsed, "position.Z");
    }

    private static void CheckDamping(double from, double to, string component)
    {
        if ((from > 0 && to <= 0) || (from < 0 && to >= 0) || (from == 0 && to != 0))
            throw new ArithmeticException($"Drag {component} lost its sign invariant; reduce timestep.");
        if (Math.Abs(to) > Math.Abs(from))
            throw new ArithmeticException($"Drag {component} magnitude increased under drag-only damping; reduce timestep.");
    }

    private static void CheckMotion(double from, double to, double velocity, double elapsed, string component)
    {
        if (velocity == 0)
        {
            if (to != from)
                throw new ArithmeticException($"Drag {component} moved with zero velocity on an invariant axis.");
            return;
        }
        if ((velocity > 0 && to < from) || (velocity < 0 && to > from))
            throw new ArithmeticException($"Drag {component} displacement opposes its velocity direction; reduce timestep.");

        // |v_i| cannot grow on a drag-only axis, so motion is at most |v_i(start)|*dt.
        // Compare against an outward representable endpoint envelope rather than a
        // subtractive displacement. Allow rounding of the product and final addition;
        // this is not a certificate for accumulated errors or physical contact.
        var travelUpper = TravelUpper(velocity, elapsed, component);
        var endpoint = velocity > 0 ? from + travelUpper : from - travelUpper;
        var outer = velocity > 0 ? Math.BitIncrement(endpoint) : Math.BitDecrement(endpoint);
        if (!double.IsFinite(outer))
            throw new ArithmeticException($"Drag {component} motion bound is not representable.");
        if ((velocity > 0 && to > outer) || (velocity < 0 && to < outer))
            throw new ArithmeticException($"Drag {component} displacement exceeds its initial-speed motion bound; reduce timestep.");
    }

    // Coordinate-update rounding allowances are not evidence of physical contact.
    // In zero gravity, even paired rounded roots must be reachable from BOTH the
    // ordinary anchor and the immutable release state; earlier coordinate rounding
    // must not progressively erase the original clearance.
    internal void CheckContactReachability(TrajectorySample anchor, double time, double height)
    {
        if (gravity != 0) return;
        CheckReachability(anchor.State, time - anchor.Time, height, time, "ordinary-anchor");
        CheckReachability(initial, time, height, time, "release-origin");
    }

    private static void CheckReachability(FlightState origin, double elapsed, double height, double time, string context)
    {
        var clearance = origin.Position.Z - height;
        if (!double.IsFinite(clearance) || clearance < 0 || !double.IsFinite(elapsed) || elapsed <= 0 ||
            origin.Velocity.Z >= 0)
            throw new ArithmeticException($"Drag zero-gravity {context} contact requires representable clearance, time, and downward velocity.");
        var clearanceLower = clearance == 0 ? 0 : Math.BitDecrement(clearance);
        var travelUpper = TravelUpper(origin.Velocity.Z, elapsed, $"{context} contact");
        if (clearanceLower > travelUpper)
            throw new ArithmeticException($"Drag contact at {time:R} s is unreachable within the zero-gravity initial-speed motion bound " +
                $"({context}: clearance lower {clearanceLower:R} m, travel upper {travelUpper:R} m).");
    }

    private static double TravelUpper(double velocity, double elapsed, string component)
    {
        var intervalUpper = Math.BitIncrement(elapsed);
        var travelUpper = Math.BitIncrement(Math.Abs(velocity) * intervalUpper);
        if (!double.IsFinite(intervalUpper) || !double.IsFinite(travelUpper))
            throw new ArithmeticException($"Drag {component} motion bound is not representable.");
        return travelUpper;
    }

    internal static void CheckAgreement(FlightState full, FlightState diagnostic)
    {
        Compare(full.Position.X, diagnostic.Position.X, PositionTolerance, "position.X");
        Compare(full.Position.Y, diagnostic.Position.Y, PositionTolerance, "position.Y");
        Compare(full.Position.Z, diagnostic.Position.Z, PositionTolerance, "position.Z");
        Compare(full.Velocity.X, diagnostic.Velocity.X, VelocityTolerance, "velocity.X");
        Compare(full.Velocity.Y, diagnostic.Velocity.Y, VelocityTolerance, "velocity.Y");
        Compare(full.Velocity.Z, diagnostic.Velocity.Z, VelocityTolerance, "velocity.Z");
    }

    private static void Compare(double first, double second, double tolerance, string component)
    {
        if (UpperDifference(first, second) > tolerance)
            throw new ArithmeticException($"Drag {component} full-step/two-half-step consistency exceeds {tolerance:R}; reduce timestep.");
    }

    internal static double UpperDifference(double first, double second)
    {
        var difference = Math.Abs(first - second);
        var upper = difference == 0 ? 0 : Math.BitIncrement(difference);
        if (!double.IsFinite(first) || !double.IsFinite(second) || !double.IsFinite(upper))
            throw new ArithmeticException("Drag numerical comparison is not representable.");
        return upper;
    }

    internal static void CheckContactHeight(double height) =>
        CheckValue(height, 0, false, PositionTolerance, "contact height");
}
