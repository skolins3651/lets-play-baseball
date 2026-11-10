using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class DragRefinementTests
{
    [Test]
    public void InteriorApexWitnessFindsHeightHiddenByEqualEndpoints()
    {
        var a = new AerodynamicSettings(.145, .0366, 1e-20, .35);
        var request = new FlightRequest(new(new(0, 0, Math.ScaleB(1, 20)), new(0, 0, .00023759466905108544)),
            242.45617275970022, .00001, .00001);
        var numerical = new DragNumericalEvaluation(request, a);
        var anchor = new TrajectorySample(0, request.InitialState);
        const double lowerTime = 9.79908938180126e-7, upperTime = 9.79988938180126e-7;
        var lower = new TrajectorySample(lowerTime, numerical.Step(anchor, lowerTime));
        var upper = new TrajectorySample(upperTime, numerical.Step(anchor, upperTime));
        Assert.That(lower.State.Position.Z, Is.EqualTo(upper.State.Position.Z));
        var apex = GravityDragRefinement.FindApex(anchor, lower, upper, numerical);
        Assert.That(apex.Time, Is.EqualTo(lowerTime + (upperTime - lowerTime) / 2));
        Assert.That(apex.State.Position.Z, Is.EqualTo(Math.BitIncrement(lower.State.Position.Z)));
        Assert.That(apex.State.Velocity.Z, Is.Zero);
    }

    [Test]
    public void LargeCoordinateAuditRejectsEvenWhenRoundedEndpointsAgree()
    {
        var request = new FlightRequest(new(new(0, 0, Math.ScaleB(1, 30)), new(0, 0, .007603029409634734)),
            242.45617275970022, 7.839591505441008e-5, 7.839591505441008e-5);
        Assert.That(Assert.Throws<ArithmeticException>(() => GravityDragSimulator.Simulate(request,
            new(.145, .0366, 1e-20, .35)))!.Message, Does.Contain("position.Z precision"));
    }

    [Test]
    public void UnresolvableAirborneExcursionCannotBecomeFalseContact()
    {
        var request = new FlightRequest(new(new(0, 0, 1), new(0, 0, 1e-12)), 10, 3e-13, 3e-13);
        var a = new AerodynamicSettings(.145, 1, 1.225, .35);
        Assert.That(Assert.Throws<ArithmeticException>(() => GravityDragSimulator.SimulateUntilGroundContact(request, a, 0))!.Message,
            Does.Contain("resolvable airborne"));
    }

    [Test]
    public void RequiredContactTrialBudgetExhaustsWithoutPartialResult()
    {
        // All finite dynamics and absolute consistency checks pass. The contact is
        // far nearer zero than 128 bisections of this horizon can resolve.
        var a = new AerodynamicSettings(Math.PI / 2, 1, 1e100, 1);
        var request = new FlightRequest(new(new(0, 0, double.Epsilon), new(0, 0, -1e-200)), 0, 1e100, 1e100);
        SimulationResult? partial = null;
        var exception = Assert.Throws<ArithmeticException>(() => partial = GravityDragSimulator.SimulateUntilGroundContact(request, a, -1));
        Assert.That(exception!.Message, Does.Contain("128").And.Contain("contact"));
        Assert.That(partial, Is.Null);
    }

    [Test]
    public void OptionalMaximumFailureDoesNotInvalidateCompletedFlight()
    {
        // Ordinary stage accelerations are representable. Near the apex, nonzero
        // drag products underflow before a required numerical state is resolved.
        var a = new AerodynamicSettings(Math.PI / 2, 1, 1e-110, 1);
        var request = new FlightRequest(new(new(0, 0, 1), new(0, 0, 1.1e-100)), 1e-200, 3e100, 3e100);
        var result = GravityDragSimulator.Simulate(request, a);
        var snapshot = result.Samples.ToArray();
        Assert.Throws<ArithmeticException>(() => GravityDragTrajectory.GetMaximumHeight(result, request, a));
        Assert.That(result.Samples, Is.EqualTo(snapshot));
        Assert.That(result.FinalTime, Is.EqualTo(request.Duration));
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
    }

    [TestCase(1, 0)]
    [TestCase(-1, 0)]
    [TestCase(1, -1)]
    [TestCase(-1, 1)]
    public void NonzeroHorizontalSignsCannotDisappearOrReverse(double from, double to)
    {
        var initial = new FlightState(default, new(from, from, 0));
        var numerical = new DragNumericalEvaluation(new(initial, 10, 1, .01), DragFixtures.Baseball);
        Assert.Throws<ArithmeticException>(() => numerical.CheckTransition(initial, new(default, new(to, to, -1))));
    }

    [TestCase(0)]
    [TestCase(10)]
    public void VerticalSignAndTransitionPoliciesRemainSpecialized(double gravity)
    {
        var initial = new FlightState(default, new(1, -2, -1));
        var numerical = new DragNumericalEvaluation(new(initial, gravity, 1, .01), DragFixtures.Baseball);
        Assert.Throws<ArithmeticException>(() => numerical.CheckTransition(initial, new(default, new(.9, -1.8, 1))));
        // Descending velocity may become less negative under drag near terminal behavior.
        Assert.DoesNotThrow(() => numerical.CheckTransition(initial, new(default, new(.9, -1.8, -.9))));
    }

    [Test]
    public void ExactRecordedApexIsReturnedVerbatim()
    {
        var a = new AerodynamicSettings(.145, .0366, 1e-20, .35);
        var request = new FlightRequest(new(default, new(0, 0, 10)), 10, 2, 1);
        var result = GravityDragSimulator.Simulate(request, a);
        Assert.That(result.Samples[1].State.Velocity.Z, Is.Zero);
        Assert.That(GravityDragTrajectory.GetMaximumHeight(result, request, a), Is.EqualTo(result.Samples[1]));
    }
}
