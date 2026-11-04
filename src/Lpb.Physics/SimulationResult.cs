using System.Collections;
using System.Collections.ObjectModel;

namespace Lpb.Physics;

/// <summary>An immutable snapshot of a successfully completed gravity-only trajectory.</summary>
/// <remarks>Execution failures throw exceptions; they do not return partial successful results.</remarks>
public sealed class SimulationResult
{
    private readonly FlightRequest? sourceRequest;

    internal SimulationResult(TrajectorySample[] samples, double requestedDuration)
        : this(samples, requestedDuration, CompletionReason.DurationReached, null, null)
    {
    }

    internal SimulationResult(TrajectorySample[] samples, FlightRequest request,
        CompletionReason completionReason, GroundContactSettings? contact = null)
        : this(samples, (request ?? throw new ArgumentNullException(nameof(request))).Duration,
            completionReason, contact, request)
    {
    }

    private SimulationResult(TrajectorySample[] samples, double requestedDuration,
        CompletionReason completionReason, GroundContactSettings? contact, FlightRequest? request)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (!double.IsFinite(requestedDuration) || requestedDuration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedDuration), requestedDuration,
                "Requested duration must be finite and greater than zero seconds.");
        }

        if (completionReason != CompletionReason.DurationReached && completionReason != CompletionReason.GroundContact)
            throw new ArgumentOutOfRangeException(nameof(completionReason));
        if (completionReason == CompletionReason.GroundContact && contact is null)
            throw new ArgumentException("Ground-contact completion requires contact geometry.", nameof(contact));

        var minimumSamples = completionReason == CompletionReason.GroundContact ? 1 : 2;
        if (samples.Length < minimumSamples || samples.Length > SimulationTimeGrid.MaximumIntegrationSteps + 1)
        {
            throw new ArgumentException($"This completion requires between {minimumSamples} and 100001 samples.", nameof(samples));
        }

        // Own the array before validating it, and expose only a read-only wrapper.
        var snapshot = (TrajectorySample[])samples.Clone();
        for (var i = 0; i < snapshot.Length; i++)
        {
            var sample = snapshot[i];
            var position = sample.State.Position;
            var velocity = sample.State.Velocity;
            if (!double.IsFinite(sample.Time) || sample.Time < 0 ||
                !double.IsFinite(position.X) || !double.IsFinite(position.Y) || !double.IsFinite(position.Z) ||
                !double.IsFinite(velocity.X) || !double.IsFinite(velocity.Y) || !double.IsFinite(velocity.Z))
            {
                throw new ArgumentException($"Sample {i} must have finite physical values and nonnegative time.", nameof(samples));
            }

            if (i > 0 && sample.Time <= snapshot[i - 1].Time)
            {
                throw new ArgumentException($"Sample {i} must strictly follow the preceding sample in time.", nameof(samples));
            }

            if (contact is not null)
            {
                if (position.Z < contact.ContactHeight)
                    throw new ArgumentException($"Sample {i} penetrates the contact plane.", nameof(samples));
                if (position.Z == contact.ContactHeight && velocity.Z <= 0 &&
                    (completionReason != CompletionReason.GroundContact || i != snapshot.Length - 1))
                    throw new ArgumentException($"Sample {i} contains an earlier qualifying contact.", nameof(samples));
            }
        }

        if (snapshot[0].Time != 0 || snapshot[^1].Time > requestedDuration ||
            (completionReason == CompletionReason.DurationReached && snapshot[^1].Time != requestedDuration))
        {
            throw new ArgumentException("Samples must start at zero and end consistently with the completion reason and horizon.", nameof(samples));
        }

        if (request is not null && snapshot[0].State != request.InitialState)
            throw new ArgumentException("The initial sample must match the source request.", nameof(samples));
        if (completionReason == CompletionReason.GroundContact &&
            (snapshot[^1].State.Position.Z != contact!.ContactHeight ||
             (snapshot.Length == 1 ? snapshot[^1].State.Velocity.Z > 0 : snapshot[^1].State.Velocity.Z >= 0)))
            throw new ArgumentException("The terminal sample must be on the plane with appropriate contact velocity.", nameof(samples));

        Samples = new SampleSnapshot(snapshot);
        CompletionReason = completionReason;
        sourceRequest = request;
    }

    /// <summary>Gets the ordered, read-only snapshot, including the initial and terminal samples.</summary>
    public IReadOnlyList<TrajectorySample> Samples { get; }

    /// <summary>Gets the terminal sample's elapsed simulation time in seconds.</summary>
    public double FinalTime => Samples[^1].Time;

    /// <summary>Gets the terminal sample's physical state in SI units.</summary>
    public FlightState FinalState => Samples[^1].State;

    /// <summary>Gets the condition that completed the trajectory.</summary>
    public CompletionReason CompletionReason { get; }

    internal void ValidateGravityContext(FlightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (sourceRequest is null)
            throw new ArgumentException("The result has no production gravity-model provenance.", "result");
        if (sourceRequest.InitialState != request.InitialState ||
            sourceRequest.GravityMagnitude != request.GravityMagnitude ||
            sourceRequest.Duration != request.Duration || sourceRequest.TimeStep != request.TimeStep)
            throw new ArgumentException("The request does not match the result's gravity-model inputs.", nameof(request));
    }

    private sealed class SampleSnapshot(TrajectorySample[] samples)
        : ReadOnlyCollection<TrajectorySample>(samples), ICollection
    {
        // The base wrapper forwards SyncRoot to its backing array, exposing a mutable reference.
        object ICollection.SyncRoot => this;
    }
}
