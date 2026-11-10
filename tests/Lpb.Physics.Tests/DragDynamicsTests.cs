using NUnit.Framework;

namespace Lpb.Physics.Tests;

internal static class DragFixtures
{
    internal static AerodynamicSettings Baseball => new(.145, .0366, 1.225, .35);
    internal static double K => 1.225 * .35 * Math.PI * .0366 * .0366 / (2 * .145);
    internal static FlightRequest Oblique(double duration = 6, double step = .01) =>
        new(new(new(0, 0, 1.0366), new(30, 20, 25)), 9.80665, duration, step);
    internal static double[] Components(FlightState s) =>
        [s.Position.X, s.Position.Y, s.Position.Z, s.Velocity.X, s.Velocity.Y, s.Velocity.Z];
}

public class DragDynamicsTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void SettingsPreserveExplicitValuesAndHaveNoMutationPaths(int field)
    {
        var a = DragFixtures.Baseball;
        Assert.That(new[] { a.BallMass, a.BallRadius, a.AirDensity, a.DragCoefficient }[field],
            Is.EqualTo(new[] { .145, .0366, 1.225, .35 }[field]));
        Assert.That(typeof(AerodynamicSettings).GetProperties().All(p => p.SetMethod is null), Is.True);
    }

    [TestCase(0, "ballMass")]
    [TestCase(1, "ballRadius")]
    [TestCase(2, "airDensity")]
    [TestCase(3, "dragCoefficient")]
    public void EveryInvalidScalarIsRejected(int field, string parameter)
    {
        foreach (var invalid in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var v = new[] { .145, .0366, 1.225, .35 }; v[field] = invalid;
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new AerodynamicSettings(v[0], v[1], v[2], v[3]))!.ParamName,
                Is.EqualTo(parameter));
        }
        if (field < 2)
        {
            var v = new[] { .145, .0366, 1.225, .35 }; v[field] = 0;
            Assert.Throws<ArgumentOutOfRangeException>(() => new AerodynamicSettings(v[0], v[1], v[2], v[3]));
        }
    }

    [TestCase(0)]
    [TestCase(9.80665)]
    public void ThreeAxisAccelerationUsesFullSpeedAndOpposesVelocity(double g)
    {
        var derivative = new GravityDragDynamics(g, DragFixtures.Baseball).Evaluate(0, new(default, new(3, -4, 12)));
        Assert.That(derivative.PositionXRate, Is.EqualTo(3));
        Assert.That(derivative.PositionYRate, Is.EqualTo(-4));
        Assert.That(derivative.PositionZRate, Is.EqualTo(12));
        Assert.That(derivative.VelocityXRate, Is.EqualTo(-DragFixtures.K * 13 * 3).Within(1e-14));
        Assert.That(derivative.VelocityYRate, Is.EqualTo(DragFixtures.K * 13 * 4).Within(1e-14));
        Assert.That(derivative.VelocityZRate, Is.EqualTo(-g - DragFixtures.K * 13 * 12).Within(1e-14));
    }

    [TestCase(2, 1, 1, 1, .5)]
    [TestCase(1, 2, 1, 1, 4)]
    [TestCase(1, 1, 2, 1, 2)]
    [TestCase(1, 1, 1, 2, 2)]
    public void ParametersHaveTheirPhysicalScaling(double mass, double radius, double rho, double cd, double factor)
    {
        var a = new AerodynamicSettings(.145 * mass, .0366 * radius, 1.225 * rho, .35 * cd);
        var d = new GravityDragDynamics(0, a).Evaluate(0, new(default, new(40, 0, 0)));
        Assert.That(d.VelocityXRate, Is.EqualTo(-DragFixtures.K * 1600 * factor).Within(1e-12));
    }

    [TestCase(0, .35)]
    [TestCase(1.225, 0)]
    public void DisabledDragDoesNotEvaluateUnsafeFactorsOrSpeed(double rho, double cd)
    {
        var state = new FlightState(default, new(double.MaxValue, double.MaxValue, 0));
        var d = new GravityDragDynamics(10, new(double.Epsilon, double.MaxValue, rho, cd)).Evaluate(0, state);
        Assert.That(d, Is.EqualTo(new FlightDerivative(double.MaxValue, double.MaxValue, 0, 0, 0, -10)));
    }

    [Test]
    public void InstantaneouslyZeroVelocityHasExactlyZeroDragDespiteExtremeCoefficient()
    {
        Assert.That(new GravityDragDynamics(10, new(double.Epsilon, double.MaxValue, double.MaxValue, double.MaxValue))
            .Evaluate(0, default), Is.EqualTo(new FlightDerivative(0, 0, 0, 0, 0, -10)));
    }

    [TestCase(1e-300, 1e-100, 1e200)]
    [TestCase(1e300, 1e100, 1e-200)]
    public void CompensatingFactorsAgreeWithIndependentPiOverTwoReference(double rho, double cd, double radius)
    {
        // Independent high-precision product of the exact binary64 inputs rounds to π/2.
        var d = new GravityDragDynamics(0, new(1, radius, rho, cd)).Evaluate(0, new(default, new(1, 0, 0)));
        Assert.That(d.VelocityXRate, Is.EqualTo(-Math.PI / 2).Within(2e-15));
    }

    [Test]
    public void FactoredNormAndOriginalComponentsAvoidPrematureRangeLoss()
    {
        var a = new AerodynamicSettings(Math.PI / 2, 1, 1e-310, 1);
        var d = new GravityDragDynamics(0, a).Evaluate(0, new(default, new(1.3e308, 1.3e308, 0)));
        Assert.That(d.VelocityXRate / -2.390020920410508e306, Is.EqualTo(1).Within(1e-12));
        var disparate = new GravityDragDynamics(0, new(Math.PI / 2, 1, 1e-308, 1))
            .Evaluate(0, new(default, new(1e308, 1e-308, 0)));
        Assert.That(disparate.VelocityYRate, Is.LessThan(0));
        Assert.That(disparate.VelocityYRate / -1e-308, Is.EqualTo(1).Within(1e-12));
        var small = new GravityDragDynamics(0, new(Math.PI / 2, 1, 1e300, 1))
            .Evaluate(0, new(default, new(1e-200, 0, 0)));
        Assert.That(small.VelocityXRate / -1e-100, Is.EqualTo(1).Within(1e-12));
    }

    [TestCase(1e-300, 1e-15, "underflow")]
    [TestCase(1, 1e200, "overflow")]
    public void UnrepresentableFinalAccelerationFailsExplicitly(double rho, double vx, string diagnostic)
    {
        Assert.That(Assert.Throws<ArithmeticException>(() => new GravityDragDynamics(0, new(Math.PI / 2, 1, rho, 1))
            .Evaluate(0, new(default, new(vx, 0, 0))))!.Message, Does.Contain(diagnostic));
    }
}
