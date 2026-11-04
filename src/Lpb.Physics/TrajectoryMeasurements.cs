namespace Lpb.Physics;

/// <summary>Optional measurements requiring only immutable trajectory samples.</summary>
public static class TrajectoryMeasurements
{
    /// <summary>Gets final minus initial ball-center position, in metres.</summary>
    public static Vector3d GetDisplacement(SimulationResult result) => Displacement(result, horizontalOnly: false);

    /// <summary>Gets the displacement vector in metres with Z zero; this is not distance or path length.</summary>
    public static Vector3d GetHorizontalDisplacement(SimulationResult result) => Displacement(result, horizontalOnly: true);

    /// <summary>Gets first-contact time and ball-center state, or null when duration was reached without contact.</summary>
    public static TrajectorySample? GetGroundContact(SimulationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.CompletionReason == CompletionReason.GroundContact ? result.Samples[^1] : null;
    }

    private static Vector3d Displacement(SimulationResult result, bool horizontalOnly)
    {
        ArgumentNullException.ThrowIfNull(result);
        var initial = result.Samples[0].State.Position;
        var final = result.FinalState.Position;
        var x = final.X - initial.X;
        var y = final.Y - initial.Y;
        var z = horizontalOnly ? 0 : final.Z - initial.Z;
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            throw new ArithmeticException("Trajectory displacement cannot be represented as finite components.");
        return new Vector3d(x, y, z);
    }
}
