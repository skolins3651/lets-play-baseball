namespace Lpb.Physics;

/// <summary>Identifies the closed rotational choices supported by Model A inputs.</summary>
public enum ModelARotationKind
{
    /// <summary>Constant world-frame angular-velocity vector.</summary>
    R0,

    /// <summary>Fixed-axis, instantaneous-airspeed-dependent linear angular damping.</summary>
    R1U
}
