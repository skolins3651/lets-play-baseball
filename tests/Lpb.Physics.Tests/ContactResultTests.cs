using System.Collections;
using System.Reflection;
using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class ContactResultTests
{
    private static SimulationResult Construct(TrajectorySample[] samples, CompletionReason reason = CompletionReason.GroundContact,
        GroundContactSettings? contact = null, double duration = 2) =>
        new(samples, new FlightRequest(samples.Length == 0 ? default : samples[0].State, 10, duration, 1), reason, contact ?? FlightFixtures.ZeroPlane);

    [TestCase(-1)]
    [TestCase(2)]
    public void UnknownCompletionReasonsAreRejected(int reason) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Construct([new(0, default)], (CompletionReason)reason));

    [Test]
    public void GroundCompletionRequiresGeometry() =>
        Assert.Throws<ArgumentException>(() => new SimulationResult([new(0, default)], new FlightRequest(default, 10, 2, 1), CompletionReason.GroundContact));

    [TestCase(0)]
    [TestCase(100002)]
    public void InvalidContactSampleCountsAreRejected(int count) =>
        Assert.Throws<ArgumentException>(() => Construct(new TrajectorySample[count]));

    [Test]
    public void SingleSampleIsOnlyValidForGenuineImmediateContact()
    {
        var valid = Construct([new(0, FlightFixtures.State(0, -1))]);
        Assert.That(valid.FinalTime, Is.Zero);
        Assert.Throws<ArgumentException>(() => Construct([new(0, FlightFixtures.State(1, -1))]));
        Assert.Throws<ArgumentException>(() => Construct([new(0, FlightFixtures.State(0, 1))]));
        Assert.Throws<ArgumentException>(() => Construct([new(1, FlightFixtures.State(0, -1))]));
        Assert.Throws<ArgumentException>(() => Construct([new(0, default)], CompletionReason.DurationReached));
    }

    [TestCase(1, 1, -1)]
    [TestCase(1, 0, 0)]
    [TestCase(1, -1, -1)]
    [TestCase(3, 0, -1)]
    public void RejectsOffPlaneStationaryPenetratingOrBeyondHorizonTerminalStates(double time, double height, double velocity)
    {
        Assert.Throws<ArgumentException>(() => Construct([new(0, FlightFixtures.State(2, 0)), new(time, FlightFixtures.State(height, velocity))]));
    }

    [Test]
    public void RejectsEarlierContactPenetrationOrderingAndWrongInitialState()
    {
        Assert.Throws<ArgumentException>(() => Construct([new(0, default), new(1, FlightFixtures.State(0, -1))]));
        Assert.Throws<ArgumentException>(() => Construct([new(0, FlightFixtures.State(-1, 1)), new(1, FlightFixtures.State(0, -1))]));
        Assert.Throws<ArgumentException>(() => Construct([new(0, FlightFixtures.State(2, 0)), new(0, FlightFixtures.State(0, -1))]));
        Assert.Throws<ArgumentException>(() => Construct([new(0, FlightFixtures.State(2, 0)), new(1, FlightFixtures.State(0, -1)), new(2, FlightFixtures.State(0, -2))]));
        Assert.Throws<ArgumentException>(() => new SimulationResult([new(0, default), new(1, default)],
            new FlightRequest(FlightFixtures.State(1, 0), 0, 1, 1), CompletionReason.DurationReached));
        Assert.Throws<ArgumentException>(() => Construct([new(0, FlightFixtures.State(2, 0)), new(1, FlightFixtures.State(1, -1))], CompletionReason.DurationReached));
    }

    [Test]
    public void ContactResultsPreserveDefensiveOwnershipAndSyncRootProtection()
    {
        var source = new[] { new TrajectorySample(0, FlightFixtures.State(2, 0)), new TrajectorySample(1, FlightFixtures.State(0, -1)) };
        var result = Construct(source);
        var final = result.Samples[^1];
        source[1] = default;
        Assert.That(result.Samples[^1], Is.EqualTo(final));
        Assert.That(result.FinalState, Is.EqualTo(final.State));
        Assert.That(result.FinalTime, Is.EqualTo(final.Time));
        Assert.That(((ICollection)result.Samples).SyncRoot, Is.SameAs(result.Samples));
        Assert.That(result.Samples, Is.Not.InstanceOf<TrajectorySample[]>());
        Assert.Throws<NotSupportedException>(() => ((IList)result.Samples)[0] = default(TrajectorySample));
        Assert.Throws<NotSupportedException>(() => ((IList<TrajectorySample>)result.Samples).Clear());
        foreach (var property in typeof(SimulationResult).GetProperties())
            Assert.That(property.SetMethod, Is.Null);
        Assert.That(typeof(SimulationResult).GetConstructors(), Is.Empty);
    }

    [TestCase("X", false)]
    [TestCase("Y", false)]
    [TestCase("Z", false)]
    [TestCase("X", true)]
    [TestCase("Y", true)]
    [TestCase("Z", true)]
    public void EveryPhysicalComponentIsRevalidated(string component, bool velocity)
    {
        // Simulate an invalid assembly caller without adding a public mutation path.
        var vector = Corrupt(default(Vector3d), component, double.NaN);
        var state = velocity ? new FlightState(default, vector) : new FlightState(vector, default);
        Assert.Throws<ArgumentException>(() => new SimulationResult([new(0, default), new(1, state)], 1));
    }

    [Test]
    public void ResultRevalidatesTimestampFiniteness()
    {
        var sample = Corrupt(new TrajectorySample(1, default), "Time", double.NaN);
        Assert.Throws<ArgumentException>(() => new SimulationResult([new(0, default), sample], 1));
    }

    private static T Corrupt<T>(T value, string property, double invalid) where T : struct
    {
        object box = value;
        typeof(T).GetField($"<{property}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(box, invalid);
        return (T)box;
    }
}
