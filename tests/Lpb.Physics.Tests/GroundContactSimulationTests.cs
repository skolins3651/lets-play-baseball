using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class GroundContactSimulationTests
{
    [TestCase(20, 0, 0, 0, 10, 0.25)]
    [TestCase(20, 0, 10, 0, 10, 0.3)]
    [TestCase(0, 10, 10, 0, 10, 0.3)]
    [TestCase(0, 10, 6, 8, 10, 3)]
    [TestCase(20, -3, -6, 8, 9.80665, 0.7)]
    [TestCase(1, 0, 2, -3, 10, 1)]
    public void LandingsMatchIndependentAnalyticalTimeAndState(double release, double vz, double vx, double vy, double g, double dt)
    {
        var contact = new GroundContactSettings(2, 0.125);
        var initial = FlightFixtures.State(2.125 + release, vz, 3, -4, vx, vy);
        var request = new FlightRequest(initial, g, 10, dt);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, contact);
        var discriminant = Math.Sqrt(vz * vz + 2 * g * release);
        var expected = vz < 0 ? 2 * release / (discriminant - vz) : (vz + discriminant) / g;
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
        Assert.That(result.FinalTime, Is.EqualTo(expected).Within(1e-10));
        FlightFixtures.AssertAnalytical(result.FinalState, initial, g, expected, 1e-9);
        Assert.That(result.FinalState.Position.Z, Is.EqualTo(contact.ContactHeight));
        Assert.That(result.FinalState.Velocity.Z, Is.LessThan(0));
        Assert.That(result.Samples[0], Is.EqualTo(new TrajectorySample(0, initial)));
        for (var i = 1; i < result.Samples.Count; i++)
        {
            Assert.That(result.Samples[i].Time, Is.GreaterThan(result.Samples[i - 1].Time));
            Assert.That(result.Samples[i].State.Position.Z, Is.GreaterThanOrEqualTo(contact.ContactHeight));
        }
        var repeated = GravityOnlySimulator.SimulateUntilGroundContact(request, contact);
        Assert.That(repeated.Samples, Is.EqualTo(result.Samples));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ImmediateContactUsesNoOrdinaryWorkAndCarriesProvenance(double vz)
    {
        var request = new FlightRequest(FlightFixtures.State(0, vz), double.MaxValue, double.Epsilon, double.Epsilon);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(result.Samples.Count, Is.EqualTo(1));
        Assert.That(result.FinalTime, Is.Zero);
        Assert.That(result.FinalState, Is.EqualTo(request.InitialState));
        Assert.That(GravityOnlyTrajectory.Evaluate(result, request, 0), Is.EqualTo(request.InitialState));
        Assert.That(GravityOnlyTrajectory.GetMaximumHeight(result, request), Is.EqualTo(result.Samples[0]));
        Assert.That(TrajectoryMeasurements.GetGroundContact(result), Is.EqualTo(result.Samples[0]));
    }

    [Test]
    public void AnyInitialPenetrationIsRejectedWithoutTolerance()
    {
        var request = new FlightRequest(FlightFixtures.State(-double.Epsilon, 1), 10, 1, 1);
        Assert.That(Assert.Throws<ArgumentException>(() => GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane))!.ParamName,
            Is.EqualTo("request"));
    }

    [TestCase(2, 2)]
    [TestCase(2, 3)]
    [TestCase(3, 3)]
    public void InitialUpwardLaunchFindsLaterReturnEvenWithinOneStep(double duration, double dt)
    {
        var request = new FlightRequest(FlightFixtures.State(0, 10), 10, duration, dt);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(result.FinalTime, Is.EqualTo(2).Within(1e-10));
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
        Assert.That(result.Samples.Count, Is.EqualTo(2));
    }

    [TestCase(-1, CompletionReason.GroundContact)]
    [TestCase(0, CompletionReason.GroundContact)]
    [TestCase(1, CompletionReason.DurationReached)]
    public void HorizonBoundaryUsesEvidenceRatherThanHeightTolerance(int offset, CompletionReason expected)
    {
        var initial = FlightFixtures.State(1 + offset * Math.ScaleB(1, -32), -1);
        var request = new FlightRequest(initial, 0, 1, 2);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(FlightFixtures.ZeroPlane.ContactHeight, Is.Zero);
        Assert.That(result.CompletionReason, Is.EqualTo(expected));
        if (offset < 0)
            Assert.That(result.FinalTime, Is.LessThan(1));
        else
            Assert.That(result.FinalTime, Is.EqualTo(1));
        if (offset > 0)
        {
            Assert.That(result.FinalState.Position.Z, Is.GreaterThan(0).And.LessThan(1e-9));
            Assert.That(TrajectoryMeasurements.GetGroundContact(result), Is.Null);
        }
    }

    [TestCase(1, -2, 0.5, CompletionReason.GroundContact)]
    [TestCase(0, 1, 2, CompletionReason.DurationReached)]
    [TestCase(1, 0, 2, CompletionReason.DurationReached)]
    [TestCase(1, 1, 2, CompletionReason.DurationReached)]
    [TestCase(2.3283064365386963e-10, 0, 2, CompletionReason.DurationReached)]
    public void ZeroGravityAndNearPlaneMotion(double z, double vz, double finalTime, CompletionReason reason)
    {
        var request = new FlightRequest(FlightFixtures.State(z, vz), 0, 2, 0.75);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(result.CompletionReason, Is.EqualTo(reason));
        Assert.That(result.FinalTime, Is.EqualTo(finalTime).Within(1e-10));
    }

    [Test]
    public void AirborneGravityFlightCompletesExactShortenedHorizon()
    {
        var request = new FlightRequest(FlightFixtures.State(20, 10), 10, 1, 0.3);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
        Assert.That(result.FinalTime, Is.EqualTo(1));
        Assert.That(result.Samples.Select(s => s.Time), Is.EqualTo(new[] { 0d, .3, .6, .8999999999999999, 1 }));
        Assert.That(TrajectoryMeasurements.GetGroundContact(result), Is.Null);
        FlightFixtures.AssertAnalytical(result.FinalState, request.InitialState, 10, 1, 1e-9);
    }

    [Test]
    public void OverBudgetHorizonMayLandEarly()
    {
        var request = new FlightRequest(FlightFixtures.State(200, -1), 0, 1_000_000, 1);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(result.FinalTime, Is.EqualTo(200));
        Assert.That(result.Samples.Count, Is.EqualTo(201));
        Assert.Throws<InvalidOperationException>(() => GravityOnlySimulator.Simulate(request));
    }

    [Test]
    public void ContactInLastPermittedStepSucceeds()
    {
        var request = new FlightRequest(FlightFixtures.State(99999.5, -1), 0, 1_000_000, 1);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(result.FinalTime, Is.EqualTo(99999.5));
        Assert.That(result.Samples.Count, Is.EqualTo(100001));
    }

    [TestCase(100000.5, -1)]
    [TestCase(1, 0)]
    public void BudgetExhaustionCannotJumpToDurationOrLaterContact(double z, double vz)
    {
        SimulationResult? result = null;
        var request = new FlightRequest(FlightFixtures.State(z, vz), 0, 1_000_000, 1);
        var error = Assert.Throws<InvalidOperationException>(() =>
            result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane));
        Assert.That(result, Is.Null);
        Assert.That(error!.Message, Does.Contain("100000").And.Contain("duration"));
    }

    [Test]
    public void NullArgumentsAreInvocationFailures()
    {
        Assert.That(Assert.Throws<ArgumentNullException>(() => GravityOnlySimulator.SimulateUntilGroundContact(null!, FlightFixtures.ZeroPlane))!.ParamName,
            Is.EqualTo("request"));
        Assert.That(Assert.Throws<ArgumentNullException>(() => GravityOnlySimulator.SimulateUntilGroundContact(new FlightRequest(default, 0, 1, 1), null!))!.ParamName,
            Is.EqualTo("contact"));
    }

    [Test]
    public void IntegrationFailureNeverBecomesSuccessfulContact()
    {
        SimulationResult? result = null;
        var request = new FlightRequest(FlightFixtures.State(1, -1), double.MaxValue, 1, 1);
        Assert.Throws<ArithmeticException>(() => result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane));
        Assert.That(result, Is.Null);
    }

    [Test]
    public void UnresolvableContactRefinementPropagatesWithoutPartialSuccess()
    {
        // Both ordinary steps are safe; a spatially accurate contact would require
        // RK4 advancement between adjacent timestamps after t=1.
        var request = new FlightRequest(FlightFixtures.State(1e12 + .0001, -1e12), 0, 2, 1);
        SimulationResult? result = null;
        var error = Assert.Throws<ArithmeticException>(() =>
            result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane));
        Assert.That(result, Is.Null);
        Assert.That(error!.Message, Does.Contain("Integration step 2").And.Contain("midpoint"));
    }

    [Test]
    public void HighHorizontalSpeedRequiresSpatiallyAccurateLocalization()
    {
        var request = new FlightRequest(FlightFixtures.State(1, 0, vx: 1e6), 10, 2, 1);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        var expectedTime = Math.Sqrt(.2);
        Assert.That(result.FinalTime, Is.EqualTo(expectedTime).Within(1e-10));
        Assert.That(result.FinalState.Position.X, Is.EqualTo(1e6 * expectedTime).Within(1e-9));
    }
}

internal static class FlightFixtures
{
    internal static GroundContactSettings ZeroPlane => new(-0.05, 0.05);
    internal static FlightState State(double z, double vz, double x = 0, double y = 0, double vx = 0, double vy = 0) =>
        new(new Vector3d(x, y, z), new Vector3d(vx, vy, vz));

    internal static void AssertAnalytical(FlightState actual, FlightState initial, double g, double time, double tolerance)
    {
        var expected = new[] { initial.Position.X + initial.Velocity.X * time,
            initial.Position.Y + initial.Velocity.Y * time, initial.Position.Z + initial.Velocity.Z * time - .5 * g * time * time,
            initial.Velocity.X, initial.Velocity.Y, initial.Velocity.Z - g * time };
        var values = new[] { actual.Position.X, actual.Position.Y, actual.Position.Z, actual.Velocity.X, actual.Velocity.Y, actual.Velocity.Z };
        for (var i = 0; i < values.Length; i++)
            Assert.That(values[i], Is.EqualTo(expected[i]).Within(tolerance), $"Component {i} at {time:R}");
    }
}
