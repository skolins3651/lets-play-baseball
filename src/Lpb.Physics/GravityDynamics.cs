namespace Lpb.Physics;

internal sealed class GravityDynamics
{
    private readonly double gravityMagnitude;

    internal GravityDynamics(double gravityMagnitude)
    {
        if (!double.IsFinite(gravityMagnitude) || gravityMagnitude < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gravityMagnitude), gravityMagnitude,
                "Gravity magnitude must be finite and nonnegative.");
        }

        this.gravityMagnitude = gravityMagnitude;
    }

    internal FlightDerivative Evaluate(double time, FlightState state) => new(
        state.Velocity.X, state.Velocity.Y, state.Velocity.Z,
        0, 0, -gravityMagnitude);
}
