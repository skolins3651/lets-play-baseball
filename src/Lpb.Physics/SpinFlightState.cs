namespace Lpb.Physics;

/// <summary>Immutable ball-centre translation and world-frame angular velocity.</summary>
/// <remarks>
/// Uses the existing right-handed field frame and SI units. The default is a valid
/// zero state. Spin magnitude, axis, and active-spin diagnostics are not retained.
/// </remarks>
public readonly record struct SpinFlightState
{
    /// <summary>Creates a complete state from finite translation and angular velocity.</summary>
    /// <param name="translation">Ball-centre position in m and linear velocity in m/s.</param>
    /// <param name="angularVelocity">World-frame angular velocity in rad/s, by the right-hand rule.</param>
    public SpinFlightState(FlightState translation, Vector3d angularVelocity)
    {
        Translation = translation;
        AngularVelocity = angularVelocity;
    }

    /// <summary>Gets ball-centre position and linear velocity in the field frame.</summary>
    public FlightState Translation { get; }

    /// <summary>Gets world-frame angular velocity in rad/s.</summary>
    public Vector3d AngularVelocity { get; }
}
