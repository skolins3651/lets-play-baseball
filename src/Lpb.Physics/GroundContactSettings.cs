namespace Lpb.Physics;

/// <summary>Immutable flat-ground and spherical-ball geometry, in metres.</summary>
public sealed class GroundContactSettings
{
    /// <summary>Creates geometry whose center-contact height is representably above the ground.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An individual input is invalid.</exception>
    /// <exception cref="ArgumentException">The sum overflows or loses the radius's effect.</exception>
    public GroundContactSettings(double groundElevation, double ballRadius)
    {
        if (!double.IsFinite(groundElevation))
            throw new ArgumentOutOfRangeException(nameof(groundElevation), groundElevation, "Ground elevation must be finite.");
        if (!double.IsFinite(ballRadius) || ballRadius <= 0)
            throw new ArgumentOutOfRangeException(nameof(ballRadius), ballRadius, "Ball radius must be finite and positive.");

        var height = groundElevation + ballRadius;
        if (!double.IsFinite(height) || height <= groundElevation)
            throw new ArgumentException("Ground elevation plus ball radius must be finite and representably above ground elevation.", nameof(ballRadius));

        GroundElevation = groundElevation;
        BallRadius = ballRadius;
        ContactHeight = height;
    }

    /// <summary>Gets the flat ground's elevation in metres.</summary>
    public double GroundElevation { get; }

    /// <summary>Gets the strictly positive ball radius in metres.</summary>
    public double BallRadius { get; }

    internal double ContactHeight { get; }
}
