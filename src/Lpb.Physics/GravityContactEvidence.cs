namespace Lpb.Physics;

// Constant-gravity event evidence only. This never advances or adjusts a state.
// The gravity contract derives the bounds, compensation, and first-contact proof.
internal static class GravityContactEvidence
{
    private static readonly double SafeProductMagnitude = Math.ScaleB(1d, -968);

    internal readonly record struct Bounds(double Lower, double Upper)
    {
        internal bool IsZero => Lower == 0 && Upper == 0;
    }

    internal static void Validate(TrajectorySample anchor, FlightState release, double time, double height, double gravity)
    {
        if (!double.IsFinite(time) || !double.IsFinite(height) || !double.IsFinite(gravity) || gravity < 0)
            throw new ArithmeticException("Gravity contact evidence requires finite time/geometry and nonnegative gravity.");
        var initial = new TrajectorySample(0, release);
        ValidateOrigin(anchor, time, height, "ordinary-anchor");
        ValidateOrigin(initial, time, height, "release-origin");
        var local = EvaluateContext(anchor, time, height, gravity, "ordinary-anchor candidate");
        var original = EvaluateContext(initial, time, height, gravity, "release-origin candidate");
        CheckOccurrence(local, time, "ordinary-anchor");
        CheckOccurrence(original, time, "release-origin");
        if (local.Clearance.IsZero && original.Clearance.IsZero)
            return;

        var (earlier, _) = CreateWindow(anchor.Time, time);
        if (!local.Clearance.IsZero)
            CheckEarlier(anchor, earlier, time, height, gravity, "ordinary-anchor");
        if (!original.Clearance.IsZero)
            CheckEarlier(initial, earlier, time, height, gravity, "release-origin");
    }

    internal static (Bounds Clearance, Bounds Velocity) Evaluate(
        TrajectorySample origin, double time, double height, double gravity)
    {
        if (!double.IsFinite(origin.Time) || origin.Time < 0 || !double.IsFinite(time) || time < origin.Time ||
            !double.IsFinite(height) || !double.IsFinite(gravity) || gravity < 0)
            throw new ArithmeticException("Gravity contact evaluation requires finite, ordered times and valid geometry/gravity.");
        var elapsed = Add(time, -origin.Time);
        var clearance = Add(origin.State.Position.Z, -height);
        var velocity = Point(origin.State.Velocity.Z);
        var acceleration = Point(-gravity);
        var halfElapsed = Multiply(elapsed, Point(.5));
        var rate = Affine(acceleration, halfElapsed, velocity);
        return (Affine(elapsed, rate, clearance), Affine(acceleration, elapsed, velocity));
    }

    internal static (double Earlier, Bounds Width) CreateWindow(double anchorTime, double time)
    {
        if (!double.IsFinite(anchorTime) || anchorTime < 0 || !double.IsFinite(time) || time <= anchorTime)
            throw new ArithmeticException("Gravity contact timing requires a finite, progressing ordinary interval.");
        // Add's upper endpoint is ceil-binary64(t - tolerance), not an arbitrary ULP allowance.
        var earlier = Math.Max(anchorTime, Math.Max(0, Add(time, -GravityOnlyRefinement.TimeTolerance).Upper));
        if (earlier >= time)
            throw new ArithmeticException($"Gravity contact timing at {time:R} s has no representable earlier timestamp " +
                $"within {GravityOnlyRefinement.TimeTolerance:R} s (ordinary anchor {anchorTime:R} s).");
        var width = Add(time, -earlier);
        if (width.Lower <= 0 || width.Upper > GravityOnlyRefinement.TimeTolerance)
            throw new ArithmeticException($"Gravity contact window {earlier:R} to {time:R} s cannot certify the required width.");
        return (earlier, width);
    }

    internal static Bounds Add(double first, double second)
    {
        var (sum, error) = TwoSum(first, second);
        return Directed(sum, error);
    }

    internal static Bounds Multiply(double first, double second)
    {
        var product = Finite(first * second, "product");
        if (StructuralProductIsExact(first, second, product))
            return Point(product);
        var error = Finite(Math.FusedMultiplyAdd(first, second, -product), "FMA product residual");
        // A nonzero rounded residual always has the true error's sign. Zero requires proof.
        if (error != 0 || Math.Abs(product) >= SafeProductMagnitude)
            return Directed(product, error);
        return new Bounds(Finite(Math.BitDecrement(product), "lower product endpoint"),
            Finite(Math.BitIncrement(product), "upper product endpoint"));
    }

    internal static Bounds Affine(double first, double second, double addend)
    {
        var product = Finite(first * second, "affine product");
        double productError;
        if (StructuralProductIsExact(first, second, product))
            productError = 0;
        else if (Math.Abs(product) >= SafeProductMagnitude)
            productError = Finite(Math.FusedMultiplyAdd(first, second, -product), "exact affine product residual");
        else
            return Add(Multiply(first, second), Point(addend));

        // p + ep = a*b; s + es = p+c. Keep ep/es until their cancellation is certified.
        var (sum, sumError) = TwoSum(product, addend);
        var correction = Add(productError, sumError);
        return Add(Point(sum), correction);
    }

    private static (double Sum, double Error) TwoSum(double first, double second)
    {
        var sum = Finite(first + second, "TwoSum sum");
        var virtualSecond = Finite(sum - first, "TwoSum virtual addend");
        var recoveredFirst = Finite(sum - virtualSecond, "TwoSum recovered addend");
        var firstError = Finite(first - recoveredFirst, "TwoSum first residual");
        var secondError = Finite(second - virtualSecond, "TwoSum second residual");
        return (sum, Finite(firstError + secondError, "TwoSum residual"));
    }

    private static bool StructuralProductIsExact(double first, double second, double product)
    {
        if (first == 0 || second == 0 || Math.Abs(first) == 1 || Math.Abs(second) == 1)
            return true;
        // Finite doubling is exact, including for subnormals. A failed round trip proves nothing.
        if (Math.Abs(first) == .5)
            return double.IsFinite(2 * product) && 2 * product == (first > 0 ? second : -second);
        if (Math.Abs(second) == .5)
            return double.IsFinite(2 * product) && 2 * product == (second > 0 ? first : -first);
        return false;
    }

    // Zero error is used only after TwoSum, structural exactness, or the safe-product proof.
    private static Bounds Directed(double value, double error) => new(
        error < 0 ? Finite(Math.BitDecrement(value), "lower outward endpoint") : value,
        error > 0 ? Finite(Math.BitIncrement(value), "upper outward endpoint") : value);

    private static Bounds Point(double value) => new(Finite(value, "singleton endpoint"), value);

    private static Bounds Add(Bounds first, Bounds second) =>
        new(Add(first.Lower, second.Lower).Lower, Add(first.Upper, second.Upper).Upper);

    private static Bounds Multiply(Bounds first, Bounds second)
    {
        var lower = double.PositiveInfinity;
        var upper = double.NegativeInfinity;
        for (var i = 0; i < Count(first); i++)
        for (var j = 0; j < Count(second); j++)
        {
            var product = Multiply(Endpoint(first, i), Endpoint(second, j));
            lower = Math.Min(lower, product.Lower);
            upper = Math.Max(upper, product.Upper);
        }
        return new Bounds(lower, upper);
    }

    private static Bounds Affine(Bounds first, Bounds second, Bounds addend)
    {
        var lower = double.PositiveInfinity;
        var upper = double.NegativeInfinity;
        for (var i = 0; i < Count(first); i++)
        for (var j = 0; j < Count(second); j++)
        for (var k = 0; k < Count(addend); k++)
        {
            var value = Affine(Endpoint(first, i), Endpoint(second, j), Endpoint(addend, k));
            lower = Math.Min(lower, value.Lower);
            upper = Math.Max(upper, value.Upper);
        }
        return new Bounds(lower, upper);
    }

    private static int Count(Bounds bounds) => bounds.Lower == bounds.Upper ? 1 : 2;
    private static double Endpoint(Bounds bounds, int index) => index == 0 ? bounds.Lower : bounds.Upper;

    private static double Finite(double value, string operation)
    {
        if (!double.IsFinite(value))
            throw new ArithmeticException($"Gravity contact {operation} is nonfinite or cannot be represented outward.");
        return value;
    }

    private static void ValidateOrigin(TrajectorySample origin, double time, double height, string context)
    {
        var z = origin.State.Position.Z;
        var v = origin.State.Velocity.Z;
        if (!double.IsFinite(origin.Time) || origin.Time < 0 || origin.Time >= time ||
            !double.IsFinite(z) || !double.IsFinite(v) ||
            !(z > height || (origin.Time == 0 && z == height && v > 0)))
            throw new ArithmeticException($"Gravity contact {context} requires an airborne origin or the initial upward launch.");
    }

    private static (Bounds Clearance, Bounds Velocity) EvaluateContext(
        TrajectorySample origin, double time, double height, double gravity, string context)
    {
        try { return Evaluate(origin, time, height, gravity); }
        catch (ArithmeticException exception)
        {
            throw new ArithmeticException($"Gravity contact {context} evidence at {time:R} s from {origin.Time:R} s failed: " +
                exception.Message, exception);
        }
    }

    private static void CheckOccurrence((Bounds Clearance, Bounds Velocity) evidence, double time, string context)
    {
        if (evidence.Clearance.Lower > 0)
            throw new ArithmeticException($"Gravity contact at {time:R} s is still airborne ({context}: " +
                $"clearance lower {evidence.Clearance.Lower:R} m).");
        if (evidence.Clearance.Upper > 0)
            throw new ArithmeticException($"Gravity contact occurrence at {time:R} s is unresolved ({context}: " +
                $"clearance [{evidence.Clearance.Lower:R}, {evidence.Clearance.Upper:R}] m).");
        if (evidence.Velocity.Upper >= 0)
            throw new ArithmeticException($"Gravity contact descending direction at {time:R} s is unresolved ({context}: " +
                $"velocity upper {evidence.Velocity.Upper:R} m/s).");
    }

    private static void CheckEarlier(TrajectorySample origin, double earlier, double time,
        double height, double gravity, string context)
    {
        var evidence = EvaluateContext(origin, earlier, height, gravity, $"{context} earlier-window");
        if (evidence.Clearance.Lower < 0)
        {
            // Before this rounded cutoff does not necessarily mean more than tolerance late.
            var reason = evidence.Clearance.Upper < 0 ? "crossing is before the selected window" : "earlier clearance sign is unresolved";
            throw new ArithmeticException($"Gravity contact timing at {time:R} s cannot be certified ({context}: " +
                $"{reason}; earlier time {earlier:R} s, clearance [{evidence.Clearance.Lower:R}, {evidence.Clearance.Upper:R}] m).");
        }
    }
}
