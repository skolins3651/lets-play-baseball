using System.Collections;
using System.Collections.ObjectModel;

namespace Lpb.Physics;

/// <summary>An immutable snapshot of a successfully completed gravity-only trajectory.</summary>
/// <remarks>Execution failures throw exceptions; they do not return partial successful results.</remarks>
public sealed class SimulationResult
{
    internal SimulationResult(TrajectorySample[] samples, double requestedDuration)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (!double.IsFinite(requestedDuration) || requestedDuration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedDuration), requestedDuration,
                "Requested duration must be finite and greater than zero seconds.");
        }

        if (samples.Length < 2 || samples.Length > SimulationTimeGrid.MaximumIntegrationSteps + 1)
        {
            throw new ArgumentException("A completed trajectory requires between 2 and 100001 samples.", nameof(samples));
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
        }

        if (snapshot[0].Time != 0 || snapshot[^1].Time != requestedDuration)
        {
            throw new ArgumentException("Samples must start at time zero and end at the exact requested duration.", nameof(samples));
        }

        Samples = new SampleSnapshot(snapshot);
    }

    /// <summary>Gets the ordered, read-only snapshot, including the initial and terminal samples.</summary>
    public IReadOnlyList<TrajectorySample> Samples { get; }

    /// <summary>Gets the terminal sample's elapsed simulation time in seconds.</summary>
    public double FinalTime => Samples[^1].Time;

    /// <summary>Gets the terminal sample's physical state in SI units.</summary>
    public FlightState FinalState => Samples[^1].State;

    /// <summary>Gets the condition that completed the trajectory.</summary>
    public CompletionReason CompletionReason => CompletionReason.DurationReached;

    private sealed class SampleSnapshot(TrajectorySample[] samples)
        : ReadOnlyCollection<TrajectorySample>(samples), ICollection
    {
        // The base wrapper forwards SyncRoot to its backing array, exposing a mutable reference.
        object ICollection.SyncRoot => this;
    }
}
