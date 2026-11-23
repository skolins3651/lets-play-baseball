namespace Lpb.Physics;

// Closed R0/R1-U choices. Returns an angular derivative, never a spin update.
internal sealed class ModelARotationKernel
{
    private readonly SpinKernelArithmetic.ScaledNumber prefactor;

    private ModelARotationKernel(SpinKernelArithmetic.ScaledNumber prefactor) => this.prefactor = prefactor;

    internal static ModelARotationKernel R0() => new(default);

    internal static ModelARotationKernel R1U(AerodynamicSettings aerodynamics, double rotationalInertia, double beta)
    {
        ArgumentNullException.ThrowIfNull(aerodynamics);
        if (!double.IsFinite(rotationalInertia) || rotationalInertia <= 0)
            throw new ArgumentOutOfRangeException(nameof(rotationalInertia), rotationalInertia,
                "Rotational inertia must be finite and positive, in kg m².");
        if (!double.IsFinite(beta) || beta < 0)
            throw new ArgumentOutOfRangeException(nameof(beta), beta, "Beta must be finite and nonnegative.");
        var radius = SpinKernelArithmetic.ScaledNumber.From(aerodynamics.BallRadius);
        var coefficient = SpinKernelArithmetic.ScaledNumber.From(aerodynamics.AirDensity)
            .Times(SpinKernelArithmetic.ScaledNumber.From(Math.PI))
            .Times(radius).Times(radius).Times(radius).Times(radius)
            .Times(SpinKernelArithmetic.ScaledNumber.From(beta))
            .DividedBy(SpinKernelArithmetic.ScaledNumber.From(rotationalInertia));
        return new(coefficient);
    }

    internal Vector3d Evaluate(Vector3d velocity, Vector3d angularVelocity)
    {
        if (prefactor.IsZero)
            return default;
        try
        {
            var rate = prefactor.Times(SpinKernelArithmetic.Norm(velocity));
            return new(Component(rate, angularVelocity.X, "X"), Component(rate, angularVelocity.Y, "Y"),
                Component(rate, angularVelocity.Z, "Z"));
        }
        catch (ArithmeticException exception)
        {
            throw new ArithmeticException("Model A R1-U evaluation failed: " + exception.Message, exception);
        }
    }

    private static double Component(SpinKernelArithmetic.ScaledNumber rate, double spin, string component) =>
        rate.Times(SpinKernelArithmetic.ScaledNumber.From(spin)).Negated().ToDouble($"R1-U angular derivative.{component}");
}
