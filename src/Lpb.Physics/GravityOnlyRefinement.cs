namespace Lpb.Physics;

// Brackets and uncertainty estimates rely on constant nonnegative gravity. Trial states
// must always be advanced from the original ordinary interval start, not a narrowed endpoint.
internal static class GravityOnlyRefinement
{
    internal const int MaximumEvaluations = 128;
    internal const double TimeTolerance = 1e-10;
    internal const double PositionTolerance = 1e-9;
    internal const double VelocityTolerance = 1e-9;

    // Absolute underflow allowance for the current finite RK4 expression, including
    // propagation through a finite coefficient/rate. See the contract's error budget.
    private static readonly double EvaluationUnderflowAllowance = Math.BitIncrement(256 * double.Epsilon * double.MaxValue);

    // Completed velocities use constant acceleration directly, not rounded stage velocities.
    private static readonly double VelocityEvaluationUnderflowAllowance = Math.BitIncrement(16 * double.Epsilon * double.MaxValue);

    internal static TrajectorySample FindContact(TrajectorySample lower, TrajectorySample upper,
        double height, double gravity, Func<double, FlightState> evaluate)
    {
        ValidateInterval(lower, upper, gravity);
        var anchor = lower;
        if (!double.IsFinite(height) || upper.State.Position.Z > height || upper.State.Velocity.Z >= 0)
            throw new ArithmeticException("Contact refinement requires a descending on/below-plane upper endpoint.");
        if (lower.State.Position.Z == height && lower.State.Velocity.Z > 0 && lower.Time == 0)
        {
            // A later descending exact endpoint is distinct from the excluded launch root.
            if (upper.State.Position.Z == height)
                return upper;
            lower = FindApex(lower, upper, gravity, evaluate);
        }
        if (lower.State.Position.Z <= height)
            throw new ArithmeticException("Contact refinement requires a resolvable airborne lower endpoint.");

        for (var evaluations = 0; ; evaluations++)
        {
            if (upper.State.Position.Z == height)
                return upper;
            if (ContactBoundsSatisfied(anchor, lower, upper, height, gravity))
                return new TrajectorySample(upper.Time, new FlightState(
                    new Vector3d(upper.State.Position.X, upper.State.Position.Y, height), upper.State.Velocity));
            if (evaluations == MaximumEvaluations)
                throw new ArithmeticException($"Contact refinement exceeded {MaximumEvaluations} evaluations.");
            var time = InteriorTime(lower.Time, upper.Time);
            var trial = new TrajectorySample(time, evaluate(time));
            ValidateState(trial.State);
            if (trial.State.Position.Z > height)
                lower = trial;
            else if (trial.State.Velocity.Z < 0)
                upper = trial;
            else
                throw new ArithmeticException("Contact refinement lost its descending-event bracket.");
        }
    }

    internal static TrajectorySample FindApex(TrajectorySample lower, TrajectorySample upper,
        double gravity, Func<double, FlightState> evaluate)
    {
        ValidateInterval(lower, upper, gravity);
        var anchor = lower;
        if (lower.State.Velocity.Z <= 0 || upper.State.Velocity.Z > 0)
            throw new ArithmeticException("Apex refinement requires positive/nonpositive vertical-velocity endpoints.");
        for (var evaluations = 0; ; evaluations++)
        {
            if (upper.State.Velocity.Z == 0 || ApexBoundsSatisfied(anchor, lower, upper, gravity))
                return upper;
            if (evaluations == MaximumEvaluations)
                throw new ArithmeticException($"Apex refinement exceeded {MaximumEvaluations} evaluations.");
            var time = InteriorTime(lower.Time, upper.Time);
            var trial = new TrajectorySample(time, evaluate(time));
            ValidateState(trial.State);
            if (trial.State.Velocity.Z > 0)
                lower = trial;
            else
                upper = trial;
        }
    }

    private static bool ContactBoundsSatisfied(TrajectorySample anchor, TrajectorySample lower,
        TrajectorySample upper, double height, double gravity)
    {
        var width = UpperBound(upper.Time - lower.Time);
        var elapsed = UpperBound(upper.Time - anchor.Time);
        var x = PositionBound(anchor.State.Position.X, anchor.State.Velocity.X, 0, elapsed, width);
        var y = PositionBound(anchor.State.Position.Y, anchor.State.Velocity.Y, 0, elapsed, width);
        var z = PositionBound(anchor.State.Position.Z, anchor.State.Velocity.Z, gravity, elapsed, width);
        var velocityX = VelocityBound(anchor.State.Velocity.X, 0, elapsed, width);
        var velocityY = VelocityBound(anchor.State.Velocity.Y, 0, elapsed, width);
        var velocityZ = VelocityBound(anchor.State.Velocity.Z, gravity, elapsed, width);
        var residual = UpperBound(Math.Abs(upper.State.Position.Z - height));
        return width <= TimeTolerance && residual <= PositionTolerance && x <= PositionTolerance &&
               y <= PositionTolerance && z <= PositionTolerance && velocityX <= VelocityTolerance &&
               velocityY <= VelocityTolerance && velocityZ <= VelocityTolerance;
    }

    // Uniform pairwise uncertainty for RK4 velocities evaluated from one fixed anchor.
    // Internal visibility supports focused tests of the certificate independently of root finding.
    internal static double VelocityBound(double velocity, double acceleration, double elapsed, double width)
    {
        if (!double.IsFinite(velocity) || !double.IsFinite(acceleration) || acceleration < 0 ||
            !double.IsFinite(elapsed) || elapsed < 0 || !double.IsFinite(width) || width < 0 || width > elapsed)
            throw new ArithmeticException("Velocity uncertainty requires finite inputs and a valid interval.");
        // All four acceleration rates and their weighted increment are exactly zero.
        if (acceleration == 0)
            return 0;
        if (Spacing(Math.Abs(velocity)) > VelocityTolerance)
            throw new ArithmeticException("Contact velocity precision cannot certify the required uncertainty.");

        var scale = SumBound(Math.Abs(velocity), ProductBound(acceleration, elapsed));
        var evaluationError = SumBound(ProductBound(16, Spacing(scale)), VelocityEvaluationUnderflowAllowance);
        return SumBound(ProductBound(acceleration, width), ProductBound(2, evaluationError));
    }

    private static double PositionBound(double position, double velocity, double acceleration, double elapsed, double width)
    {
        // Gravity leaves a stationary horizontal coordinate exactly unchanged in RK4.
        if (velocity == 0 && acceleration == 0)
            return 0;
        if (Spacing(Math.Abs(position)) > PositionTolerance)
            throw new ArithmeticException("The position precision cannot certify the required localization uncertainty.");

        // A uniform envelope from the ORIGINAL anchor bounds every interior evaluation,
        // including cancellation and final-addition rounding. Endpoint agreement is insufficient.
        var speed = SumBound(Math.Abs(velocity), ProductBound(acceleration, elapsed));
        var scale = SumBound(Math.Abs(position), ProductBound(speed, elapsed));
        var evaluationError = SumBound(ProductBound(64, Spacing(scale)), EvaluationUnderflowAllowance);
        return SumBound(ProductBound(speed, width), ProductBound(2, evaluationError));
    }

    private static double Spacing(double magnitude) => UpperBound(Math.BitIncrement(magnitude) - magnitude);

    private static double SumBound(double first, double second) => UpperBound(first + second);

    private static bool ApexBoundsSatisfied(TrajectorySample anchor, TrajectorySample lower,
        TrajectorySample upper, double gravity)
    {
        var width = UpperBound(upper.Time - lower.Time);
        var elapsed = UpperBound(upper.Time - anchor.Time);
        var height = PositionBound(anchor.State.Position.Z, anchor.State.Velocity.Z, gravity, elapsed, width);
        // This is an apex time-localization check plus a directly measured numeric residual,
        // not the contact certificate's pairwise velocity-evaluation uncertainty.
        var velocity = ProductBound(gravity, width);
        return width <= TimeTolerance && height <= PositionTolerance && velocity <= VelocityTolerance &&
               Math.Abs(upper.State.Velocity.Z) <= VelocityTolerance;
    }

    private static double ProductBound(double first, double second)
    {
        if (first == 0 || second == 0)
            return 0;
        var product = first * second;
        if (product == 0)
            throw new ArithmeticException("Refinement uncertainty bound underflowed.");
        return UpperBound(product);
    }

    // One outward ULP bounds rounding in each nonnegative subtraction/product used here.
    private static double UpperBound(double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArithmeticException("Refinement uncertainty bound is nonfinite or invalid.");
        var bound = value == 0 ? 0 : Math.BitIncrement(value);
        if (!double.IsFinite(bound))
            throw new ArithmeticException("Refinement uncertainty bound cannot be represented conservatively.");
        return bound;
    }

    private static double InteriorTime(double lower, double upper)
    {
        var time = lower + (upper - lower) / 2;
        if (!double.IsFinite(time) || time <= lower || time >= upper)
            throw new ArithmeticException($"Refinement has no representable interior timestamp between {lower:R} and {upper:R} s.");
        return time;
    }

    private static void ValidateInterval(TrajectorySample lower, TrajectorySample upper, double gravity)
    {
        if (!double.IsFinite(lower.Time) || lower.Time < 0 || !double.IsFinite(upper.Time) || upper.Time <= lower.Time ||
            !double.IsFinite(gravity) || gravity < 0)
            throw new ArithmeticException("Refinement requires a finite, progressing interval and nonnegative finite gravity.");
        ValidateState(lower.State);
        ValidateState(upper.State);
    }

    private static void ValidateState(FlightState state)
    {
        if (!double.IsFinite(state.Position.X) || !double.IsFinite(state.Position.Y) || !double.IsFinite(state.Position.Z) ||
            !double.IsFinite(state.Velocity.X) || !double.IsFinite(state.Velocity.Y) || !double.IsFinite(state.Velocity.Z))
            throw new ArithmeticException("Refinement requires finite physical states.");
    }
}
