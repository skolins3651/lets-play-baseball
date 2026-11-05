using Lpb.Physics;

namespace Lpb.Visualizer;

internal sealed class SimulationException(Exception inner) : Exception(inner.Message, inner);

internal sealed record Measurements(Vector3d? DisplacementMetres, Vector3d? HorizontalDisplacementMetres,
    double? HorizontalDistanceMetres, double? MaximumHeightAboveGroundMetres);
internal sealed record Landmarks(PlotPoint Launch, PlotPoint? Maximum, PlotPoint? Contact, PlotPoint Terminal);
internal sealed record ExperimentResponse(Guid ResultId, ExecutedScenario ExecutedScenario, string CompletionReason,
    double RequestedDurationSeconds, double ActualElapsedSeconds, int OriginalSampleCount,
    int RequestedDisplayPointCount, int ActualDisplayPointCount, string SamplingSource,
    PlotPoint[] Points, Landmarks Landmarks, Measurements Measurements, string[] Warnings);

internal sealed class Experiment
{
    public required FlightRequest Request { get; init; }
    public required SimulationResult Result { get; init; }
    public required ExecutedScenario Scenario { get; init; }
    public required Measurements Measurements { get; init; }
    public required Landmarks Landmarks { get; init; }
    public required string[] Warnings { get; init; }

    public static Experiment Run(AdaptedLaunch launch)
    {
        SimulationResult result;
        try
        {
            result = launch.Scenario.Launch.Mode == "contact"
                ? GravityOnlySimulator.SimulateUntilGroundContact(launch.Request, launch.Contact)
                : GravityOnlySimulator.Simulate(launch.Request);
        }
        catch (ArgumentException exception)
        {
            throw new InputException(new() { ["centerHeightFeet"] = exception.Message });
        }
        catch (Exception exception) when (exception is ArithmeticException or InvalidOperationException)
        {
            throw new SimulationException(exception);
        }
        return Analyze(launch.Request, result, launch.Scenario);
    }

    internal static Experiment Analyze(FlightRequest request, SimulationResult result, ExecutedScenario scenario)
    {
        var warnings = new List<string>();
        TrajectorySample? maximum = null;
        Vector3d? displacement = null;
        Vector3d? horizontal = null;
        double? distance = null;
        double? height = null;
        try { maximum = GravityOnlyTrajectory.GetMaximumHeight(result, request); }
        catch (ArithmeticException exception) { warnings.Add($"Maximum over simulated interval unavailable: {exception.Message}"); }
        if (maximum is not null)
        {
            var relative = maximum.Value.State.Position.Z - scenario.GroundElevationMetres;
            if (double.IsFinite(relative)) height = relative;
            else warnings.Add("Maximum height relative to ground cannot be represented.");
        }
        try { displacement = TrajectoryMeasurements.GetDisplacement(result); }
        catch (ArithmeticException exception) { warnings.Add($"Displacement unavailable: {exception.Message}"); }
        try
        {
            horizontal = TrajectoryMeasurements.GetHorizontalDisplacement(result);
            var x = Math.Abs(horizontal.Value.X);
            var y = Math.Abs(horizontal.Value.Y);
            var scale = Math.Max(x, y);
            var magnitude = scale == 0 ? 0 : scale * Math.Sqrt((x / scale) * (x / scale) + (y / scale) * (y / scale));
            if (double.IsFinite(magnitude)) distance = magnitude;
            else warnings.Add("Horizontal distance cannot be represented.");
        }
        catch (ArithmeticException exception) { warnings.Add($"Horizontal displacement unavailable: {exception.Message}"); }
        var contact = TrajectoryMeasurements.GetGroundContact(result);
        return new()
        {
            Request = request, Result = result, Scenario = scenario,
            Measurements = new(displacement, horizontal, distance, height),
            Landmarks = new(PlotPoint.From(result.Samples[0]), maximum is null ? null : PlotPoint.From(maximum.Value),
                contact is null ? null : PlotPoint.From(contact.Value), PlotPoint.From(result.Samples[^1])),
            Warnings = warnings.ToArray()
        };
    }

    public ExperimentResponse Display(Guid id, int count)
    {
        var path = DisplaySampler.Sample(Result, Request, count);
        return new(id, Scenario, Result.CompletionReason.ToString(), Request.Duration, Result.FinalTime,
            Result.Samples.Count, count, path.Points.Length, path.Source, path.Points, Landmarks, Measurements,
            Warnings.Concat(path.Warnings).ToArray());
    }
}
