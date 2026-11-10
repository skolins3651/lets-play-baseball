namespace Lpb.Physics;

internal static class TrajectoryEvaluation
{
    // Model/provenance validation precedes this call, including exact timestamp returns.
    internal static FlightState Evaluate(SimulationResult result, double time,
        Func<TrajectorySample, double, FlightState> advance)
    {
        if (!double.IsFinite(time) || time < 0 || time > result.FinalTime)
            throw new ArgumentOutOfRangeException(nameof(time), time, "Time must lie in the actual simulated interval.");
        var lower = 0;
        var upper = result.Samples.Count - 1;
        while (lower <= upper)
        {
            var middle = lower + (upper - lower) / 2;
            var sample = result.Samples[middle];
            if (sample.Time == time)
                return sample.State;
            if (sample.Time < time)
                lower = middle + 1;
            else
                upper = middle - 1;
        }
        var anchor = result.Samples[upper];
        return advance(anchor, time);
    }

    internal static TrajectorySample PreferEarlierRecordedMaximum(SimulationResult result, TrajectorySample candidate)
    {
        foreach (var sample in result.Samples)
        {
            if (sample.State.Position.Z > candidate.State.Position.Z ||
                (sample.State.Position.Z == candidate.State.Position.Z && sample.Time < candidate.Time))
                candidate = sample;
        }
        return candidate;
    }
}
