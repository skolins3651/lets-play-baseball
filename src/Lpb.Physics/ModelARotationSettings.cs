namespace Lpb.Physics;

/// <summary>Immutable, explicitly selected R0 or R1-U inputs, without calibrated defaults.</summary>
public sealed class ModelARotationSettings
{
    private ModelARotationSettings(ModelARotationKind kind, double? rotationalInertia, double? beta)
    {
        Kind = kind;
        RotationalInertia = rotationalInertia;
        Beta = beta;
    }

    /// <summary>Creates the constant-spin control, with no inertia or beta input.</summary>
    public static ModelARotationSettings R0() => new(ModelARotationKind.R0, null, null);

    /// <summary>Creates explicit R1-U inputs, including when beta or air density is zero.</summary>
    /// <param name="rotationalInertia">Finite, positive scalar inertia in kg m².</param>
    /// <param name="beta">Finite, nonnegative dimensionless damping parameter.</param>
    public static ModelARotationSettings R1U(double rotationalInertia, double beta)
    {
        if (!double.IsFinite(rotationalInertia) || rotationalInertia <= 0)
            throw new ArgumentOutOfRangeException(nameof(rotationalInertia), rotationalInertia,
                "Rotational inertia must be finite and positive, in kg m².");
        if (!double.IsFinite(beta) || beta < 0)
            throw new ArgumentOutOfRangeException(nameof(beta), beta, "Beta must be finite and nonnegative.");
        return new(ModelARotationKind.R1U, rotationalInertia, beta);
    }

    /// <summary>Gets the selected rotational policy, including for zero-beta R1-U.</summary>
    public ModelARotationKind Kind { get; }

    /// <summary>Gets explicit inertia in kg m² for R1-U, or null for R0.</summary>
    public double? RotationalInertia { get; }

    /// <summary>Gets the dimensionless R1-U beta, or null for R0.</summary>
    public double? Beta { get; }
}
