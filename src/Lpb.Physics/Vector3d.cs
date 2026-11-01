namespace Lpb.Physics;

/// <summary>
/// An immutable, unit-agnostic vector with three finite double-precision components.
/// </summary>
/// <remarks>
/// Physical meaning and units are defined by the context that uses the vector.
/// The default value is the valid zero vector.
/// </remarks>
public readonly record struct Vector3d
{
    /// <summary>Creates a vector from three finite components.</summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    /// <param name="z">The Z component.</param>
    /// <exception cref="ArgumentOutOfRangeException">A component is not finite.</exception>
    public Vector3d(double x, double y, double z)
    {
        if (!double.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "The X component must be finite.");
        }

        if (!double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, "The Y component must be finite.");
        }

        if (!double.IsFinite(z))
        {
            throw new ArgumentOutOfRangeException(nameof(z), z, "The Z component must be finite.");
        }

        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Gets the X component.</summary>
    public double X { get; }

    /// <summary>Gets the Y component.</summary>
    public double Y { get; }

    /// <summary>Gets the Z component.</summary>
    public double Z { get; }
}
