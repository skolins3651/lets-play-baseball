using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class GravityDynamicsTests
{
    [TestCase(0)]
    [TestCase(10)]
    [TestCase(9.80665)]
    [TestCase(double.MaxValue)]
    public void DerivativeUsesVelocityAndNegativeZGravityIndependentlyOfTimeAndPosition(double gravity)
    {
        var dynamics = new GravityDynamics(gravity);
        var state = new FlightState(new Vector3d(-1, -2, -3), new Vector3d(-4, 5, -6));
        var expected = new FlightDerivative(-4, 5, -6, 0, 0, -gravity);

        Assert.That(dynamics.Evaluate(0, state), Is.EqualTo(expected));
        Assert.That(dynamics.Evaluate(100, state), Is.EqualTo(expected));
        Assert.That(dynamics.Evaluate(1, new FlightState(default, state.Velocity)), Is.EqualTo(expected));
    }

    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void ConstructionRejectsInvalidGravity(double gravity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GravityDynamics(gravity));
    }
}
