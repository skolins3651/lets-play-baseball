namespace Lpb.Physics;

internal readonly struct SimulationTimeGrid
{
    internal const int MaximumIntegrationSteps = 100_000;

    private readonly double duration;
    private readonly double timeStep;

    internal SimulationTimeGrid(FlightRequest request) : this(request, requireWholeHorizon: true)
    {
    }

    internal SimulationTimeGrid(FlightRequest request, bool requireWholeHorizon)
    {
        ArgumentNullException.ThrowIfNull(request);
        duration = request.Duration;
        timeStep = request.TimeStep;

        // Positive infinity is allowed only as a preflight upper-bound sentinel.
        if (Boundary(MaximumIntegrationSteps, timeStep) < duration)
        {
            if (!requireWholeHorizon)
            {
                StepCount = MaximumIntegrationSteps;
                return;
            }

            throw new InvalidOperationException(
                $"The request exceeds the {MaximumIntegrationSteps} integration-step limit " +
                $"(duration {duration:R} s, timestep {timeStep:R} s).");
        }

        var lower = 1;
        var upper = MaximumIntegrationSteps;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            if (Boundary(middle, timeStep) >= duration)
            {
                upper = middle;
            }
            else
            {
                lower = middle + 1;
            }
        }

        StepCount = lower;
    }

    internal int StepCount { get; }

    internal double GetNextTime(int stepIndex, double currentTime)
    {
        if (stepIndex < 1 || stepIndex > StepCount)
        {
            throw new ArgumentOutOfRangeException(nameof(stepIndex));
        }

        // Exhausting the execution budget does not mean the requested horizon was reached.
        var boundary = Boundary(stepIndex, timeStep);
        var nextTime = boundary >= duration ? duration : boundary;
        var interval = nextTime - currentTime;
        if (!double.IsFinite(currentTime) || currentTime < 0 ||
            !double.IsFinite(nextTime) || nextTime <= currentTime || nextTime > duration ||
            !double.IsFinite(interval) || interval <= 0)
        {
            throw new ArithmeticException(
                $"Step {stepIndex} cannot advance safely from {currentTime:R} to {nextTime:R} s " +
                $"within duration {duration:R} s.");
        }

        return nextTime;
    }

    private static double Boundary(int index, double step) => (double)index * step;
}
