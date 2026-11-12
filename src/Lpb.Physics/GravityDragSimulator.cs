namespace Lpb.Physics;

/// <summary>Runs fixed-grid gravity-plus-quadratic-drag flight in stationary air.</summary>
/// <remarks>Required numerical failures throw without a successful partial trajectory.</remarks>
public static class GravityDragSimulator
{
    /// <summary>Advances unconstrained motion to the requested duration, including below-ground motion.</summary>
    public static SimulationResult Simulate(FlightRequest request, AerodynamicSettings aerodynamics)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(aerodynamics);
        var source = new FlightProvenance(request, null, aerodynamics);
        return FlightExecution.Run(source, CreateAdvance(request, aerodynamics), null);
    }

    /// <summary>Advances to first ground contact or duration, using the aerodynamic radius.</summary>
    /// <remarks>Immediate contact bypasses integration. Nonzero-drag numerical checks may reject finite inputs.</remarks>
    public static SimulationResult SimulateUntilGroundContact(
        FlightRequest request, AerodynamicSettings aerodynamics, double groundElevation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(aerodynamics);
        if (!double.IsFinite(groundElevation))
            throw new ArgumentOutOfRangeException(nameof(groundElevation), groundElevation, "Ground elevation must be finite.");
        GroundContactSettings contact;
        try { contact = new GroundContactSettings(groundElevation, aerodynamics.BallRadius); }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Ground elevation plus aerodynamic radius must be finite and representably above ground.",
                nameof(groundElevation), exception);
        }
        var source = new FlightProvenance(request, contact, aerodynamics);
        if (aerodynamics.Disabled)
        {
            var advance = CreateAdvance(request, aerodynamics);
            TrajectorySample? Locate(TrajectorySample current, TrajectorySample next) =>
                GravityOnlySimulator.TryFindContact(current, next, contact, request.GravityMagnitude, request.InitialState, advance);
            return FlightExecution.Run(source, advance, Locate);
        }
        var numerical = new DragNumericalEvaluation(request, aerodynamics);
        var diagnostic = default(FlightState);
        FlightState Advance(TrajectorySample anchor, double time)
        {
            DragNumericalEvaluation.CheckContactHeight(contact.ContactHeight);
            var step = numerical.EvaluateStep(anchor, time);
            diagnostic = step.Diagnostic;
            return step.Full;
        }
        TrajectorySample? FindContact(TrajectorySample current, TrajectorySample next)
        {
            GravityDragRefinement.CheckContactGeometry(next.Time, next.State, diagnostic, contact.ContactHeight);
            return IsCandidate(next, contact.ContactHeight)
                ? GravityDragRefinement.FindContact(current, next, diagnostic, contact.ContactHeight, numerical) : null;
        }
        return FlightExecution.Run(source, Advance, FindContact);
    }

    private static bool IsCandidate(TrajectorySample sample, double height) =>
        sample.State.Position.Z < height || (sample.State.Position.Z == height && sample.State.Velocity.Z <= 0);

    internal static Func<TrajectorySample, double, FlightState> CreateAdvance(
        FlightRequest request, AerodynamicSettings aerodynamics)
    {
        if (!aerodynamics.Disabled)
            return new DragNumericalEvaluation(request, aerodynamics).Step;
        var gravity = new GravityDynamics(request.GravityMagnitude);
        Func<double, FlightState, FlightDerivative> evaluate = gravity.Evaluate;
        return (anchor, time) => Rk4Integrator.Step(anchor.Time, time, anchor.State, evaluate);
    }
}
