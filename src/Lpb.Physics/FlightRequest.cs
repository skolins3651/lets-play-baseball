namespace Lpb.Physics;

/// <summary>
/// Immutable inputs for a gravity-only trajectory starting at elapsed simulation time zero.
/// </summary>
/// <remarks>
/// This input contract does not execute a simulation. Ground contact is disabled;
/// no mass, radius, spin, or air properties are required.
/// </remarks>
public sealed class FlightRequest
{
    /// <summary>Creates a request with explicit gravity and numerical settings.</summary>
    /// <param name="initialState">The state at elapsed simulation time zero.</param>
    /// <param name="gravityMagnitude">The finite, nonnegative gravity magnitude in metres per second squared.</param>
    /// <param name="duration">The finite, strictly positive requested duration in seconds.</param>
    /// <param name="timeStep">The finite, strictly positive integration timestep in seconds; it may exceed duration.</param>
    /// <exception cref="ArgumentOutOfRangeException">A scalar input is outside its documented range.</exception>
    public FlightRequest(FlightState initialState, double gravityMagnitude, double duration, double timeStep)
    {
        if (!double.IsFinite(gravityMagnitude) || gravityMagnitude < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gravityMagnitude), gravityMagnitude,
                "Gravity magnitude must be finite and nonnegative, in metres per second squared.");
        }

        if (!double.IsFinite(duration) || duration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration,
                "Duration must be finite and greater than zero seconds.");
        }

        if (!double.IsFinite(timeStep) || timeStep <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeStep), timeStep,
                "Timestep must be finite and greater than zero seconds.");
        }

        InitialState = initialState;
        GravityMagnitude = gravityMagnitude;
        Duration = duration;
        TimeStep = timeStep;
    }

    /// <summary>Gets the state at elapsed simulation time zero.</summary>
    public FlightState InitialState { get; }

    /// <summary>
    /// Gets the constant gravity magnitude in metres per second squared, acting along -Z.
    /// Zero is valid; no Earth-gravity default is assumed.
    /// </summary>
    public double GravityMagnitude { get; }

    /// <summary>Gets the requested elapsed simulation duration in seconds.</summary>
    public double Duration { get; }

    /// <summary>Gets the integration timestep in seconds, which may exceed the duration.</summary>
    public double TimeStep { get; }
}
