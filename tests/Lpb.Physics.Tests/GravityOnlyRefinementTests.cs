using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class GravityOnlyRefinementTests
{
    private static TrajectorySample Sample(double t, double z, double vz = -1, double vx = 0) =>
        new(t, FlightFixtures.State(z, vz, vx: vx));

    [TestCase(false)]
    [TestCase(true)]
    public void AllUncertaintyBoundsMustPassEvenAfterTimeTolerance(bool velocityBound)
    {
        var calls = 0;
        var speed = velocityBound ? 0 : 1e8;
        var g = velocityBound ? 1e6 : 0;
        var lower = Sample(0, 1e-12, vx: speed);
        var upper = Sample(1e-11, -1e-12, vx: speed);
        var result = GravityOnlyRefinement.FindContact(lower, upper, 0, g, _ =>
        {
            calls++;
            return upper.State;
        });
        Assert.That(calls, Is.GreaterThan(0).And.LessThanOrEqualTo(128));
        Assert.That((velocityBound ? g : speed) * result.Time, Is.LessThanOrEqualTo(1e-9));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SearchesHaveHardEvaluationCaps(bool apex)
    {
        var calls = 0;
        var lower = Sample(0, 1, 1);
        var upper = Sample(1, -1);
        FlightState Evaluate(double _) { calls++; return upper.State; }
        var error = Assert.Throws<ArithmeticException>(() =>
        {
            if (apex) GravityOnlyRefinement.FindApex(lower, upper, 0, Evaluate);
            else GravityOnlyRefinement.FindContact(lower, upper, 0, 0, Evaluate);
        });
        Assert.That(calls, Is.EqualTo(128));
        Assert.That(error!.Message, Does.Contain("128 evaluations"));
    }

    [Test]
    public void ExcessiveHeightResidualPreventsSuccessDespiteSmallTimeAndStateBounds()
    {
        var lower = Sample(0, 1);
        var upper = Sample(1e-11, -1);
        Assert.Throws<ArithmeticException>(() => GravityOnlyRefinement.FindContact(lower, upper, 0, 0, _ => upper.State));
    }

    [TestCase(2, double.MaxValue)]
    [TestCase(0.1, double.Epsilon)]
    public void UnrepresentableUncertaintyBoundsFail(double width, double vx)
    {
        Assert.Throws<ArithmeticException>(() => GravityOnlyRefinement.FindContact(
            Sample(0, 1, vx: vx), Sample(width, -1, vx: vx), 0, 0, _ => throw new AssertionException("Unexpected evaluation")));
    }

    [Test]
    public void UnresolvableAdjacentTimestampsFail()
    {
        var error = Assert.Throws<ArithmeticException>(() => GravityOnlyRefinement.FindContact(
            Sample(1, 1), Sample(Math.BitIncrement(1d), -1), 0, 0, _ => throw new AssertionException("Unexpected evaluation")));
        Assert.That(error!.Message, Does.Contain("interior timestamp"));
    }

    [Test]
    public void AdjacentTimestampsMayUseAlreadyEvaluatedStateWhenEveryBoundPasses()
    {
        var result = GravityOnlyRefinement.FindContact(Sample(1, 1e-20), Sample(Math.BitIncrement(1d), -1e-20), 0, 0,
            _ => throw new AssertionException("No further evaluation is needed"));
        Assert.That(result.Time, Is.EqualTo(Math.BitIncrement(1d)));
        Assert.That(result.State.Position.Z, Is.Zero);
    }

    [Test]
    public void ExactRootRequiresNoFurtherEvaluation()
    {
        var upper = Sample(1, 0);
        Assert.That(GravityOnlyRefinement.FindContact(Sample(0, 1), upper, 0, 0,
            _ => throw new AssertionException("Unexpected evaluation")), Is.EqualTo(upper));
    }

    [TestCase(0, 0, -1)]
    [TestCase(1, 0, 0)]
    [TestCase(1, 1, -1)]
    public void InvalidContactBracketsCannotBeAcceptedAsExactRoots(double lowerZ, double upperZ, double upperVz)
    {
        Assert.Throws<ArithmeticException>(() => GravityOnlyRefinement.FindContact(
            Sample(0, lowerZ), Sample(1, upperZ, upperVz), 0, 0, _ => default));
    }

    [Test]
    public void LostDescendingBracketFailsExplicitly()
    {
        Assert.Throws<ArithmeticException>(() => GravityOnlyRefinement.FindContact(
            Sample(0, 1), Sample(1, -1), 0, 1, _ => FlightFixtures.State(-1, 1)));
    }

    [Test]
    public void ApexRequiresVelocitySignBracket()
    {
        Assert.Throws<ArithmeticException>(() => GravityOnlyRefinement.FindApex(Sample(0, 1), Sample(1, 1), 1, _ => default));
    }

    [Test]
    public void UnresolvableLaunchExcursionFailsRatherThanReportingInitialContact()
    {
        Assert.Throws<ArithmeticException>(() => GravityOnlyRefinement.FindContact(
            Sample(0, 0, 1), Sample(1, -1), 0, 1, _ => FlightFixtures.State(0, 0)));
    }

    [Test]
    public void UnsafeRequiredRk4TrialIsNotReplacedWithInterpolation()
    {
        var lower = Sample(1, 1);
        var upper = Sample(Math.BitIncrement(Math.BitIncrement(1d)), -1);
        var dynamics = new GravityDynamics(0);
        var error = Assert.Throws<ArithmeticException>(() => GravityOnlyRefinement.FindContact(lower, upper, 0, 0,
            t => Rk4Integrator.Step(lower.Time, t, lower.State, dynamics.Evaluate)));
        Assert.That(error!.Message, Does.Contain("midpoint"));
    }

    [Test]
    public void ProjectionChangesOnlyTerminalZAndStaysWithinResidualLimit()
    {
        var request = new FlightRequest(FlightFixtures.State(1, 0, 3, -2, 6, 8), 10, 2, 1);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        var raw = Rk4Integrator.Step(0, result.FinalTime, request.InitialState, new GravityDynamics(10).Evaluate);
        Assert.That(Math.Abs(raw.Position.Z), Is.GreaterThan(0).And.LessThanOrEqualTo(1e-9));
        Assert.That(result.FinalState.Position.Z, Is.Zero);
        Assert.That(result.FinalState.Position.X, Is.EqualTo(raw.Position.X));
        Assert.That(result.FinalState.Position.Y, Is.EqualTo(raw.Position.Y));
        Assert.That(result.FinalState.Velocity, Is.EqualTo(raw.Velocity));
        Assert.That(result.FinalTime, Is.EqualTo(Math.Sqrt(.2)).Within(1e-10));
    }

    [Test]
    public void ReviewContactPositionRoundingReproductionFailsExplicitly()
    {
        var request = new FlightRequest(FlightFixtures.State(1, 0, Math.ScaleB(1, 30), 0,
            Math.ScaleB(1, -23) / Math.Sqrt(.2), 0), 10, 2, 1);
        SimulationResult? result = null;
        var error = Assert.Throws<ArithmeticException>(() =>
            result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane));
        Assert.That(result, Is.Null);
        Assert.That(error!.Message, Does.Contain("Integration step 1").And.Contain("position").And.Contain("precision"));
    }

    [TestCase(15, true)]
    [TestCase(16, false)]
    [TestCase(22, false)]
    [TestCase(23, false)]
    public void NonexactCertificateReservesRoomForCoordinateEvaluationRounding(int exponent, bool resolvable)
    {
        // A manufactured nonexact bracket isolates the rounding floor. Endpoints agree in X;
        // that agreement alone cannot certify other RK4 evaluations from the original anchor.
        var lower = new TrajectorySample(0, FlightFixtures.State(1e-12, -1, Math.ScaleB(1, exponent), vx: .001));
        var upper = new TrajectorySample(1e-11, FlightFixtures.State(-1e-12, -1, Math.ScaleB(1, exponent), vx: .001));
        TrajectorySample Refine() => GravityOnlyRefinement.FindContact(lower, upper, 0, 0, _ => upper.State);
        if (resolvable)
            Assert.That(Refine().State.Position.Z, Is.Zero);
        else
            Assert.Throws<ArithmeticException>(() => Refine());
    }

    [TestCase(-200)]
    [TestCase(0)]
    [TestCase(200)]
    public void BaseballScaleCoordinatesRetainAccurateContact(double x)
    {
        var request = new FlightRequest(FlightFixtures.State(1, 0, x, -100, 40, 30), 10, 2, 1);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        var expectedTime = Math.Sqrt(.2);
        Assert.That(result.FinalTime, Is.EqualTo(expectedTime).Within(1e-10));
        Assert.That(result.FinalState.Position.X, Is.EqualTo(x + 40 * expectedTime).Within(1e-9));
        Assert.That(result.FinalState.Position.Y, Is.EqualTo(-100 + 30 * expectedTime).Within(1e-9));
    }

    [Test]
    public void ReviewHighSpeedVelocityRoundingReproductionFailsExplicitly()
    {
        var request = new FlightRequest(new FlightState(new Vector3d(0, 0, 1), new Vector3d(0, 0, -1e9)),
            59.604644775390625, 2e-9, 2e-9);
        SimulationResult? result = null;
        var error = Assert.Throws<ArithmeticException>(() => result =
            GravityOnlySimulator.SimulateUntilGroundContact(request, new GroundContactSettings(-.05, .05)));
        Assert.That(result, Is.Null);
        Assert.That(error!.Message, Does.Contain("Integration step 1").And.Contain("velocity").And.Contain("precision"));
    }

    [Test]
    public void AuditApexHeightRoundingRequiresNumericalRejection()
    {
        const double initialHeight = 1073741824;
        var request = new FlightRequest(FlightFixtures.State(initialHeight, .007603029409634734),
            242.45617275970022, 7.839591505441008e-05, 7.839591505441008e-05);
        var result = GravityOnlySimulator.Simulate(request);
        // Inside the old accepted apex bracket [3.135836415265976e-5, 3.13583664890401e-5],
        // rounded height jumps one ULP although the speed-times-width estimate is tiny.
        var interior = GravityOnlyTrajectory.Evaluate(result, request, 3.135836623030339e-05);
        Assert.That(interior.Position.Z, Is.EqualTo(Math.BitIncrement(initialHeight)));
        Assert.That(interior.Position.Z - initialHeight, Is.GreaterThan(1e-9));
        Assert.That(Assert.Throws<ArithmeticException>(() => GravityOnlyTrajectory.GetMaximumHeight(result, request))!.Message,
            Does.Contain("position").And.Contain("precision"));
    }

    [TestCase(17, -1, true)]
    [TestCase(17, 1, true)]
    [TestCase(18, -1, false)]
    [TestCase(18, 1, false)]
    [TestCase(22, -1, false)]
    [TestCase(22, 1, false)]
    public void VelocityCertificateIncludesEvaluationRoundingNearSpacingBoundaries(int exponent, int sign, bool certifiable)
    {
        var bound = GravityOnlyRefinement.VelocityBound(sign * Math.ScaleB(1, exponent), 1, 1e-11, 1e-12);
        Assert.That(bound <= 1e-9, Is.EqualTo(certifiable));
        Assert.That(bound, Is.GreaterThan(1e-12)); // Physical variation alone is insufficient.
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void VelocitySpacingCoarserThanToleranceRejectsSmallPhysicalChanges(int sign)
    {
        Assert.That(Assert.Throws<ArithmeticException>(() =>
            GravityOnlyRefinement.VelocityBound(sign * Math.ScaleB(1, 23), 1, 1e-11, 1e-12))!.Message,
            Does.Contain("velocity").And.Contain("precision"));
    }

    [TestCase(0.99e-10, true)]
    [TestCase(1e-10, false)]
    public void VelocityCertificateReservesRoundingRoomAtTheUncertaintyThreshold(double width, bool certifiable)
    {
        var bound = GravityOnlyRefinement.VelocityBound(-10, 10, .2, width);
        Assert.That(bound <= 1e-9, Is.EqualTo(certifiable));
        Assert.That(bound, Is.GreaterThan(10 * width));
    }

    [TestCase(-double.MaxValue)]
    [TestCase(-1e9)]
    [TestCase(-double.Epsilon)]
    [TestCase(0)]
    [TestCase(double.Epsilon)]
    [TestCase(1e9)]
    [TestCase(double.MaxValue)]
    public void UnchangedVelocityHasExactlyZeroUncertainty(double velocity)
    {
        Assert.That(GravityOnlyRefinement.VelocityBound(velocity, 0, double.MaxValue, double.MaxValue), Is.Zero);
    }

    [TestCase(double.MaxValue, 8, 1)]
    [TestCase(1, double.MaxValue, 1)]
    [TestCase(double.Epsilon, .1, .05)]
    [TestCase(1e-300, 1, 1e-100)]
    public void VelocityCertificateRejectsNonrepresentableBoundCalculations(double gravity, double elapsed, double width)
    {
        Assert.Throws<ArithmeticException>(() => GravityOnlyRefinement.VelocityBound(0, gravity, elapsed, width));
    }

    [Test]
    public void RepresentableSubnormalAccelerationReceivesFiniteConservativeAllowance()
    {
        var bound = GravityOnlyRefinement.VelocityBound(0, double.Epsilon, 1, 1);
        Assert.That(double.IsFinite(bound), Is.True);
        Assert.That(bound, Is.GreaterThan(double.Epsilon).And.LessThan(1e-9));
    }

    [TestCase(-40)]
    [TestCase(0)]
    [TestCase(30)]
    public void BaseballScaleVelocitiesRetainAccurateLanding(double verticalVelocity)
    {
        var request = new FlightRequest(FlightFixtures.State(20, verticalVelocity, vx: 40, vy: -35), 9.80665, 10, .3);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
        Assert.That(result.FinalState.Velocity.Z,
            Is.EqualTo(-Math.Sqrt(verticalVelocity * verticalVelocity + 2 * 9.80665 * 20)).Within(1e-9));
        Assert.That(result.FinalState.Velocity.X, Is.EqualTo(40));
        Assert.That(result.FinalState.Velocity.Y, Is.EqualTo(-35));
        var prior = GravityOnlyTrajectory.Evaluate(result, request, Math.BitDecrement(result.FinalTime));
        Assert.That(Math.Abs(result.FinalState.Velocity.Z - prior.Velocity.Z), Is.LessThanOrEqualTo(1e-9));
    }

    [Test]
    public void ZeroGravityLargeVelocityComponentsRemainExact()
    {
        var velocity = new Vector3d(1e9, -1e9, -1e9);
        var request = new FlightRequest(new FlightState(new Vector3d(0, 0, 1), velocity), 0, 2e-9, 2e-9);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
        foreach (var time in new[] { 0d, result.FinalTime / 2, Math.BitDecrement(result.FinalTime), result.FinalTime })
            Assert.That(GravityOnlyTrajectory.Evaluate(result, request, time).Velocity, Is.EqualTo(velocity));
    }

    [Test]
    public void VelocityBoundCoversInteriorEvaluationsFromANonzeroAnchor()
    {
        const double start = 3;
        const double upper = 3.1;
        var lower = upper - 5e-13;
        const double gravity = 59.604644775390625;
        var state = FlightFixtures.State(1, -131072, vx: 1e9, vy: -1e9);
        var dynamics = new GravityDynamics(gravity);
        var bound = GravityOnlyRefinement.VelocityBound(state.Velocity.Z, gravity,
            Math.BitIncrement(upper - start), Math.BitIncrement(upper - lower));
        Assert.That(bound, Is.LessThanOrEqualTo(1e-9));
        var values = new List<double>();
        for (var i = 0; i <= 32; i++)
        {
            var time = lower + (upper - lower) * (i / 32d);
            var evaluated = Rk4Integrator.Step(start, time, state, dynamics.Evaluate);
            values.Add(evaluated.Velocity.Z);
            Assert.That(evaluated.Velocity.X, Is.EqualTo(state.Velocity.X));
            Assert.That(evaluated.Velocity.Y, Is.EqualTo(state.Velocity.Y));
        }
        Assert.That(values.Max() - values.Min(), Is.LessThanOrEqualTo(bound));
    }

    [Test]
    public void NonzeroAnchorContactAndApexResidualKeepTheirDistinctSemantics()
    {
        var anchor = new TrajectorySample(3, FlightFixtures.State(1, -10));
        var dynamics = new GravityDynamics(10);
        FlightState Evaluate(double time) => Rk4Integrator.Step(anchor.Time, time, anchor.State, dynamics.Evaluate);
        var contact = GravityOnlyRefinement.FindContact(anchor, new TrajectorySample(3.2, Evaluate(3.2)), 0, 10, Evaluate);
        Assert.That(contact.Time, Is.EqualTo(3 + (Math.Sqrt(120) - 10) / 10).Within(1e-10));
        Assert.That(contact.State.Velocity.Z, Is.EqualTo(-Math.Sqrt(120)).Within(1e-9));

        var request = new FlightRequest(FlightFixtures.State(11, 7), 9.80665, 2, 2);
        var result = GravityOnlySimulator.Simulate(request);
        var apex = GravityOnlyTrajectory.GetMaximumHeight(result, request);
        Assert.That(Math.Abs(apex.State.Velocity.Z), Is.LessThanOrEqualTo(1e-9));
        Assert.That(apex.State, Is.EqualTo(GravityOnlyTrajectory.Evaluate(result, request, apex.Time)));
    }
}
