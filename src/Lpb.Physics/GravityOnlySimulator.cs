namespace Lpb.Physics;

/// <summary>Runs headless, fixed-step RK4 trajectories with constant gravity.</summary>
public static class GravityOnlySimulator
{
    /// <summary>Advances unconstrained motion to the exact requested duration, including below-ground motion.</summary>
    /// <remarks>Whole-horizon preflight permits at most 100,000 steps and 100,001 samples.</remarks>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="InvalidOperationException">The request exceeds the integration-step limit.</exception>
    /// <exception cref="ArithmeticException">Required numerical calculations are unsafe.</exception>
    public static SimulationResult Simulate(FlightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SimulateCore(request, null);
    }

    /// <summary>Advances to first ground contact or the exact requested duration, whichever occurs first.</summary>
    /// <remarks>Caps actual ordinary integration work; root refinement has separate bounded budgets.</remarks>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentException">The initial center penetrates the contact plane.</exception>
    /// <exception cref="InvalidOperationException">The ordinary integration budget is exhausted.</exception>
    /// <exception cref="ArithmeticException">Integration or event localization cannot execute safely.</exception>
    public static SimulationResult SimulateUntilGroundContact(FlightRequest request, GroundContactSettings contact)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(contact);
        if (request.InitialState.Position.Z < contact.ContactHeight)
            throw new ArgumentException("Initial penetration below the ball-center contact plane is unsupported.", nameof(request));
        if (request.InitialState.Position.Z == contact.ContactHeight && request.InitialState.Velocity.Z <= 0)
            return new SimulationResult([new TrajectorySample(0, request.InitialState)], request, CompletionReason.GroundContact, contact);
        return SimulateCore(request, contact);
    }

    private static SimulationResult SimulateCore(FlightRequest request, GroundContactSettings? contact)
    {
        var grid = new SimulationTimeGrid(request, requireWholeHorizon: contact is null);
        var samples = new TrajectorySample[contact is null ? grid.StepCount + 1 : Math.Min(256, grid.StepCount + 1)];
        var dynamics = new GravityDynamics(request.GravityMagnitude);
        Func<double, FlightState, FlightDerivative> evaluate = dynamics.Evaluate;
        var current = new TrajectorySample(0, request.InitialState);
        samples[0] = current;
        var count = 1;

        for (var step = 1; step <= grid.StepCount; step++)
        {
            var nextTime = grid.GetNextTime(step, current.Time);
            TrajectorySample next;
            var reason = CompletionReason.DurationReached;
            try
            {
                next = new TrajectorySample(nextTime, Rk4Integrator.Step(current.Time, nextTime, current.State, evaluate));
                if (contact is not null && next.State.Position.Z <= contact.ContactHeight &&
                    (next.State.Position.Z < contact.ContactHeight || next.State.Velocity.Z <= 0))
                {
                    var anchor = current;
                    next = GravityOnlyRefinement.FindContact(current, next, contact.ContactHeight, request.GravityMagnitude,
                        trialTime => Rk4Integrator.Step(anchor.Time, trialTime, anchor.State, evaluate));
                    reason = CompletionReason.GroundContact;
                }
            }
            catch (ArithmeticException exception)
            {
                throw new ArithmeticException(
                    $"Integration step {step} from {current.Time:R} to {nextTime:R} s failed: {exception.Message}", exception);
            }

            if (count == samples.Length)
                Array.Resize(ref samples, Math.Min(samples.Length * 2, SimulationTimeGrid.MaximumIntegrationSteps + 1));
            samples[count++] = next;
            if (reason == CompletionReason.GroundContact || next.Time == request.Duration)
            {
                if (samples.Length != count)
                    Array.Resize(ref samples, count);
                return new SimulationResult(samples, request, reason, contact);
            }
            current = next;
        }

        throw new InvalidOperationException($"The request exhausted the {SimulationTimeGrid.MaximumIntegrationSteps} integration-step limit " +
            $"before contact or duration {request.Duration:R} s (timestep {request.TimeStep:R} s).");
    }
}
