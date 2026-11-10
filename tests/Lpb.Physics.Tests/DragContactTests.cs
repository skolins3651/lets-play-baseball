using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class DragContactTests
{
    [TestCase(0)]
    [TestCase(-1)]
    public void ImmediateContactBypassesUnsafeIntegrationAndRetainsProvenance(double vz)
    {
        var a = DragFixtures.Baseball;
        var r = new FlightRequest(new(new(0, 0, a.BallRadius), new(0, 0, vz)), double.MaxValue, double.Epsilon, double.Epsilon);
        var result = GravityDragSimulator.SimulateUntilGroundContact(r, a, 0);
        Assert.That(result.Samples.Count, Is.EqualTo(1));
        Assert.That(result.FinalTime, Is.Zero);
        Assert.That(result.ModelKind, Is.EqualTo(FlightModelKind.GravityAndDrag));
        Assert.That(GravityDragTrajectory.Evaluate(result, r, a, 0), Is.EqualTo(r.InitialState));
        Assert.That(GravityDragTrajectory.GetMaximumHeight(result, r, a), Is.EqualTo(result.Samples[0]));
    }

    [Test]
    public void VerticalDropMatchesIndependentTanhAndContactReferenceWithAuthoritativeRadius()
    {
        var a = DragFixtures.Baseball;
        const double g = 10, ground = 2;
        var request = new FlightRequest(new(new(0, 0, ground + a.BallRadius + 1), default), g, 10, .001);
        var result = GravityDragSimulator.SimulateUntilGroundContact(request, a, ground);
        var omega = Math.Sqrt(g * DragFixtures.K);
        var expectedTime = Math.Acosh(Math.Exp(DragFixtures.K)) / omega;
        Assert.That(result.FinalTime, Is.EqualTo(expectedTime).Within(1e-8));
        Assert.That(result.FinalState.Position.Z, Is.EqualTo(ground + a.BallRadius));
        Assert.That(result.FinalState.Velocity.Z, Is.EqualTo(-Math.Sqrt(g / DragFixtures.K) * Math.Tanh(omega * expectedTime)).Within(1e-8));
        Assert.That(TrajectoryMeasurements.GetGroundContact(result), Is.EqualTo(result.Samples[^1]));
        Assert.That(result.Samples.All(s => s.State.Position.Z >= ground + a.BallRadius), Is.True);
        Assert.That(GravityDragTrajectory.Evaluate(result, request, a, result.FinalTime), Is.EqualTo(result.FinalState));
        Assert.Throws<ArgumentOutOfRangeException>(() => GravityDragTrajectory.Evaluate(result, request, a, Math.BitIncrement(result.FinalTime)));
        var before = result.FinalTime - 1e-6;
        Assert.That(GravityDragTrajectory.Evaluate(result, request, a, before).Position.Z, Is.GreaterThan(result.FinalState.Position.Z));
    }

    [Test]
    public void NonzeroDragLaunchAndReturnCanBothOccurInOneOrdinaryStep()
    {
        var a = DragFixtures.Baseball;
        const double v = .01, g = 10;
        var request = new FlightRequest(new(default, new(0, 0, v)), g, .0025, .0025);
        var result = GravityDragSimulator.SimulateUntilGroundContact(request, a, -a.BallRadius);
        var x = v * Math.Sqrt(DragFixtures.K / g);
        var expected = (Math.Atan(x) + Math.Asinh(x)) / Math.Sqrt(g * DragFixtures.K);
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
        Assert.That(result.Samples.Count, Is.EqualTo(2));
        Assert.That(result.FinalTime, Is.EqualTo(expected).Within(1e-9));
        Assert.That(result.FinalState.Position.Z, Is.Zero);
        Assert.That(result.FinalState.Velocity.Z, Is.LessThan(0));
    }

    [TestCase(-1, CompletionReason.GroundContact)]
    [TestCase(0, CompletionReason.GroundContact)]
    [TestCase(1, CompletionReason.DurationReached)]
    public void HorizonUsesNumericalCrossingEvidenceNotHeightProximity(int offset, CompletionReason expected)
    {
        var a = new AerodynamicSettings(.145, .0366, 1e-20, .35);
        var request = new FlightRequest(new(new(0, 0, 1 + offset * Math.ScaleB(1, -32)), new(0, 0, -1)), 0, 1, 1);
        var result = GravityDragSimulator.SimulateUntilGroundContact(request, a, -a.BallRadius);
        Assert.That(result.CompletionReason, Is.EqualTo(expected));
        if (offset > 0)
        {
            Assert.That(result.FinalState.Position.Z, Is.GreaterThan(0).And.LessThan(1e-8));
            Assert.That(TrajectoryMeasurements.GetGroundContact(result), Is.Null);
        }
        else if (offset == 0) Assert.That(result.FinalTime, Is.EqualTo(1));
        else Assert.That(result.FinalTime, Is.LessThan(1));
    }

    [Test]
    public void ExactLaterContactAtHorizonExcludesLaunchRoot()
    {
        var a = new AerodynamicSettings(.145, .0366, 1e-20, .35);
        var r = new FlightRequest(new(default, new(0, 0, 10)), 10, 2, 2);
        var result = GravityDragSimulator.SimulateUntilGroundContact(r, a, -a.BallRadius);
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
        Assert.That(result.FinalTime, Is.EqualTo(2));
        Assert.That(result.Samples.Count, Is.EqualTo(2));
    }

    [Test]
    public void InitialPenetrationAndUnsafeGeometryRejectWithoutTolerance()
    {
        var a = DragFixtures.Baseball;
        var request = new FlightRequest(new(new(0, 0, -double.Epsilon), new(0, 0, 1)), 10, 1, .01);
        Assert.That(Assert.Throws<ArgumentException>(() => GravityDragSimulator.SimulateUntilGroundContact(request, a, -a.BallRadius))!.ParamName, Is.EqualTo("request"));
        Assert.Throws<ArgumentOutOfRangeException>(() => GravityDragSimulator.SimulateUntilGroundContact(request, a, double.NaN));
        Assert.That(Assert.Throws<ArgumentException>(() => GravityDragSimulator.SimulateUntilGroundContact(request, a, double.MaxValue))!.ParamName, Is.EqualTo("groundElevation"));
    }

    [Test]
    public void UnconstrainedAndActualWorkLimitsRemainDistinct()
    {
        var a = DragFixtures.Baseball;
        var r = new FlightRequest(new(new(0, 0, 1), new(0, 0, -1)), 0, 1e6, .001);
        Assert.Throws<InvalidOperationException>(() => GravityDragSimulator.Simulate(r, a));
        Assert.That(GravityDragSimulator.SimulateUntilGroundContact(r, a, 0).FinalTime, Is.LessThan(2));
        var stationary = new FlightRequest(new(new(0, 0, 1), default), 0, 100001, 1);
        SimulationResult? partial = null;
        Assert.Throws<InvalidOperationException>(() => partial = GravityDragSimulator.SimulateUntilGroundContact(stationary, a, 0));
        Assert.That(partial, Is.Null);
        var maximum = GravityDragSimulator.Simulate(new(default, 0, 100000, 1), a);
        Assert.That(maximum.Samples.Count, Is.EqualTo(100001));
        Assert.That(maximum.FinalTime, Is.EqualTo(100000));
    }

    [Test]
    public void ContactInLastOrdinaryStepStillSucceedsWithEnabledDrag()
    {
        var a = new AerodynamicSettings(.145, .0366, 1e-20, .35);
        var request = new FlightRequest(new(new(0, 0, 99999.5), new(0, 0, -1)), 0, 1e6, 1);
        var result = GravityDragSimulator.SimulateUntilGroundContact(request, a, -a.BallRadius);
        Assert.That(result.FinalTime, Is.EqualTo(99999.5));
        Assert.That(result.Samples.Count, Is.EqualTo(100001));
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
    }
}
