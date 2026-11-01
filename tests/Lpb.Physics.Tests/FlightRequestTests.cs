using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class FlightRequestTests
{
    [Test]
    public void ConstructionPreservesAllInputsIncludingBelowGroundState()
    {
        var state = new FlightState(new Vector3d(-1, -2, -3), new Vector3d(-4, -5, -6));
        var request = new FlightRequest(state, 9.80665, 2, 0.25);

        Assert.That(request.InitialState, Is.EqualTo(state));
        Assert.That(request.GravityMagnitude, Is.EqualTo(9.80665));
        Assert.That(request.Duration, Is.EqualTo(2));
        Assert.That(request.TimeStep, Is.EqualTo(0.25));
    }

    [Test]
    public void ConstructionAcceptsZeroGravityAndDefaultState()
    {
        var request = new FlightRequest(default, 0, 1, 0.25);

        Assert.That(request.InitialState, Is.EqualTo(new FlightState()));
        Assert.That(request.GravityMagnitude, Is.Zero);
    }

    [Test]
    public void ConstructionAcceptsTimeStepLargerThanDuration()
    {
        var request = new FlightRequest(default, 10, 1, 2);

        Assert.That(request.Duration, Is.EqualTo(1));
        Assert.That(request.TimeStep, Is.EqualTo(2));
    }

    [TestCase(double.Epsilon, double.Epsilon, double.Epsilon)]
    [TestCase(double.MaxValue, double.MaxValue, double.MaxValue)]
    [TestCase(double.MaxValue, double.Epsilon, double.MaxValue)]
    [TestCase(double.Epsilon, double.MaxValue, double.Epsilon)]
    public void ConstructionAcceptsSmallAndLargeFiniteInputs(double gravityMagnitude, double duration, double timeStep)
    {
        var request = new FlightRequest(default, gravityMagnitude, duration, timeStep);

        Assert.That(request.GravityMagnitude, Is.EqualTo(gravityMagnitude));
        Assert.That(request.Duration, Is.EqualTo(duration));
        Assert.That(request.TimeStep, Is.EqualTo(timeStep));
    }

    [TestCase(-1)]
    [TestCase(-double.Epsilon)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ConstructionRejectsInvalidGravityMagnitude(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new FlightRequest(default, value, 1, 0.25));

        Assert.That(exception!.ParamName, Is.EqualTo("gravityMagnitude"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-double.Epsilon)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ConstructionRejectsInvalidDuration(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new FlightRequest(default, 10, value, 0.25));

        Assert.That(exception!.ParamName, Is.EqualTo("duration"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-double.Epsilon)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ConstructionRejectsInvalidTimeStep(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new FlightRequest(default, 10, 1, value));

        Assert.That(exception!.ParamName, Is.EqualTo("timeStep"));
    }
}
