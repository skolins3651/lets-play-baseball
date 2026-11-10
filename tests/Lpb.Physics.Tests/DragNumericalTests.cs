using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class DragNumericalTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void CoarseOneDimensionalFlightRejectsAlongEveryAxis(int axis)
    {
        var v = new[] { 0d, 0d, 0d }; v[axis] = 40;
        var request = new FlightRequest(new(default, new(v[0], v[1], v[2])), 0, 4, 4);
        Assert.That(Assert.Throws<ArithmeticException>(() => GravityDragSimulator.Simulate(request, DragFixtures.Baseball))!.Message,
            Does.Contain("consistency").And.Contain("Integration step 1"));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void RefinedFlightMatchesIndependentDragOnlySolution(int axis)
    {
        var v = new[] { 3d, -4d, 12d };
        var request = new FlightRequest(new(new(1, 2, 3), new(v[0], v[1], v[2])), 0, 4, .01);
        var result = GravityDragSimulator.Simulate(request, DragFixtures.Baseball);
        foreach (var sample in result.Samples)
        {
            var expectedPosition = axis + 1 + v[axis] / 13 * Math.Log(1 + DragFixtures.K * 13 * sample.Time) / DragFixtures.K;
            var expectedVelocity = v[axis] / (1 + DragFixtures.K * 13 * sample.Time);
            var values = DragFixtures.Components(sample.State);
            Assert.That(values[axis], Is.EqualTo(expectedPosition).Within(1e-8));
            Assert.That(values[axis + 3], Is.EqualTo(expectedVelocity).Within(1e-9));
        }
        Assert.That(result.ModelKind, Is.EqualTo(FlightModelKind.GravityAndDrag));
        Assert.Throws<ArgumentException>(() => GravityOnlyTrajectory.Evaluate(result, request, 0));
        Assert.Throws<ArgumentException>(() => GravityOnlyTrajectory.GetMaximumHeight(result, request));
    }

    [Test]
    public void ReviewHorizontalFixtureIsAccurateAtRefinedStep()
    {
        var r = GravityDragSimulator.Simulate(new(new(default, new(40, 0, 0)), 0, 4, .01), DragFixtures.Baseball);
        Assert.That(r.FinalState.Position.X, Is.EqualTo(111.043056557832).Within(1e-8));
        Assert.That(r.FinalState.Velocity.X, Is.EqualTo(20.045172928851).Within(1e-9));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void AgreementIncludesEveryComponent(int component)
    {
        var x = new double[6]; x[component] = 2e-8;
        var state = new FlightState(new(x[0], x[1], x[2]), new(x[3], x[4], x[5]));
        Assert.Throws<ArithmeticException>(() => DragNumericalEvaluation.CheckAgreement(default, state));
    }

    [TestCase(.04)]
    [TestCase(.02)]
    [TestCase(.01)]
    public void OrdinaryObliqueFlightsAreAcceptedAndUseExactIndexedTimes(double step)
    {
        var request = DragFixtures.Oblique(6, step);
        var r = GravityDragSimulator.Simulate(request, DragFixtures.Baseball);
        for (var i = 1; i < r.Samples.Count - 1; i++) Assert.That(r.Samples[i].Time, Is.EqualTo((double)i * step));
        Assert.That(r.FinalTime, Is.EqualTo(6));
        Assert.That(r.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DisabledDragDelegatesWithoutChangingSamplesOrApplyingNewGates(bool density)
    {
        var request = new FlightRequest(new(new(Math.ScaleB(1, 30), 0, 0), new(1e9, 0, 1e9)), 10, 2, 1);
        var a = new AerodynamicSettings(.145, .0366, density ? 0 : 1.225, density ? .35 : 0);
        var drag = GravityDragSimulator.Simulate(request, a);
        Assert.That(drag.Samples, Is.EqualTo(GravityOnlySimulator.Simulate(request).Samples));
        Assert.That(drag.ModelKind, Is.EqualTo(FlightModelKind.GravityAndDrag));
    }

    [Test]
    public void LargeStructuralConstantsRemainValidButMovingCoarseCoordinatesReject()
    {
        var state = new FlightState(new(double.MaxValue, Math.ScaleB(1, 30), 1), default);
        Assert.That(GravityDragSimulator.Simulate(new(state, 0, 1, 1), DragFixtures.Baseball).FinalState, Is.EqualTo(state));
        var moving = new FlightState(state.Position, new(0, 1, 0));
        Assert.That(Assert.Throws<ArithmeticException>(() => GravityDragSimulator.Simulate(new(moving, 0, 1, .01), DragFixtures.Baseball))!.Message,
            Does.Contain("position.Y precision"));
    }

    [Test]
    public void RequiredComparisonAndSubdivisionArithmeticCannotBeBypassed()
    {
        Assert.Throws<ArithmeticException>(() => DragNumericalEvaluation.UpperDifference(double.MaxValue, -double.MaxValue));
        Assert.Throws<ArithmeticException>(() => GravityDragSimulator.Simulate(new(default, 0, double.Epsilon, double.Epsilon), DragFixtures.Baseball));
        var request = new FlightRequest(default, 0, Math.BitIncrement(1d), 1);
        Assert.Throws<ArithmeticException>(() => GravityDragSimulator.Simulate(request, DragFixtures.Baseball));
    }

    [Test]
    public void SuperterminalDescentSlowsWithoutReversingAndTerminalMotionIsSupported()
    {
        var a = DragFixtures.Baseball;
        var request = new FlightRequest(new(new(0, 0, 100), new(0, 0, -60)), 9.80665, .1, .001);
        var result = GravityDragSimulator.Simulate(request, a);
        Assert.That(result.FinalState.Velocity.Z, Is.GreaterThan(-60).And.LessThan(0));
        Assert.That(GravityDragTrajectory.GetMaximumHeight(result, request, a), Is.EqualTo(result.Samples[0]));
        var terminal = -Math.Sqrt(9.80665 / DragFixtures.K);
        var equilibrium = new FlightRequest(new(default, new(0, 0, terminal)), 9.80665, 1, .01);
        var stationaryVelocity = GravityDragSimulator.Simulate(equilibrium, a).FinalState;
        Assert.That(stationaryVelocity.Velocity.Z, Is.EqualTo(terminal).Within(1e-12));
        Assert.That(stationaryVelocity.Position.Z, Is.EqualTo(terminal).Within(1e-12));
    }
}
