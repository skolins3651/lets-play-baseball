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
        return SimulateCore(request, contact);
    }

    private static SimulationResult SimulateCore(FlightRequest request, GroundContactSettings? contact)
    {
        var dynamics = new GravityDynamics(request.GravityMagnitude);
        Func<double, FlightState, FlightDerivative> evaluate = dynamics.Evaluate;
        Func<TrajectorySample, double, FlightState> advance = (anchor, time) =>
            Rk4Integrator.Step(anchor.Time, time, anchor.State, evaluate);
        TrajectorySample? FindContact(TrajectorySample current, TrajectorySample next) =>
            TryFindContact(current, next, contact!, request.GravityMagnitude, request.InitialState, advance);
        return FlightExecution.Run(new FlightProvenance(request, contact), advance, contact is null ? null : FindContact);
    }

    internal static TrajectorySample? TryFindContact(TrajectorySample current, TrajectorySample next,
        GroundContactSettings contact, double gravity, FlightState release,
        Func<TrajectorySample, double, FlightState> advance)
    {
        if (next.State.Position.Z > contact.ContactHeight ||
            (next.State.Position.Z == contact.ContactHeight && next.State.Velocity.Z > 0))
            return null;
        return RefineContact(current, next, contact, gravity, release, advance);
    }

    // Keep the fixed-anchor closure allocation on the actual event path only.
    private static TrajectorySample RefineContact(TrajectorySample current, TrajectorySample next,
        GroundContactSettings contact, double gravity, FlightState release,
        Func<TrajectorySample, double, FlightState> advance)
    {
        var terminal = GravityOnlyRefinement.FindContact(current, next, contact.ContactHeight, gravity, time => advance(current, time));
        GravityContactEvidence.Validate(current, release, terminal.Time, contact.ContactHeight, gravity);
        return terminal;
    }
}
