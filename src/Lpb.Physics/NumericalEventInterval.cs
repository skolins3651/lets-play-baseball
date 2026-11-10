namespace Lpb.Physics;

internal static class NumericalEventInterval
{
    internal static double InteriorTime(double lower, double upper)
    {
        var time = lower + (upper - lower) / 2;
        if (!double.IsFinite(time) || time <= lower || time >= upper)
            throw new ArithmeticException($"Refinement has no representable interior timestamp between {lower:R} and {upper:R} s.");
        return time;
    }

    internal static void ValidateState(FlightState state)
    {
        if (!double.IsFinite(state.Position.X) || !double.IsFinite(state.Position.Y) || !double.IsFinite(state.Position.Z) ||
            !double.IsFinite(state.Velocity.X) || !double.IsFinite(state.Velocity.Y) || !double.IsFinite(state.Velocity.Z))
            throw new ArithmeticException("Refinement requires finite physical states.");
    }
}
