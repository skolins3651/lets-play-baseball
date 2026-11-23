namespace Lpb.Physics;

// Typed nine-component companion: preserves the existing six-component RK4 path verbatim.
internal static class SpinRk4Integrator
{
    internal static SpinFlightState Step(
        double startTime,
        double endTime,
        SpinFlightState state,
        Func<double, SpinFlightState, SpinFlightDerivative> evaluate)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        var interval = endTime - startTime;
        if (!double.IsFinite(startTime) || startTime < 0 || !double.IsFinite(endTime) ||
            endTime <= startTime || !double.IsFinite(interval) || interval <= 0)
            throw new ArithmeticException($"Spin RK4 requires finite, progressing times; received {startTime:R} to {endTime:R} s.");

        var halfStep = interval / 2;
        var sixthStep = interval / 6;
        if (!double.IsFinite(halfStep) || halfStep <= 0 || !double.IsFinite(sixthStep) || sixthStep <= 0)
            throw new ArithmeticException($"Spin RK4 half-step or sixth-step is unrepresentable for interval {interval:R} s.");

        var midpoint = startTime + halfStep;
        if (!double.IsFinite(midpoint) || midpoint <= startTime || midpoint >= endTime)
            throw new ArithmeticException($"Spin RK4 has no representable interior midpoint between {startTime:R} and {endTime:R} s.");

        var k1 = Evaluate(evaluate, startTime, state, "k1 derivative");
        var k2 = Evaluate(evaluate, midpoint, AddScaled(state, k1, halfStep, "k2 state"), "k2 derivative");
        var k3 = Evaluate(evaluate, midpoint, AddScaled(state, k2, halfStep, "k3 state"), "k3 derivative");
        var k4 = Evaluate(evaluate, endTime, AddScaled(state, k3, interval, "k4 state"), "k4 derivative");

        return new(
            new FlightState(
                new Vector3d(
                    Combine(state.Translation.Position.X, k1.PositionXRate, k2.PositionXRate, k3.PositionXRate, k4.PositionXRate, sixthStep, "position.X"),
                    Combine(state.Translation.Position.Y, k1.PositionYRate, k2.PositionYRate, k3.PositionYRate, k4.PositionYRate, sixthStep, "position.Y"),
                    Combine(state.Translation.Position.Z, k1.PositionZRate, k2.PositionZRate, k3.PositionZRate, k4.PositionZRate, sixthStep, "position.Z")),
                new Vector3d(
                    Combine(state.Translation.Velocity.X, k1.VelocityXRate, k2.VelocityXRate, k3.VelocityXRate, k4.VelocityXRate, sixthStep, "velocity.X"),
                    Combine(state.Translation.Velocity.Y, k1.VelocityYRate, k2.VelocityYRate, k3.VelocityYRate, k4.VelocityYRate, sixthStep, "velocity.Y"),
                    Combine(state.Translation.Velocity.Z, k1.VelocityZRate, k2.VelocityZRate, k3.VelocityZRate, k4.VelocityZRate, sixthStep, "velocity.Z"))),
            new Vector3d(
                Combine(state.AngularVelocity.X, k1.AngularVelocityXRate, k2.AngularVelocityXRate, k3.AngularVelocityXRate, k4.AngularVelocityXRate, sixthStep, "angularVelocity.X"),
                Combine(state.AngularVelocity.Y, k1.AngularVelocityYRate, k2.AngularVelocityYRate, k3.AngularVelocityYRate, k4.AngularVelocityYRate, sixthStep, "angularVelocity.Y"),
                Combine(state.AngularVelocity.Z, k1.AngularVelocityZRate, k2.AngularVelocityZRate, k3.AngularVelocityZRate, k4.AngularVelocityZRate, sixthStep, "angularVelocity.Z")));
    }

    private static SpinFlightDerivative Evaluate(
        Func<double, SpinFlightState, SpinFlightDerivative> evaluate, double time, SpinFlightState state, string stage)
    {
        SpinFlightDerivative derivative;
        try
        {
            // I3 can wrap this delegate to check the same complete stage state and its rates.
            derivative = evaluate(time, state);
        }
        catch (ArithmeticException exception)
        {
            throw new ArithmeticException($"Spin RK4 {stage} at {time:R} s: {exception.Message}", exception);
        }
        Finite(derivative.PositionXRate, stage, "position.X rate");
        Finite(derivative.PositionYRate, stage, "position.Y rate");
        Finite(derivative.PositionZRate, stage, "position.Z rate");
        Finite(derivative.VelocityXRate, stage, "velocity.X rate");
        Finite(derivative.VelocityYRate, stage, "velocity.Y rate");
        Finite(derivative.VelocityZRate, stage, "velocity.Z rate");
        Finite(derivative.AngularVelocityXRate, stage, "angularVelocity.X rate");
        Finite(derivative.AngularVelocityYRate, stage, "angularVelocity.Y rate");
        Finite(derivative.AngularVelocityZRate, stage, "angularVelocity.Z rate");
        return derivative;
    }

    private static SpinFlightState AddScaled(SpinFlightState state, SpinFlightDerivative derivative, double scale, string stage) => new(
        new FlightState(
            new Vector3d(
                Advance(state.Translation.Position.X, derivative.PositionXRate, scale, stage, "position.X"),
                Advance(state.Translation.Position.Y, derivative.PositionYRate, scale, stage, "position.Y"),
                Advance(state.Translation.Position.Z, derivative.PositionZRate, scale, stage, "position.Z")),
            new Vector3d(
                Advance(state.Translation.Velocity.X, derivative.VelocityXRate, scale, stage, "velocity.X"),
                Advance(state.Translation.Velocity.Y, derivative.VelocityYRate, scale, stage, "velocity.Y"),
                Advance(state.Translation.Velocity.Z, derivative.VelocityZRate, scale, stage, "velocity.Z"))),
        new Vector3d(
            Advance(state.AngularVelocity.X, derivative.AngularVelocityXRate, scale, stage, "angularVelocity.X"),
            Advance(state.AngularVelocity.Y, derivative.AngularVelocityYRate, scale, stage, "angularVelocity.Y"),
            Advance(state.AngularVelocity.Z, derivative.AngularVelocityZRate, scale, stage, "angularVelocity.Z")));

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
            throw new ArithmeticException($"Spin RK4 {stage}: {component} is nonfinite ({value:R}).");
        return value;
    }
}
