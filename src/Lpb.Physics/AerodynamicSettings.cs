namespace Lpb.Physics;

/// <summary>Explicit immutable spherical-ball and stationary-air assumptions, in SI units.</summary>
public sealed class AerodynamicSettings
{
    /// <summary>Creates constant flight parameters; there are no calibrated or implicit defaults.</summary>
    public AerodynamicSettings(double ballMass, double ballRadius, double airDensity, double dragCoefficient)
    {
        if (!double.IsFinite(ballMass) || ballMass <= 0)
            throw new ArgumentOutOfRangeException(nameof(ballMass), ballMass, "Ball mass must be finite and positive, in kg.");
        if (!double.IsFinite(ballRadius) || ballRadius <= 0)
            throw new ArgumentOutOfRangeException(nameof(ballRadius), ballRadius, "Ball radius must be finite and positive, in m.");
        if (!double.IsFinite(airDensity) || airDensity < 0)
            throw new ArgumentOutOfRangeException(nameof(airDensity), airDensity, "Air density must be finite and nonnegative, in kg/m³.");
        if (!double.IsFinite(dragCoefficient) || dragCoefficient < 0)
            throw new ArgumentOutOfRangeException(nameof(dragCoefficient), dragCoefficient, "Drag coefficient must be finite and nonnegative.");
        BallMass = ballMass;
        BallRadius = ballRadius;
        AirDensity = airDensity;
        DragCoefficient = dragCoefficient;
    }

    /// <summary>Gets ball mass in kg.</summary>
    public double BallMass { get; }
    /// <summary>Gets the authoritative ball radius in m, including for ground contact.</summary>
    public double BallRadius { get; }
    /// <summary>Gets constant air density in kg/m³.</summary>
    public double AirDensity { get; }
    /// <summary>Gets the constant dimensionless coefficient referenced to frontal area πR².</summary>
    public double DragCoefficient { get; }

    internal bool Disabled => AirDensity == 0 || DragCoefficient == 0;
}
