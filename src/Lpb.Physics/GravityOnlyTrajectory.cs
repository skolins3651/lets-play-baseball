namespace Lpb.Physics;

/// <summary>Optional numerical analysis requiring explicit, matching gravity-only simulation inputs.</summary>
public static class GravityOnlyTrajectory
{
    /// <summary>Evaluates within the actual simulated interval; recorded timestamps return stored states verbatim.</summary>
    /// <exception cref="ArgumentException">The result lacks provenance or the request does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Time is nonfinite or outside the actual interval.</exception>
    /// <exception cref="ArithmeticException">Required RK4 arithmetic is unsafe.</exception>
    public static FlightState Evaluate(SimulationResult result, FlightRequest request, double time)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.ValidateGravityContext(request);
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
        return Rk4Integrator.Step(anchor.Time, time, anchor.State, new GravityDynamics(request.GravityMagnitude).Evaluate);
    }

    /// <summary>Gets maximum ball-center height and its earliest time over the actual simulated interval.</summary>
    /// <exception cref="ArgumentException">The result lacks provenance or the request does not match.</exception>
    /// <exception cref="ArithmeticException">A required numerical extremum cannot be resolved safely.</exception>
    public static TrajectorySample GetMaximumHeight(SimulationResult result, FlightRequest request)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.ValidateGravityContext(request);
        var first = result.Samples[0];
        var last = result.Samples[^1];
        if (result.Samples.Count == 1 || first.State.Velocity.Z <= 0)
            return first;
        if (last.State.Velocity.Z >= 0)
            return PreferEarlierRecordedMaximum(result, last);
        // This gravity model permits at most one positive-to-nonpositive velocity crossing.
        var lower = 0;
        var upper = result.Samples.Count - 1;
        while (upper - lower > 1)
        {
            var middle = lower + (upper - lower) / 2;
            if (result.Samples[middle].State.Velocity.Z > 0)
                lower = middle;
            else
                upper = middle;
        }
        var anchor = result.Samples[lower];
        var dynamics = new GravityDynamics(request.GravityMagnitude);
        var apex = GravityOnlyRefinement.FindApex(anchor, result.Samples[upper], request.GravityMagnitude,
            time => Rk4Integrator.Step(anchor.Time, time, anchor.State, dynamics.Evaluate));
        var maximum = first;
        if (apex.State.Position.Z > maximum.State.Position.Z)
            maximum = apex;
        if (last.State.Position.Z > maximum.State.Position.Z)
            maximum = last;
        return PreferEarlierRecordedMaximum(result, maximum);
    }

    private static TrajectorySample PreferEarlierRecordedMaximum(SimulationResult result, TrajectorySample candidate)
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
