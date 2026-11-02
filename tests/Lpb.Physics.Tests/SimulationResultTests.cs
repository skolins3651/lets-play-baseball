using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class SimulationResultTests
{
    [Test]
    public void ResultOwnsReadOnlySnapshotAndDerivesFinalValuesFromLastSample()
    {
        var finalState = new FlightState(new Vector3d(1, 2, 3), new Vector3d(-4, -5, -6));
        var source = new[] { new TrajectorySample(0, default), new TrajectorySample(1, finalState) };
        var result = new SimulationResult(source, 1);
        source[0] = new TrajectorySample(0.5, finalState);
        source[1] = new TrajectorySample(2, default);

        Assert.That(result.Samples[0], Is.EqualTo(default(TrajectorySample)));
        Assert.That(result.Samples[1], Is.EqualTo(new TrajectorySample(1, finalState)));
        Assert.That(result.FinalTime, Is.EqualTo(result.Samples[^1].Time));
        Assert.That(result.FinalState, Is.EqualTo(result.Samples[^1].State));
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
        Assert.That(result.Samples, Is.Not.InstanceOf<TrajectorySample[]>());

        var list = (IList<TrajectorySample>)result.Samples;
        Assert.That(list.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => list[0] = new TrajectorySample(0.5, default));
        Assert.Throws<NotSupportedException>(() => list.Add(default));
        Assert.Throws<NotSupportedException>(() => list.Clear());
        Assert.That(result.Samples.Count, Is.EqualTo(2));
    }

    [Test]
    public void PublicOutputContractsHaveNoPublicConstructionOrPropertyMutationEscape()
    {
        Assert.That(typeof(SimulationResult).GetConstructors(), Is.Empty);
        foreach (var type in new[] { typeof(SimulationResult), typeof(TrajectorySample) })
        {
            foreach (var property in type.GetProperties())
            {
                Assert.That(property.SetMethod, Is.Null, $"{type.Name}.{property.Name}");
            }
        }
    }

    [Test]
    public void LegacyCollectionSyncRootCannotExposeWritableBackingArray()
    {
        var result = new SimulationResult(ValidSamples(), 1);
        var collection = (System.Collections.ICollection)result.Samples;

        Assert.That(collection.SyncRoot, Is.SameAs(result.Samples));
        Assert.That(collection.SyncRoot, Is.Not.InstanceOf<TrajectorySample[]>());
        var list = (System.Collections.IList)result.Samples;
        Assert.That(list.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => list[0] = new TrajectorySample(0.5, default));
    }

    [Test]
    public void ConstructionRejectsNullSamples()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SimulationResult(null!, 1));
        Assert.That(exception!.ParamName, Is.EqualTo("samples"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ConstructionRejectsInvalidRequestedDuration(double duration)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationResult(ValidSamples(), duration));
        Assert.That(exception!.ParamName, Is.EqualTo("requestedDuration"));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(100002)]
    public void ConstructionRejectsInvalidSampleCount(int count)
    {
        var exception = Assert.Throws<ArgumentException>(() => new SimulationResult(new TrajectorySample[count], 1));
        Assert.That(exception!.ParamName, Is.EqualTo("samples"));
    }

    [TestCase(0, 0, 1)]
    [TestCase(0, 0.75, 0.5)]
    [TestCase(0.25, 0.5, 1)]
    [TestCase(0, 0.5, 0.75)]
    [TestCase(0, 2, 1)]
    public void ConstructionRejectsInvalidOrderingAndEndpoints(double first, double middle, double last)
    {
        var samples = new[]
        {
            new TrajectorySample(first, default),
            new TrajectorySample(middle, default),
            new TrajectorySample(last, default)
        };

        var exception = Assert.Throws<ArgumentException>(() => new SimulationResult(samples, 1));
        Assert.That(exception!.ParamName, Is.EqualTo("samples"));
    }

    [Test]
    public void FinalTimeMustMatchDurationExactlyWithoutTolerance()
    {
        Assert.Throws<ArgumentException>(() => new SimulationResult(ValidSamples(), Math.BitIncrement(1d)));
    }

    private static TrajectorySample[] ValidSamples() =>
        [new TrajectorySample(0, default), new TrajectorySample(1, default)];
}
