using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class FlightStateTests
{
    [Test]
    public void ConstructionPreservesNegativePositionAndVelocityComponents()
    {
        var state = new FlightState(new Vector3d(-1.25, -2.5, -3.75), new Vector3d(-4.5, -5.25, -6.125));

        Assert.That(state.Position.X, Is.EqualTo(-1.25));
        Assert.That(state.Position.Y, Is.EqualTo(-2.5));
        Assert.That(state.Position.Z, Is.EqualTo(-3.75));
        Assert.That(state.Velocity.X, Is.EqualTo(-4.5));
        Assert.That(state.Velocity.Y, Is.EqualTo(-5.25));
        Assert.That(state.Velocity.Z, Is.EqualTo(-6.125));
    }

    [Test]
    public void DefaultAndParameterlessConstructionProduceZeroStates()
    {
        FlightState defaultState = default;
        var parameterlessState = new FlightState();
        var zero = new Vector3d(0, 0, 0);

        Assert.That(defaultState.Position, Is.EqualTo(zero));
        Assert.That(defaultState.Velocity, Is.EqualTo(zero));
        Assert.That(parameterlessState.Position, Is.EqualTo(zero));
        Assert.That(parameterlessState.Velocity, Is.EqualTo(zero));
    }
}
