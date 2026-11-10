using System.Collections;
using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class DragTrajectoryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void EquivalentInputsAndRecordedStatesAreAccepted(bool contact)
    {
        var request = DragFixtures.Oblique();
        var a = DragFixtures.Baseball;
        var result = contact ? GravityDragSimulator.SimulateUntilGroundContact(request, a, 0) : GravityDragSimulator.Simulate(request, a);
        var equivalent = new AerodynamicSettings(a.BallMass, a.BallRadius, a.AirDensity, a.DragCoefficient);
        var copy = new FlightRequest(request.InitialState, request.GravityMagnitude, request.Duration, request.TimeStep);
        foreach (var sample in result.Samples)
            Assert.That(GravityDragTrajectory.Evaluate(result, copy, equivalent, sample.Time), Is.EqualTo(sample.State));
        var time = .12345;
        var anchor = result.Samples.Last(s => s.Time < time);
        Assert.That(GravityDragTrajectory.Evaluate(result, copy, equivalent, time),
            Is.EqualTo(new DragNumericalEvaluation(request, a).Step(anchor, time)));
        Assert.That(GravityDragTrajectory.GetMaximumHeight(result, copy, equivalent).Time, Is.LessThan(result.FinalTime));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public void EveryContextFieldMustMatchBeforeExactTimeReturns(int field)
    {
        var r = DragFixtures.Oblique(1);
        var result = GravityDragSimulator.Simulate(r, DragFixtures.Baseball);
        var wrong = new FlightRequest(field == 0 ? default : r.InitialState,
            field == 1 ? 10 : r.GravityMagnitude, field == 2 ? 2 : r.Duration, field == 3 ? .02 : r.TimeStep);
        var values = new[] { .145, .0366, 1.225, .35 };
        if (field >= 4) values[field - 4] *= 2;
        var aero = new AerodynamicSettings(values[0], values[1], values[2], values[3]);
        var expected = field < 4 ? "request" : "aerodynamics";
        Assert.That(Assert.Throws<ArgumentException>(() => GravityDragTrajectory.Evaluate(result, wrong, aero, 0))!.ParamName, Is.EqualTo(expected));
        Assert.That(Assert.Throws<ArgumentException>(() => GravityDragTrajectory.GetMaximumHeight(result, wrong, aero))!.ParamName, Is.EqualTo(expected));
    }

    [Test]
    public void CrossModelAndProvenanceFreeResultsRejectNumericalAnalysis()
    {
        var request = new FlightRequest(default, 0, 1, 1);
        foreach (var result in new[] { GravityOnlySimulator.Simulate(request), new SimulationResult([new(0, default), new(1, default)], 1) })
        {
            Assert.That(Assert.Throws<ArgumentException>(() => GravityDragTrajectory.Evaluate(result, request, DragFixtures.Baseball, 0))!.ParamName, Is.EqualTo("result"));
            Assert.Throws<ArgumentException>(() => GravityDragTrajectory.GetMaximumHeight(result, request, DragFixtures.Baseball));
            Assert.That(TrajectoryMeasurements.GetDisplacement(result), Is.EqualTo(default(Vector3d)));
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DisabledDragRetainsIdentityContextAndGravityContactGuarantees(bool density)
    {
        var request = new FlightRequest(new(new(0, 0, 1), new(6, 8, 10)), 10, 6, .3);
        var a = new AerodynamicSettings(.145, .0366, density ? 0 : 1.225, density ? .35 : 0);
        var drag = GravityDragSimulator.SimulateUntilGroundContact(request, a, 0);
        var vacuum = GravityOnlySimulator.SimulateUntilGroundContact(request, new(0, a.BallRadius));
        Assert.That(drag.Samples, Is.EqualTo(vacuum.Samples));
        Assert.That(drag.ModelKind, Is.EqualTo(FlightModelKind.GravityAndDrag));
        Assert.That(GravityDragTrajectory.Evaluate(drag, request, a, .123), Is.EqualTo(GravityOnlyTrajectory.Evaluate(vacuum, request, .123)));
        Assert.That(GravityDragTrajectory.GetMaximumHeight(drag, request, a), Is.EqualTo(GravityOnlyTrajectory.GetMaximumHeight(vacuum, request)));
        Assert.Throws<ArgumentException>(() => GravityOnlyTrajectory.Evaluate(drag, request, 0));
        Assert.Throws<ArgumentException>(() => GravityOnlyTrajectory.GetMaximumHeight(drag, request));
        // Even mathematically identical disabled configurations must match all supplied fields.
        Assert.Throws<ArgumentException>(() => GravityDragTrajectory.Evaluate(drag, request, new(.29, a.BallRadius, a.AirDensity, a.DragCoefficient), 0));
    }

    [TestCase(-1)]
    [TestCase(2)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidTimesReject(double time)
    {
        var request = DragFixtures.Oblique(1);
        var result = GravityDragSimulator.Simulate(request, DragFixtures.Baseball);
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => GravityDragTrajectory.Evaluate(result, request, DragFixtures.Baseball, time))!.ParamName, Is.EqualTo("time"));
    }

    [Test]
    public void OptionalInteriorFailureLeavesSuccessfulResultIntact()
    {
        var request = DragFixtures.Oblique(1);
        var result = GravityDragSimulator.Simulate(request, DragFixtures.Baseball);
        var before = result.Samples.ToArray();
        Assert.Throws<ArithmeticException>(() => GravityDragTrajectory.Evaluate(result, request, DragFixtures.Baseball, Math.BitIncrement(.01)));
        Assert.That(result.Samples, Is.EqualTo(before));
        Assert.That(result.FinalTime, Is.EqualTo(1));
    }

    [TestCase(-2, 10, 1, 0)]
    [TestCase(0, 0, 1, 0)]
    [TestCase(-2, 0, 1, 0)]
    [TestCase(2, 0, 1, 1)]
    [TestCase(10, 10, .1, .1)]
    public void EndpointAndConstantHeightMaximaUseActualInterval(double vz, double g, double duration, double expectedTime)
    {
        var request = new FlightRequest(new(new(0, 0, 10), new(6, 8, vz)), g, duration, .001);
        var result = GravityDragSimulator.Simulate(request, DragFixtures.Baseball);
        Assert.That(GravityDragTrajectory.GetMaximumHeight(result, request, DragFixtures.Baseball).Time, Is.EqualTo(expectedTime));
        Assert.That(TrajectoryMeasurements.GetGroundContact(result), Is.Null);
    }

    [Test]
    public void InteriorVerticalApexMatchesIndependentAnalyticalSolution()
    {
        const double v = 5, g = 10;
        var request = new FlightRequest(new(new(0, 0, 1), new(0, 0, v)), g, 2, .001);
        var result = GravityDragSimulator.Simulate(request, DragFixtures.Baseball);
        var apex = GravityDragTrajectory.GetMaximumHeight(result, request, DragFixtures.Baseball);
        var k = DragFixtures.K;
        Assert.That(apex.Time, Is.EqualTo(Math.Atan(v * Math.Sqrt(k / g)) / Math.Sqrt(g * k)).Within(1e-8));
        Assert.That(apex.State.Position.Z, Is.EqualTo(1 + Math.Log(1 + k * v * v / g) / (2 * k)).Within(1e-8));
        Assert.That(Math.Abs(apex.State.Velocity.Z), Is.LessThanOrEqualTo(1e-8));
    }

    [Test]
    public void AscendingPlateauUsesEarliestEqualRecordedHeightWithoutApproximateTies()
    {
        var request = new FlightRequest(new(new(0, 0, 1.9999999999999996), new(0, 0, Math.ScaleB(1, -52))), 0, 4, 1);
        var result = GravityDragSimulator.Simulate(request, DragFixtures.Baseball);
        Assert.That(result.Samples.Select(s => s.State.Position.Z), Is.EqualTo(new[] { 1.9999999999999996, 1.9999999999999998, 2d, 2d, 2d }));
        Assert.That(GravityDragTrajectory.GetMaximumHeight(result, request, DragFixtures.Baseball), Is.EqualTo(result.Samples[2]));
        var distinct = new FlightRequest(new(new(0, 0, 2), new(0, 0, Math.ScaleB(1, -51))), 0, 3, 1);
        var increasing = GravityDragSimulator.Simulate(distinct, DragFixtures.Baseball);
        Assert.That(GravityDragTrajectory.GetMaximumHeight(increasing, distinct, DragFixtures.Baseball), Is.EqualTo(increasing.Samples[^1]));
    }

    [Test]
    public void DragResultsOwnTheirSnapshotAndDoNotLeakBackingArray()
    {
        var request = new FlightRequest(default, 0, 1, 1);
        var source = new[] { new TrajectorySample(0, default), new TrajectorySample(1, default) };
        var provenance = new FlightProvenance(request, null, DragFixtures.Baseball);
        var result = new SimulationResult(source, provenance, CompletionReason.DurationReached);
        source[1] = new(2, default);
        Assert.That(result.FinalTime, Is.EqualTo(1));
        Assert.That(((ICollection)result.Samples).SyncRoot, Is.SameAs(result.Samples));
        Assert.Throws<NotSupportedException>(() => ((IList)result.Samples)[0] = default(TrajectorySample));
        Assert.That(provenance.Aerodynamics!.BallMass, Is.EqualTo(.145));
        Assert.That(provenance.NumericalPolicy, Does.Contain("drag-full-state-v1"));
        Assert.Throws<ArgumentException>(() => new FlightProvenance(request, new(0, .1), DragFixtures.Baseball));
    }

    [Test]
    public void NullArgumentsRemainInvocationFailures()
    {
        var r = new FlightRequest(default, 0, 1, 1);
        var a = DragFixtures.Baseball;
        var result = GravityDragSimulator.Simulate(r, a);
        Assert.Throws<ArgumentNullException>(() => GravityDragSimulator.Simulate(null!, a));
        Assert.Throws<ArgumentNullException>(() => GravityDragSimulator.Simulate(r, null!));
        Assert.Throws<ArgumentNullException>(() => GravityDragSimulator.SimulateUntilGroundContact(null!, a, 0));
        Assert.Throws<ArgumentNullException>(() => GravityDragSimulator.SimulateUntilGroundContact(r, null!, 0));
        Assert.Throws<ArgumentNullException>(() => GravityDragTrajectory.Evaluate(null!, r, a, 0));
        Assert.Throws<ArgumentNullException>(() => GravityDragTrajectory.Evaluate(result, null!, a, 0));
        Assert.Throws<ArgumentNullException>(() => GravityDragTrajectory.Evaluate(result, r, null!, 0));
        Assert.Throws<ArgumentNullException>(() => GravityDragTrajectory.GetMaximumHeight(null!, r, a));
        Assert.Throws<ArgumentNullException>(() => GravityDragTrajectory.GetMaximumHeight(result, null!, a));
        Assert.Throws<ArgumentNullException>(() => GravityDragTrajectory.GetMaximumHeight(result, r, null!));
    }
}
