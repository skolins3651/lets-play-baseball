namespace Lpb.Physics;

internal static class Rk4Integrator
{
    internal static FlightState Step(
        double startTime,
        double endTime,
        FlightState state,
        Func<double, FlightState, FlightDerivative> evaluate)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        var interval = endTime - startTime;
        if (!double.IsFinite(startTime) || startTime < 0 || !double.IsFinite(endTime) ||
            endTime <= startTime || !double.IsFinite(interval) || interval <= 0)
        {
            throw new ArithmeticException($"RK4 requires finite, progressing times; received {startTime:R} to {endTime:R} s.");
        }

        var halfStep = interval / 2;
        var sixthStep = interval / 6;
        if (!double.IsFinite(halfStep) || halfStep <= 0 || !double.IsFinite(sixthStep) || sixthStep <= 0)
        {
            throw new ArithmeticException($"RK4 half-step or sixth-step is unrepresentable for interval {interval:R} s.");
        }

        var midpoint = startTime + halfStep;
        if (!double.IsFinite(midpoint) || midpoint <= startTime || midpoint >= endTime)
        {
            throw new ArithmeticException($"RK4 has no representable interior midpoint between {startTime:R} and {endTime:R} s.");
        }

        var k1 = Evaluate(evaluate, startTime, state, "k1 derivative");
        var k2 = Evaluate(evaluate, midpoint, AddScaled(state, k1, halfStep, "k2 state"), "k2 derivative");
        var k3 = Evaluate(evaluate, midpoint, AddScaled(state, k2, halfStep, "k3 state"), "k3 derivative");
        var k4 = Evaluate(evaluate, endTime, AddScaled(state, k3, interval, "k4 state"), "k4 derivative");

        return new FlightState(
            new Vector3d(
                Combine(state.Position.X, k1.PositionXRate, k2.PositionXRate, k3.PositionXRate, k4.PositionXRate, sixthStep, "position.X"),
                Combine(state.Position.Y, k1.PositionYRate, k2.PositionYRate, k3.PositionYRate, k4.PositionYRate, sixthStep, "position.Y"),
                Combine(state.Position.Z, k1.PositionZRate, k2.PositionZRate, k3.PositionZRate, k4.PositionZRate, sixthStep, "position.Z")),
            new Vector3d(
                Combine(state.Velocity.X, k1.VelocityXRate, k2.VelocityXRate, k3.VelocityXRate, k4.VelocityXRate, sixthStep, "velocity.X"),
                Combine(state.Velocity.Y, k1.VelocityYRate, k2.VelocityYRate, k3.VelocityYRate, k4.VelocityYRate, sixthStep, "velocity.Y"),
                Combine(state.Velocity.Z, k1.VelocityZRate, k2.VelocityZRate, k3.VelocityZRate, k4.VelocityZRate, sixthStep, "velocity.Z")));
    }

    private static FlightDerivative Evaluate(
        Func<double, FlightState, FlightDerivative> evaluate, double time, FlightState state, string stage)
    {
        var derivative = evaluate(time, state);
        Finite(derivative.PositionXRate, stage, "position.X rate");
        Finite(derivative.PositionYRate, stage, "position.Y rate");
        Finite(derivative.PositionZRate, stage, "position.Z rate");
        Finite(derivative.VelocityXRate, stage, "velocity.X rate");
        Finite(derivative.VelocityYRate, stage, "velocity.Y rate");
        Finite(derivative.VelocityZRate, stage, "velocity.Z rate");
        return derivative;
    }

    private static FlightState AddScaled(FlightState state, FlightDerivative derivative, double scale, string stage) => new(
        new Vector3d(
            Advance(state.Position.X, derivative.PositionXRate, scale, stage, "position.X"),
            Advance(state.Position.Y, derivative.PositionYRate, scale, stage, "position.Y"),
            Advance(state.Position.Z, derivative.PositionZRate, scale, stage, "position.Z")),
        new Vector3d(
            Advance(state.Velocity.X, derivative.VelocityXRate, scale, stage, "velocity.X"),
            Advance(state.Velocity.Y, derivative.VelocityYRate, scale, stage, "velocity.Y"),
            Advance(state.Velocity.Z, derivative.VelocityZRate, scale, stage, "velocity.Z")));

    private static double Advance(double initial, double rate, double scale, string stage, string component)
    {
        var increment = Finite(scale * rate, stage, component);
        return Finite(initial + increment, stage, component);
    }

    private static double Combine(double initial, double k1, double k2, double k3, double k4, double sixthStep, string component)
    {
        var twiceK2 = Finite(2 * k2, "weighted combination", component);
        var twiceK3 = Finite(2 * k3, "weighted combination", component);
        var sum = Finite(k1 + twiceK2, "weighted combination", component);
        sum = Finite(sum + twiceK3, "weighted combination", component);
        sum = Finite(sum + k4, "weighted combination", component);
        var increment = Finite(sixthStep * sum, "final increment", component);
        return Finite(initial + increment, "completed state", component);
    }

    private static double Finite(double value, string stage, string component)
    {
        if (!double.IsFinite(value))
        {
            throw new ArithmeticException($"RK4 {stage}: {component} is nonfinite ({value:R}).");
        }

        return value;
    }
}
