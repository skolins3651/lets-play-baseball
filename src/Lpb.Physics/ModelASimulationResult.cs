using System.Collections;
using System.Collections.ObjectModel;

namespace Lpb.Physics;

/// <summary>An immutable, duration-complete Model A sample snapshot.</summary>
/// <remarks>
/// There is no public constructor. ModelASimulator produces checked duration results; internal construction
/// validates sample structure and retained inputs, not dynamics or numerical acceptance.
/// </remarks>
public sealed class ModelASimulationResult
{
    private readonly ModelAFlightProvenance provenance;

    internal ModelASimulationResult(SpinTrajectorySample[] samples, ModelAFlightProvenance source,
        CompletionReason completionReason)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(source);
        if (completionReason != CompletionReason.DurationReached)
            throw new ArgumentOutOfRangeException(nameof(completionReason), completionReason,
                "Only duration completion is supported by the Model A contract.");
        if (samples.Length < 2 || samples.Length > SimulationTimeGrid.MaximumIntegrationSteps + 1)
            throw new ArgumentException("Duration completion requires between 2 and 100001 samples.", nameof(samples));

        var snapshot = (SpinTrajectorySample[])samples.Clone();
        for (var i = 0; i < snapshot.Length; i++)
        {
            var sample = snapshot[i];
            var translation = sample.State.Translation;
            var position = translation.Position;
            var velocity = translation.Velocity;
            var spin = sample.State.AngularVelocity;
            if (!double.IsFinite(sample.Time) || sample.Time < 0 ||
                !double.IsFinite(position.X) || !double.IsFinite(position.Y) || !double.IsFinite(position.Z) ||
                !double.IsFinite(velocity.X) || !double.IsFinite(velocity.Y) || !double.IsFinite(velocity.Z) ||
                !double.IsFinite(spin.X) || !double.IsFinite(spin.Y) || !double.IsFinite(spin.Z))
                throw new ArgumentException($"Sample {i} must have finite physical values and nonnegative time.", nameof(samples));
            if (i > 0 && sample.Time <= snapshot[i - 1].Time)
                throw new ArgumentException($"Sample {i} must strictly follow the preceding sample in time.", nameof(samples));
        }

        if (snapshot[0].Time != 0 || snapshot[^1].Time != source.Request.Duration)
            throw new ArgumentException("Samples must start at zero and end at the exact requested duration.", nameof(samples));
        if (snapshot[0].State != source.Request.InitialState)
            throw new ArgumentException("The initial sample must match the complete source request state.", nameof(samples));

        provenance = source;
        Samples = new SampleSnapshot(snapshot);
        CompletionReason = completionReason;
    }

    /// <summary>Gets the ordered read-only snapshot, including release and terminal samples.</summary>
    public IReadOnlyList<SpinTrajectorySample> Samples { get; }

    /// <summary>Gets the terminal sample's complete physical state in SI units.</summary>
    public SpinFlightState FinalState => Samples[^1].State;

    /// <summary>Gets the terminal sample's elapsed time in seconds.</summary>
    public double FinalTime => Samples[^1].Time;

    /// <summary>Gets duration completion; this contract has no contact completion.</summary>
    public CompletionReason CompletionReason { get; }

    /// <summary>Gets the fixed Model A and Nathan ASR-B physical prescription identity.</summary>
    public string ModelIdentity => provenance.ModelIdentity;

    private sealed class SampleSnapshot(SpinTrajectorySample[] samples)
        : ReadOnlyCollection<SpinTrajectorySample>(samples), ICollection
    {
        // The base wrapper's SyncRoot otherwise exposes its backing array.
        object ICollection.SyncRoot => this;
    }
}
