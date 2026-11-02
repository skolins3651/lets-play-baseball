using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class TrajectorySampleTests
{
    [Test]
    public void ConstructionPreservesTimeAndState()
    {
        var state = new FlightState(new Vector3d(-1, 2, -3), new Vector3d(4, -5, 6));
        var sample = new TrajectorySample(0.25, state);

        Assert.That(sample.Time, Is.EqualTo(0.25));
        Assert.That(sample.State, Is.EqualTo(state));
    }

    [Test]
    public void DefaultAndParameterlessSamplesAreValidZeroSamples()
    {
        Assert.That(default(TrajectorySample), Is.EqualTo(new TrajectorySample(0, default)));
        Assert.That(new TrajectorySample(), Is.EqualTo(default(TrajectorySample)));
    }

    [TestCase(double.Epsilon)]
    [TestCase(double.MaxValue)]
    public void ConstructionAcceptsExtremeFiniteTimes(double time)
    {
        Assert.That(new TrajectorySample(time, default).Time, Is.EqualTo(time));
    }

    [TestCase(-1)]
    [TestCase(-double.Epsilon)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ConstructionRejectsInvalidTime(double time)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TrajectorySample(time, default));

        Assert.That(exception!.ParamName, Is.EqualTo("time"));
    }
}
