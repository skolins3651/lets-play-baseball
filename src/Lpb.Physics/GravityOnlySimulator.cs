namespace Lpb.Physics;

/// <summary>Runs headless, fixed-step RK4 trajectories with constant gravity and no ground contact.</summary>
public static class GravityOnlySimulator
{
    /// <summary>Advances the initial state from time zero to the exact requested duration.</summary>
    /// <remarks>
    /// Records every integration step, with at most 100,000 steps and 100,001 samples.
    /// Unsafe execution throws rather than returning a partial trajectory.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="InvalidOperationException">The request exceeds the integration-step limit.</exception>
    /// <exception cref="ArithmeticException">Time progression or RK4 arithmetic cannot be represented safely.</exception>
    public static SimulationResult Simulate(FlightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var grid = new SimulationTimeGrid(request);
        var samples = new TrajectorySample[grid.StepCount + 1];
        var dynamics = new GravityDynamics(request.GravityMagnitude);
        Func<double, FlightState, FlightDerivative> evaluate = dynamics.Evaluate;
        var state = request.InitialState;
        var time = 0d;
        samples[0] = new TrajectorySample(time, state);

        for (var step = 1; step <= grid.StepCount; step++)
        {
            var nextTime = grid.GetNextTime(step, time);
            try
            {
                state = Rk4Integrator.Step(time, nextTime, state, evaluate);
            }
            catch (ArithmeticException exception)
            {
                throw new ArithmeticException(
                    $"Integration step {step} from {time:R} to {nextTime:R} s failed: {exception.Message}", exception);
            }

            time = nextTime;
            samples[step] = new TrajectorySample(time, state);
        }

        return new SimulationResult(samples, request.Duration);
    }
}
