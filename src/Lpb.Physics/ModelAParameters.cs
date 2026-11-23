namespace Lpb.Physics;

/// <summary>Immutable physical inputs for the named Nathan ASR-B Model A prescription.</summary>
/// <remarks>Scientific coefficients remain provisional. No inertia or beta is inferred.</remarks>
public sealed class ModelAParameters
{
    private ModelAParameters(AerodynamicSettings aerodynamics, ModelARotationSettings rotation)
    {
        ArgumentNullException.ThrowIfNull(aerodynamics);
        ArgumentNullException.ThrowIfNull(rotation);
        Aerodynamics = aerodynamics;
        Rotation = rotation;
        PrescriptionIdentity = "model-a/nathan-asr-b-v1";
    }

    /// <summary>Creates Nathan ASR-B inputs with explicit stationary-air and rotational settings.</summary>
    public static ModelAParameters NathanAsrB(AerodynamicSettings aerodynamics, ModelARotationSettings rotation) =>
        new(aerodynamics, rotation);

    /// <summary>Gets the original immutable ball, density, and constant-drag inputs.</summary>
    public AerodynamicSettings Aerodynamics { get; }

    /// <summary>Gets the original immutable rotational selection and applicable inputs.</summary>
    public ModelARotationSettings Rotation { get; }

    internal string PrescriptionIdentity { get; }
}
