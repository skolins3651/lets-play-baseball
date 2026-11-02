namespace Lpb.Physics;

/// <summary>An immutable physical state at an elapsed simulation time in seconds.</summary>
/// <remarks>The default value is a valid sample at time zero with zero position and velocity.</remarks>
public readonly record struct TrajectorySample
{
    /// <summary>Creates a sample with finite, nonnegative elapsed time.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Time is nonfinite or negative.</exception>
    public TrajectorySample(double time, FlightState state)
    {
        if (!double.IsFinite(time) || time < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(time), time,
                "Sample time must be finite and nonnegative, in seconds.");
        }

        Time = time;
        State = state;
    }

    /// <summary>Gets elapsed simulation time in seconds.</summary>
    public double Time { get; }

    /// <summary>Gets the ball-centre position and linear velocity in SI units.</summary>
    public FlightState State { get; }
}
