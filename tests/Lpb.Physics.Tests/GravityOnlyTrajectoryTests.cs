using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class GravityOnlyTrajectoryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void BothProductionEntriesAcceptMatchingAndEquivalentRequests(bool contactAware)
    {
        var request = new FlightRequest(FlightFixtures.State(20, 10, 1, -2, 6, 8), 10, 1, .3);
        var result = contactAware ? GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane)
            : GravityOnlySimulator.Simulate(request);
        var equivalent = new FlightRequest(request.InitialState, 10, 1, .3);
        foreach (var context in new[] { request, equivalent })
        {
            foreach (var sample in result.Samples)
                Assert.That(GravityOnlyTrajectory.Evaluate(result, context, sample.Time), Is.EqualTo(sample.State));
            FlightFixtures.AssertAnalytical(GravityOnlyTrajectory.Evaluate(result, context, .2), request.InitialState, 10, .2, 1e-9);
            FlightFixtures.AssertAnalytical(GravityOnlyTrajectory.Evaluate(result, context, .8), request.InitialState, 10, .8, 1e-9);
            Assert.That(GravityOnlyTrajectory.GetMaximumHeight(result, context), Is.EqualTo(result.Samples[^1]));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void EveryMismatchedContextFieldIsRejected(int changed)
    {
        var request = new FlightRequest(FlightFixtures.State(1, 10), 10, 3, .3);
        var wrong = new FlightRequest(changed == 0 ? FlightFixtures.State(2, 10) : request.InitialState,
            changed == 1 ? 9 : 10, changed == 2 ? 4 : 3, changed == 3 ? .4 : .3);
        foreach (var result in new[] { GravityOnlySimulator.Simulate(request),
                     GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane) })
        {
            Assert.That(Assert.Throws<ArgumentException>(() => GravityOnlyTrajectory.Evaluate(result, wrong, 0))!.ParamName, Is.EqualTo("request"));
            Assert.Throws<ArgumentException>(() => GravityOnlyTrajectory.GetMaximumHeight(result, wrong));
        }
    }

    [Test]
    public void ProvenanceFreeResultPermitsSampleMeasurementsButNotNumericalAnalysis()
    {
        var result = new SimulationResult([new TrajectorySample(0, default), new TrajectorySample(1, default)], 1);
        var request = new FlightRequest(default, 0, 1, 1);
        Assert.That(TrajectoryMeasurements.GetDisplacement(result), Is.EqualTo(default(Vector3d)));
        Assert.That(Assert.Throws<ArgumentException>(() => GravityOnlyTrajectory.Evaluate(result, request, 0))!.ParamName, Is.EqualTo("result"));
        Assert.Throws<ArgumentException>(() => GravityOnlyTrajectory.GetMaximumHeight(result, request));
    }

    [TestCase(.25)]
    [TestCase(.3)]
    [TestCase(3)]
    public void ContinuousApexIsIndependentOfStoredSampleSpacing(double dt)
    {
        var request = new FlightRequest(FlightFixtures.State(0, 10, vx: 6, vy: 8), 10, 3, dt);
        foreach (var result in new[] { GravityOnlySimulator.Simulate(request),
                     GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane) })
        {
            var apex = GravityOnlyTrajectory.GetMaximumHeight(result, request);
            Assert.That(apex.Time, Is.EqualTo(1).Within(1e-10));
            Assert.That(apex.State.Position.Z, Is.EqualTo(5).Within(1e-9));
            Assert.That(Math.Abs(apex.State.Velocity.Z), Is.LessThanOrEqualTo(1e-9));
        }
    }

    [TestCase(10, -2, 10, 0, 10)]
    [TestCase(10, 0, 0, 0, 10)]
    [TestCase(10, -2, 0, 0, 10)]
    [TestCase(10, 2, 0, 1, 12)]
    [TestCase(10, 10, 10, .5, 13.75)]
    [TestCase(10, 10, 10, 1, 15)]
    public void EndpointAndConstantHeightMaximaUseActualInterval(double z, double vz, double g, double expectedTime, double expectedZ)
    {
        var duration = expectedTime == .5 ? .5 : 1;
        var request = new FlightRequest(FlightFixtures.State(z, vz, vx: 2), g, duration, .3);
        var result = GravityOnlySimulator.Simulate(request);
        var apex = GravityOnlyTrajectory.GetMaximumHeight(result, request);
        Assert.That(apex.Time, Is.EqualTo(expectedTime));
        Assert.That(apex.State.Position.Z, Is.EqualTo(expectedZ).Within(1e-9));
    }

    [Test]
    public void StoredTerminalStateIsExactAndInteriorStateIsUnprojected()
    {
        var request = new FlightRequest(FlightFixtures.State(1, 0), 10, 2, 1);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(GravityOnlyTrajectory.Evaluate(result, request, result.FinalTime), Is.EqualTo(result.FinalState));
        var time = Math.BitDecrement(result.FinalTime);
        var expected = Rk4Integrator.Step(0, time, request.InitialState, new GravityDynamics(10).Evaluate);
        Assert.That(expected.Position.Z, Is.Not.Zero);
        Assert.That(GravityOnlyTrajectory.Evaluate(result, request, time), Is.EqualTo(expected));
        Assert.Throws<ArgumentOutOfRangeException>(() => GravityOnlyTrajectory.Evaluate(result, request, Math.BitIncrement(result.FinalTime)));
    }

    [TestCase(-1)]
    [TestCase(2)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void EvaluationRejectsInvalidTimes(double time)
    {
        var request = new FlightRequest(FlightFixtures.State(1, 0), 0, 1, .25);
        var result = GravityOnlySimulator.Simulate(request);
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => GravityOnlyTrajectory.Evaluate(result, request, time))!.ParamName,
            Is.EqualTo("time"));
    }

    [Test]
    public void InteriorRk4RepresentabilityFailureIsPreserved()
    {
        var request = new FlightRequest(FlightFixtures.State(1, 0), 0, 2, 1);
        var result = GravityOnlySimulator.Simulate(request);
        Assert.Throws<ArithmeticException>(() => GravityOnlyTrajectory.Evaluate(result, request, Math.BitIncrement(1d)));
    }

    [Test]
    public void NontrivialApexMatchesIndependentStandardGravityReference()
    {
        const double gravity = 9.80665;
        var request = new FlightRequest(FlightFixtures.State(11, 7), gravity, 2, .4);
        var result = GravityOnlySimulator.Simulate(request);
        var apex = GravityOnlyTrajectory.GetMaximumHeight(result, request);
        Assert.That(apex.Time, Is.EqualTo(7 / gravity).Within(1e-10));
        Assert.That(apex.State.Position.Z, Is.EqualTo(11 + 49 / (2 * gravity)).Within(1e-9));
    }

    [Test]
    public void NullAnalysisArgumentsAreRejected()
    {
        var request = new FlightRequest(default, 0, 1, 1);
        var result = GravityOnlySimulator.Simulate(request);
        Assert.Throws<ArgumentNullException>(() => GravityOnlyTrajectory.Evaluate(null!, request, 0));
        Assert.Throws<ArgumentNullException>(() => GravityOnlyTrajectory.GetMaximumHeight(null!, request));
        Assert.Throws<ArgumentNullException>(() => GravityOnlyTrajectory.Evaluate(result, null!, 0));
        Assert.Throws<ArgumentNullException>(() => GravityOnlyTrajectory.GetMaximumHeight(result, null!));
    }

    [TestCase(3)]
    [TestCase(4)]
    [TestCase(8)]
    public void ReviewAscendingPlateauReportsFirstEqualStoredMaximum(double duration)
    {
        var request = new FlightRequest(FlightFixtures.State(1.9999999999999996, Math.ScaleB(1, -52)), 0, duration, 1);
        var result = GravityOnlySimulator.Simulate(request);
        Assert.That(result.Samples.Take(4).Select(s => s.State.Position.Z),
            Is.EqualTo(new[] { 1.9999999999999996, 1.9999999999999998, 2d, 2d }));
        var maximum = GravityOnlyTrajectory.GetMaximumHeight(result, request);
        Assert.That(maximum, Is.EqualTo(result.Samples[2]));
        Assert.That(maximum.Time, Is.EqualTo(2));
    }

    [Test]
    public void AdjacentDistinctHeightsAreNotTreatedAsEqualMaxima()
    {
        var request = new FlightRequest(FlightFixtures.State(2, Math.ScaleB(1, -51)), 0, 3, 1);
        var result = GravityOnlySimulator.Simulate(request);
        Assert.That(result.Samples[2].State.Position.Z, Is.LessThan(result.FinalState.Position.Z));
        Assert.That(GravityOnlyTrajectory.GetMaximumHeight(result, request), Is.EqualTo(result.Samples[^1]));
    }
}
