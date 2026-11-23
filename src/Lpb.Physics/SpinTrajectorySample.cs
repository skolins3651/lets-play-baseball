namespace Lpb.Physics;

/// <summary>An immutable complete spin flight state at elapsed simulation time in seconds.</summary>
/// <remarks>The default is a valid zero state at time zero.</remarks>
public readonly record struct SpinTrajectorySample
{
    /// <summary>Creates a sample with finite, nonnegative elapsed time.</summary>
    public SpinTrajectorySample(double time, SpinFlightState state)
    {
        if (!double.IsFinite(time) || time < 0)
            throw new ArgumentOutOfRangeException(nameof(time), time,
                "Sample time must be finite and nonnegative, in seconds.");
        Time = time;
        State = state;
    }

    /// <summary>Gets elapsed simulation time in seconds.</summary>
    public double Time { get; }

    /// <summary>Gets all nine physical components in SI units.</summary>
    public SpinFlightState State { get; }
}
