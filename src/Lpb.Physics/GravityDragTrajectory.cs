namespace Lpb.Physics;

/// <summary>Optional numerical evaluation and maximum-height analysis with matching drag-model inputs.</summary>
public static class GravityDragTrajectory
{
    /// <summary>Returns recorded states verbatim or performs checked RK4 from the preceding sample.</summary>
    /// <remarks>Time must lie within the actual interval. No interior state is projected onto the ground.</remarks>
    public static FlightState Evaluate(SimulationResult result, FlightRequest request,
        AerodynamicSettings aerodynamics, double time)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.ValidateDragContext(request, aerodynamics);
        return TrajectoryEvaluation.Evaluate(result, time, GravityDragSimulator.CreateAdvance(request, aerodynamics));
    }

    /// <summary>Gets maximum center height over the actual interval, preferring earlier equal numerical candidates.</summary>
    /// <remarks>Optional numerical failure throws without changing the successful result.</remarks>
    public static TrajectorySample GetMaximumHeight(SimulationResult result, FlightRequest request,
        AerodynamicSettings aerodynamics)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.ValidateDragContext(request, aerodynamics);
        if (aerodynamics.Disabled)
            return GravityOnlyTrajectory.GetMaximumHeightCore(result, request);
        var first = result.Samples[0];
        var last = result.Samples[^1];
        if (result.Samples.Count == 1 || first.State.Velocity.Z <= 0)
            return first;
        if (request.GravityMagnitude == 0 || last.State.Velocity.Z >= 0)
            return TrajectoryEvaluation.PreferEarlierRecordedMaximum(result, last);
        // This model has one positive-to-nonpositive crossing; descending velocities
        // themselves need not be sorted or monotonically decreasing.
        var lower = 0;
        var upper = result.Samples.Count - 1;
        while (upper - lower > 1)
        {
            var middle = lower + (upper - lower) / 2;
            if (result.Samples[middle].State.Velocity.Z > 0) lower = middle;
            else upper = middle;
        }
        var anchor = result.Samples[lower];
        try
        {
            var apex = GravityDragRefinement.FindApex(anchor, anchor, result.Samples[upper],
                new DragNumericalEvaluation(request, aerodynamics));
            return TrajectoryEvaluation.PreferEarlierRecordedMaximum(result, apex);
        }
        catch (ArithmeticException exception)
        {
            throw new ArithmeticException($"Drag maximum-height interval {anchor.Time:R} to {result.Samples[upper].Time:R} s failed: {exception.Message}", exception);
        }
    }
}
