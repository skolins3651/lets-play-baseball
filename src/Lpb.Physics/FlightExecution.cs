namespace Lpb.Physics;

// Shared indexed execution and ownership. Physical event evidence belongs to the caller.
internal static class FlightExecution
{
    internal static SimulationResult Run(FlightProvenance source,
        Func<TrajectorySample, double, FlightState> advance,
        Func<TrajectorySample, TrajectorySample, TrajectorySample?>? findContact)
    {
        var request = source.Request;
        var contact = source.Contact;
        if (contact is not null)
        {
            if (request.InitialState.Position.Z < contact.ContactHeight)
                throw new ArgumentException("Initial penetration below the ball-center contact plane is unsupported.", nameof(request));
            if (request.InitialState.Position.Z == contact.ContactHeight && request.InitialState.Velocity.Z <= 0)
                return new SimulationResult([new TrajectorySample(0, request.InitialState)], source, CompletionReason.GroundContact);
        }
        var grid = new SimulationTimeGrid(request, requireWholeHorizon: contact is null);
        var samples = new TrajectorySample[contact is null ? grid.StepCount + 1 : Math.Min(256, grid.StepCount + 1)];
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
                next = new TrajectorySample(nextTime, advance(current, nextTime));
                var terminal = findContact?.Invoke(current, next);
                if (terminal is not null)
                {
                    next = terminal.Value;
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
                return new SimulationResult(samples, source, reason);
            }
            current = next;
        }

        throw new InvalidOperationException($"The request exhausted the {SimulationTimeGrid.MaximumIntegrationSteps} integration-step limit " +
            $"before contact or duration {request.Duration:R} s (timestep {request.TimeStep:R} s).");
    }
}
