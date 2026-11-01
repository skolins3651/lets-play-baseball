namespace Lpb.Physics;

/// <summary>
/// The immutable ball-centre position and linear velocity for gravity-only free flight.
/// </summary>
/// <remarks>
/// Uses the field-fixed, right-handed frame: +X toward first base/right field,
/// +Y toward third base/left field, and +Z upward. The origin is the rear tip of
/// home plate at reference ground elevation. Negative components, including
/// below-ground positions, are valid while ground contact is disabled.
/// The default value is a valid state at the origin with zero velocity.
/// </remarks>
public readonly record struct FlightState
{
    /// <summary>Creates a state from finite position and velocity vectors.</summary>
    /// <param name="position">The ball-centre position in metres.</param>
    /// <param name="velocity">The linear velocity in metres per second.</param>
    public FlightState(Vector3d position, Vector3d velocity)
    {
        Position = position;
        Velocity = velocity;
    }

    /// <summary>Gets the ball-centre position in metres in the field-fixed frame.</summary>
    public Vector3d Position { get; }

    /// <summary>Gets the linear velocity in metres per second in the field-fixed frame.</summary>
    public Vector3d Velocity { get; }
}
