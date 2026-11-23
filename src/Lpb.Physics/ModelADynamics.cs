namespace Lpb.Physics;

// Complete instantaneous stationary-air derivative; no numerical acceptance or flight execution.
internal sealed class ModelADynamics
{
    private readonly GravityDragDynamics gravityDrag;
    private readonly ModelAMagnusKernel magnus;
    private readonly ModelARotationKernel rotation;

    internal ModelADynamics(double gravityMagnitude, ModelAParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var aerodynamics = parameters.Aerodynamics;
        gravityDrag = new(gravityMagnitude, aerodynamics);
        magnus = new(aerodynamics);
        rotation = parameters.Rotation.Kind switch
        {
            ModelARotationKind.R0 => ModelARotationKernel.R0(),
            ModelARotationKind.R1U => ModelARotationKernel.R1U(aerodynamics,
                parameters.Rotation.RotationalInertia!.Value, parameters.Rotation.Beta!.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(parameters), "Unsupported Model A rotational selection.")
        };
    }

    internal SpinFlightDerivative Evaluate(double time, SpinFlightState state)
    {
        try
        {
            var velocity = state.Translation.Velocity;
            var spin = state.AngularVelocity;
            var translation = gravityDrag.Evaluate(time, state.Translation);
            var lift = magnus.Evaluate(velocity, spin);
            var angular = rotation.Evaluate(velocity, spin);
            // Keep the legacy gravity/drag expression intact, then add Magnus componentwise.
            return new(translation.PositionXRate, translation.PositionYRate, translation.PositionZRate,
                Combine(translation.VelocityXRate, lift.X, "velocity.X"),
                Combine(translation.VelocityYRate, lift.Y, "velocity.Y"),
                Combine(translation.VelocityZRate, lift.Z, "velocity.Z"),
                angular.X, angular.Y, angular.Z);
        }
        catch (ArithmeticException exception)
        {
            throw new ArithmeticException("Model A coupled dynamics evaluation failed: " + exception.Message, exception);
        }
    }

    private static double Combine(double gravityDrag, double magnus, string component)
    {
        var acceleration = gravityDrag + magnus;
        if (!double.IsFinite(acceleration))
            throw new ArithmeticException($"Combined acceleration {component} is nonfinite ({acceleration:R}).");
        return acceleration;
    }
}
