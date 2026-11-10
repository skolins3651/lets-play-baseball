using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class DragReleaseReachabilityTests
{
    [TestCase(1)]
    [TestCase(1.5)]
    [TestCase(1048576)]
    public void AccumulatedRoundingCannotEraseReleaseClearance(double height)
    {
        var unit = Math.BitIncrement(height) - height;
        var duration = 3 + 2.2 / 3.6;
        var a = new AerodynamicSettings(.145, 1, 1.225, .35);
        var request = new FlightRequest(new(new(0, 0, height + 14 * unit), new(0, 0, -3.6 * unit)), 0, duration, 1);
        var numerical = new DragNumericalEvaluation(request, a);
        var anchor = new TrajectorySample(0, request.InitialState);
        foreach (var clearance in new[] { 10, 6, 2 })
        {
            var time = anchor.Time + 1;
            anchor = new(time, numerical.Step(anchor, time));
            Assert.That((anchor.State.Position.Z - height) / unit, Is.EqualTo(clearance));
        }
        var endpoint = numerical.EvaluateStep(anchor, duration);
        Assert.That(endpoint.Full.Position.Z, Is.EqualTo(height));
        Assert.That(endpoint.Diagnostic.Position.Z, Is.EqualTo(height));
        // The local guard alone permits this candidate; the release bound disproves it.
        Assert.That(anchor.State.Position.Z - height,
            Is.LessThan(-anchor.State.Velocity.Z * (duration - anchor.Time)));
        Assert.That(request.InitialState.Position.Z - height,
            Is.GreaterThan(-request.InitialState.Velocity.Z * duration));
        SimulationResult? partial = null;
        var error = Assert.Throws<ArithmeticException>(() =>
            partial = GravityDragSimulator.SimulateUntilGroundContact(request, a, height - 1));
        Assert.That(error!.Message, Does.Contain("release-origin").And.Contain("Integration step 4").And.Contain("is unreachable"));
        Assert.That(partial, Is.Null);
    }

    [TestCase(1)]
    [TestCase(1.5)]
    [TestCase(1048576)]
    public void ReleaseBoundAlsoProtectsCandidatesFoundDuringRefinement(double height)
    {
        var unit = Math.BitIncrement(height) - height;
        var a = new AerodynamicSettings(.145, 1, 1.225, .35);
        var request = new FlightRequest(new(new(0, 0, height + 13 * unit), new(0, 0, -3.6 * unit)), 0, 4, 1);
        var numerical = new DragNumericalEvaluation(request, a);
        var anchor = new TrajectorySample(0, request.InitialState);
        for (var i = 1; i <= 3; i++) anchor = new(i, numerical.Step(anchor, i));
        var ordinary = numerical.EvaluateStep(anchor, 4);
        Assert.That(ordinary.Full.Position.Z, Is.LessThanOrEqualTo(height));
        Assert.That(ordinary.Diagnostic.Position.Z, Is.LessThanOrEqualTo(height));
        Assert.DoesNotThrow(() => numerical.CheckContactReachability(anchor, 4, height));
        var trial = numerical.EvaluateStep(anchor, 3.5);
        Assert.That(trial.Full.Position.Z, Is.LessThanOrEqualTo(height));
        Assert.That(trial.Diagnostic.Position.Z, Is.LessThanOrEqualTo(height));
        Assert.That(anchor.State.Position.Z - height, Is.LessThan(-anchor.State.Velocity.Z * .5));
        var error = Assert.Throws<ArithmeticException>(() => GravityDragSimulator.SimulateUntilGroundContact(request, a, height - 1));
        Assert.That(error!.Message, Does.Contain("release-origin").And.Contain("contact at 3.5 s").And.Contain("Integration step 4"));
    }

    [TestCase(0, 0)]
    [TestCase(3, -4)]
    public void ValidMultistepContactMatchesIndependentZeroGravitySolution(double vx, double vy)
    {
        var a = DragFixtures.Baseball;
        const double height = 1, clearance = 2;
        var initial = new FlightState(new(2, -3, height + clearance), new(vx, vy, -1));
        var request = new FlightRequest(initial, 0, 3, .005);
        var result = GravityDragSimulator.SimulateUntilGroundContact(request, a, height - a.BallRadius);
        var speed = Math.Sqrt(vx * vx + vy * vy + 1);
        var expectedTime = (Math.Exp(DragFixtures.K * speed * clearance) - 1) / (DragFixtures.K * speed);
        Assert.That(result.Samples.Count, Is.GreaterThan(2));
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
        Assert.That(result.FinalTime, Is.EqualTo(expectedTime).Within(1e-8));
        Assert.That(result.FinalState.Position.Z, Is.EqualTo(height));
        Assert.That(result.FinalState.Position.X, Is.EqualTo(2 + vx * clearance).Within(1e-8));
        Assert.That(result.FinalState.Position.Y, Is.EqualTo(-3 + vy * clearance).Within(1e-8));
        Assert.That(result.FinalState.Velocity.Z,
            Is.EqualTo(-1 / (1 + DragFixtures.K * speed * expectedTime)).Within(1e-8));
    }

    [TestCase(1)]
    [TestCase(1.5)]
    [TestCase(1048576)]
    public void OutwardBoundsPermitBorderlineRepresentedReleaseTime(double height)
    {
        var unit = Math.BitIncrement(height) - height;
        var request = new FlightRequest(new(new(0, 0, height + 14 * unit), new(0, 0, -3.6 * unit)), 0, 4, 1);
        var numerical = new DragNumericalEvaluation(request, DragFixtures.Baseball);
        var local = new TrajectorySample(3, new(new(0, 0, height + 2 * unit), request.InitialState.Velocity));
        var borderline = Math.BitDecrement(14 / 3.6);
        // A necessary conservative bound may permit an adjacent rounded timestamp;
        // passing it is not an assertion that the drag trajectory has made contact.
        Assert.DoesNotThrow(() => numerical.CheckContactReachability(local, borderline, height));
    }

    [TestCase(0)]
    [TestCase(-0d)]
    public void InitiallyTouchingZeroGravityBallStillCompletesImmediately(double verticalVelocity)
    {
        var a = new AerodynamicSettings(.145, 1, 1.225, .35);
        var request = new FlightRequest(new(new(0, 0, 1), new(0, 0, verticalVelocity)), 0, double.Epsilon, double.Epsilon);
        var result = GravityDragSimulator.SimulateUntilGroundContact(request, a, 0);
        Assert.That(result.Samples.Count, Is.EqualTo(1));
        Assert.That(result.FinalTime, Is.Zero);
        Assert.That(result.FinalState, Is.EqualTo(request.InitialState));
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
    }

    [TestCase(1, 0)]
    [TestCase(0, 1)]
    public void StationaryAirborneAndUpwardTouchingPathsInventNoContact(double clearance, double vz)
    {
        var a = new AerodynamicSettings(.145, 1, 1.225, .35);
        var request = new FlightRequest(new(new(0, 0, 1 + clearance), new(0, 0, vz)), 0, .1, .001);
        var result = GravityDragSimulator.SimulateUntilGroundContact(request, a, 0);
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
        Assert.That(TrajectoryMeasurements.GetGroundContact(result), Is.Null);
    }

    [Test]
    public void UnsafeReleaseBoundCannotHideBehindSafeLocalBound()
    {
        var initial = new FlightState(new(0, 0, 1), new(0, 0, -double.MaxValue));
        var numerical = new DragNumericalEvaluation(new(initial, 0, 1, .5), DragFixtures.Baseball);
        var local = new TrajectorySample(.5, new(new(0, 0, .1), new(0, 0, -1)));
        var error = Assert.Throws<ArithmeticException>(() => numerical.CheckContactReachability(local, 1, 0));
        Assert.That(error!.Message, Does.Contain("release-origin contact motion bound is not representable"));
    }
}
