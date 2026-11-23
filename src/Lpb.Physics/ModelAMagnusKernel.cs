namespace Lpb.Physics;

// Instantaneous stationary-air Nathan ASR-B acceleration; no flight execution.
internal sealed class ModelAMagnusKernel
{
    private static readonly SpinKernelArithmetic.ScaledNumber Rate = SpinKernelArithmetic.ScaledNumber.From(6.041);
    private readonly SpinKernelArithmetic.ScaledNumber radius;
    private readonly SpinKernelArithmetic.ScaledNumber prefactor;

    internal ModelAMagnusKernel(AerodynamicSettings aerodynamics)
    {
        ArgumentNullException.ThrowIfNull(aerodynamics);
        radius = SpinKernelArithmetic.ScaledNumber.From(aerodynamics.BallRadius);
        // B*a*k*r, with exactly the approved seven operations and exact half shift.
        prefactor = SpinKernelArithmetic.ScaledNumber.From(aerodynamics.AirDensity)
            .Times(SpinKernelArithmetic.ScaledNumber.From(Math.PI))
            .Times(radius).Times(radius)
            .DividedBy(SpinKernelArithmetic.ScaledNumber.From(aerodynamics.BallMass)).Shifted(-1)
            .Times(SpinKernelArithmetic.ScaledNumber.From(.336)).Times(Rate).Times(radius);
    }

    internal Vector3d Evaluate(Vector3d velocity, Vector3d angularVelocity)
    {
        if (prefactor.IsZero)
            return default;
        try
        {
            var cross = SpinKernelArithmetic.Cross(angularVelocity, velocity);
            if (cross.X.IsZero && cross.Y.IsZero && cross.Z.IsZero)
                return default;
            var speed = SpinKernelArithmetic.Norm(velocity);
            var magnitude = SpinKernelArithmetic.Norm(cross.X, cross.Y, cross.Z);
            var argument = Rate.Times(radius).Times(magnitude).DividedBy(speed.Times(speed));
            var factor = prefactor.Times(SpinKernelArithmetic.Phi(argument));
            return new(factor.Times(cross.X).ToDouble("Magnus acceleration.X"),
                factor.Times(cross.Y).ToDouble("Magnus acceleration.Y"),
                factor.Times(cross.Z).ToDouble("Magnus acceleration.Z"));
        }
        catch (ArithmeticException exception)
        {
            throw new ArithmeticException("Model A Magnus evaluation failed: " + exception.Message, exception);
        }
    }
}
