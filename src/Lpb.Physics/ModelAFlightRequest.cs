namespace Lpb.Physics;

/// <summary>Immutable explicit inputs for unconstrained Model A flight starting at time zero.</summary>
/// <remarks>Construction validates inputs; it does not execute flight or numerical acceptance checks.</remarks>
public sealed class ModelAFlightRequest
{
    /// <summary>Creates a request using the established gravity and time-input conventions.</summary>
    /// <param name="initialState">The complete release state in SI units.</param>
    /// <param name="gravityMagnitude">Finite, nonnegative gravity in m/s², acting along -Z.</param>
    /// <param name="duration">Finite, strictly positive requested duration in seconds.</param>
    /// <param name="timeStep">Finite, strictly positive timestep in seconds; it may exceed duration.</param>
    public ModelAFlightRequest(SpinFlightState initialState, double gravityMagnitude, double duration, double timeStep)
    {
        TranslationRequest = new FlightRequest(initialState.Translation, gravityMagnitude, duration, timeStep);
        InitialState = initialState;
    }

    /// <summary>Gets the complete state at elapsed simulation time zero.</summary>
    public SpinFlightState InitialState { get; }

    /// <summary>Gets constant gravity magnitude in m/s², acting along -Z.</summary>
    public double GravityMagnitude => TranslationRequest.GravityMagnitude;

    /// <summary>Gets requested elapsed duration in seconds.</summary>
    public double Duration => TranslationRequest.Duration;

    /// <summary>Gets the nominal timestep in seconds.</summary>
    public double TimeStep => TranslationRequest.TimeStep;

    internal FlightRequest TranslationRequest { get; }
}
