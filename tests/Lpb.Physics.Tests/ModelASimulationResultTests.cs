using System.Collections;
using System.Reflection;
using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class ModelASimulationResultTests
{
    // Contract fixtures only: a stationary, zero-gravity R0 case. No simulator is invoked.
    private static readonly SpinFlightState Stationary = new(new(new(-1, 2, -3), default), new(4, -5, 6));

    [Test]
    public void ResultOwnsSamplesAndRetainsPrivateProvenanceWithoutExecutionAttestation()
    {
        var source = Source(Stationary);
        var input = new[] { new SpinTrajectorySample(0, Stationary), new SpinTrajectorySample(1, Stationary) };
        var result = new ModelASimulationResult(input, source, CompletionReason.DurationReached);
        input[0] = default;
        input[1] = new(2, default);
        Assert.That(result.Samples[0], Is.EqualTo(new SpinTrajectorySample(0, Stationary)));
        Assert.That(result.Samples[^1], Is.EqualTo(new SpinTrajectorySample(1, Stationary)));
        Assert.That(result.FinalState, Is.EqualTo(result.Samples[^1].State));
        Assert.That(result.FinalTime, Is.EqualTo(result.Samples[^1].Time));
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
        Assert.That(result.ModelIdentity, Is.EqualTo("model-a/nathan-asr-b-v1"));
        var retainedField = typeof(ModelASimulationResult).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(f => f.FieldType == typeof(ModelAFlightProvenance));
        Assert.That(retainedField.IsPrivate && retainedField.IsInitOnly, Is.True);
        Assert.That(retainedField.GetValue(result), Is.SameAs(source));
        Assert.That(source.NumericalPolicyIdentity, Is.Null);
    }

    [Test]
    public void ReadOnlyInterfacesSyncRootAndCopiesCannotMutateTheSnapshot()
    {
        var result = Construct(ValidSamples());
        var generic = (IList<SpinTrajectorySample>)result.Samples;
        var legacy = (IList)result.Samples;
        Assert.That(result.Samples, Is.Not.InstanceOf<SpinTrajectorySample[]>());
        Assert.That(generic.IsReadOnly, Is.True);
        Assert.That(legacy.IsReadOnly, Is.True);
        Assert.That(((ICollection)result.Samples).SyncRoot, Is.SameAs(result.Samples));
        Assert.That(((ICollection)result.Samples).SyncRoot, Is.Not.InstanceOf<SpinTrajectorySample[]>());
        Assert.Throws<NotSupportedException>(() => generic[0] = default);
        Assert.Throws<NotSupportedException>(() => generic.Add(default));
        Assert.Throws<NotSupportedException>(() => generic.Clear());
        Assert.Throws<NotSupportedException>(() => legacy[0] = default(SpinTrajectorySample));
        var copy = new SpinTrajectorySample[2];
        ((ICollection)result.Samples).CopyTo(copy, 0);
        copy[1] = default;
        Assert.That(result.Samples[^1].Time, Is.EqualTo(1));
    }

    [Test]
    public void NullSamplesAndProvenanceAreRejected()
    {
        Assert.That(Assert.Throws<ArgumentNullException>(() => Construct(null!))!.ParamName, Is.EqualTo("samples"));
        Assert.That(Assert.Throws<ArgumentNullException>(() =>
            new ModelASimulationResult(ValidSamples(), null!, CompletionReason.DurationReached))!.ParamName, Is.EqualTo("source"));
    }

    [TestCase(CompletionReason.GroundContact)]
    [TestCase((CompletionReason)(-1))]
    [TestCase((CompletionReason)2)]
    public void OnlyDurationCompletionIsAccepted(CompletionReason reason) =>
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ModelASimulationResult(ValidSamples(), Source(), reason))!.ParamName, Is.EqualTo("completionReason"));

    [TestCase(0)] [TestCase(1)] [TestCase(100002)]
    public void ResultRejectsSampleCountsOutsideTheApprovedBounds(int count) =>
        Assert.That(Assert.Throws<ArgumentException>(() => Construct(new SpinTrajectorySample[count]))!.ParamName,
            Is.EqualTo("samples"));

    [Test]
    public void MaximumSampleCountIsAcceptedForStationaryContractData()
    {
        var samples = Enumerable.Range(0, 100001).Select(i => new SpinTrajectorySample(i, default)).ToArray();
        var result = new ModelASimulationResult(samples, Source(duration: 100000), CompletionReason.DurationReached);
        Assert.That(result.Samples.Count, Is.EqualTo(100001));
        Assert.That(result.FinalTime, Is.EqualTo(100000));
    }

    [TestCase(0, 0, 1)]
    [TestCase(0, .75, .5)]
    [TestCase(.25, .5, 1)]
    [TestCase(0, .5, .75)]
    [TestCase(0, .5, 2)]
    public void OrderingAndDurationEndpointsMustBeExact(double first, double middle, double last) =>
        Assert.Throws<ArgumentException>(() => Construct([new(first, default), new(middle, default), new(last, default)]));

    [TestCase(false)] [TestCase(true)]
    public void AdjacentTerminalTimesAreNotMergedIntoTheHorizon(bool above)
    {
        var time = above ? Math.BitIncrement(1d) : Math.BitDecrement(1d);
        Assert.Throws<ArgumentException>(() => Construct([new(0, default), new(time, default)]));
    }

    [Test]
    public void TimeStepGreaterThanDurationDoesNotInvalidateAContractSnapshot()
    {
        var source = new ModelAFlightProvenance(new(default, 0, 1, 2),
            ModelAParameters.NathanAsrB(new(1, 1, 0, 0), ModelARotationSettings.R0()));
        Assert.That(new ModelASimulationResult(ValidSamples(), source, CompletionReason.DurationReached).FinalTime, Is.EqualTo(1));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)]
    public void InitialStateMustMatchEveryComponentOfTheSource(int component)
    {
        var values = new double[9];
        values[component] = double.Epsilon;
        var state = SpinFlightContractTests.FromComponents(values);
        Assert.Throws<ArgumentException>(() => Construct([new(0, state), new(1, default)]));
    }

    [TestCaseSource(nameof(NonfiniteComponents))]
    public void ResultRevalidatesEveryPhysicalComponent(int component, double value)
    {
        var coordinate = new[] { "X", "Y", "Z" }[component % 3];
        var vector = Corrupt(default(Vector3d), coordinate, value);
        var state = component switch
        {
            < 3 => new SpinFlightState(new(vector, default), default),
            < 6 => new SpinFlightState(new(default, vector), default),
            _ => new SpinFlightState(default, vector)
        };
        Assert.Throws<ArgumentException>(() => Construct([new(0, default), new(1, state)]));
    }

    [TestCase(-double.Epsilon)] [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
    public void ResultRevalidatesTimestampDespiteAnInvalidInternalCaller(double time)
    {
        var terminal = Corrupt(new SpinTrajectorySample(1, default), "Time", time);
        Assert.Throws<ArgumentException>(() => Construct([new(0, default), terminal]));
    }

    private static IEnumerable<TestCaseData> NonfiniteComponents()
    {
        for (var component = 0; component < 9; component++)
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                yield return new TestCaseData(component, value);
    }

    private static T Corrupt<T>(T value, string property, double invalid) where T : struct
    {
        object box = value;
        typeof(T).GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(box, invalid);
        return (T)box;
    }

    private static SpinTrajectorySample[] ValidSamples() => [new(0, default), new(1, default)];

    private static ModelAFlightProvenance Source(SpinFlightState initial = default, double duration = 1) =>
        new(new(initial, 0, duration, 1), ModelAParameters.NathanAsrB(new(1, 1, 0, 0), ModelARotationSettings.R0()));

    private static ModelASimulationResult Construct(SpinTrajectorySample[] samples) =>
        new(samples, Source(), CompletionReason.DurationReached);
}
