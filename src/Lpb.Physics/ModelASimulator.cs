namespace Lpb.Physics;

/// <summary>Runs duration-only Model A flight under the fixed B2-N numerical policy.</summary>
/// <remarks>Acceptance is not a certified accumulated-error or empirical-calibration guarantee.</remarks>
public static class ModelASimulator
{
    /// <summary>Returns accepted full-step samples, including the exact requested terminal time.</summary>
    /// <remarks>Numerical or resource failures throw without a successful partial result. No events are evaluated.</remarks>
    public static ModelASimulationResult Simulate(ModelAFlightRequest request, ModelAParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(parameters);
        var grid = new SimulationTimeGrid(request.TranslationRequest);
        var numerical = new ModelANumericalEvaluation(request, parameters);
        var samples = new SpinTrajectorySample[grid.StepCount + 1];
        var current = new SpinTrajectorySample(0, request.InitialState);
        samples[0] = current;
        for (var step = 1; step <= grid.StepCount; step++)
        {
            var nextTime = grid.GetNextTime(step, current.Time);
            current = new(nextTime, numerical.Step(step, current, nextTime));
            samples[step] = current;
        }
        var source = ModelAFlightProvenance.AfterCheckedDuration(request, parameters);
        return new(samples, source, CompletionReason.DurationReached);
    }
}
