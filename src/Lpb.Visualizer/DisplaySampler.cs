using Lpb.Physics;

namespace Lpb.Visualizer;

internal sealed record PlotPoint(double TimeSeconds, double XMetres, double YMetres, double ZMetres)
{
    public static PlotPoint From(TrajectorySample sample) => new(sample.Time,
        sample.State.Position.X, sample.State.Position.Y, sample.State.Position.Z);
}

internal sealed record DisplayPath(PlotPoint[] Points, string Source, string[] Warnings);

internal static class DisplaySampler
{
    internal const int MaximumPointCount = 2000;
    internal const string SmoothnessNote = "Additional display points can make the curve appear smoother, but do not guarantee greater numerical or physical accuracy.";

    public static void ValidateCount(int count)
    {
        if (count is < 2 or > MaximumPointCount)
            throw new InputException(new() { ["displayPointCount"] = "Display count must be an integer from 2 to 2,000." });
    }

    public static DisplayPath Sample(SimulationResult result, FlightRequest request, int count, AerodynamicSettings? aerodynamics = null)
    {
        ValidateCount(count);
        var warnings = new List<string>();
        if (count > result.Samples.Count) warnings.Add(SmoothnessNote);
        if (result.FinalTime == 0)
            return new([PlotPoint.From(result.Samples[0])], "Recorded samples", warnings.ToArray());
        try
        {
            var points = new List<PlotPoint>(count) { PlotPoint.From(result.Samples[0]) };
            foreach (var time in Times(result.FinalTime, count).Skip(1).SkipLast(1))
            {
                var state = result.ModelKind switch
                {
                    FlightModelKind.GravityOnly when aerodynamics is null => GravityOnlyTrajectory.Evaluate(result, request, time),
                    FlightModelKind.GravityAndDrag when aerodynamics is not null => GravityDragTrajectory.Evaluate(result, request, aerodynamics, time),
                    _ => throw new ArgumentException("Result and display model context disagree.", nameof(result))
                };
                points.Add(PlotPoint.From(new TrajectorySample(time, state)));
            }
            points.Add(PlotPoint.From(result.Samples[^1]));
            if (points.Count != count)
                warnings.Add("Some display timestamps could not be represented distinctly; fewer points are shown.");
            return new(points.ToArray(), "Numerical evaluation", warnings.ToArray());
        }
        catch (ArithmeticException exception)
        {
            warnings.Add($"Display evaluation unavailable; showing a bounded subset of recorded samples. {exception.Message}");
            var size = Math.Min(count, result.Samples.Count);
            var points = new PlotPoint[size];
            for (var i = 0; i < size; i++)
                points[i] = PlotPoint.From(result.Samples[(int)((long)i * (result.Samples.Count - 1) / (size - 1))]);
            return new(points, "Recorded-sample fallback", warnings.ToArray());
        }
    }

    internal static IReadOnlyList<double> Times(double finalTime, int count)
    {
        ValidateCount(count);
        var times = new List<double>(count) { 0 };
        if (finalTime == 0) return times;
        for (var i = 1; i < count - 1; i++)
        {
            var time = finalTime * ((double)i / (count - 1));
            if (double.IsFinite(time) && time > times[^1] && time < finalTime) times.Add(time);
        }
        times.Add(finalTime);
        return times;
    }
}
