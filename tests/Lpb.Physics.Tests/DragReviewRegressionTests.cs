using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class DragReviewRegressionTests
{
    private const double AliasedStep = 5.248930418956277e-6;

    [TestCase(0, 1)]
    [TestCase(0, -1)]
    [TestCase(1, 1)]
    [TestCase(1, -1)]
    [TestCase(2, 1)]
    [TestCase(2, -1)]
    public void AgreementCannotAuthorizeDragThatIncreasesSpeed(int axis, int sign)
    {
        var a = new AerodynamicSettings(Math.PI / 2, 1, 1e12, 1);
        var request = new FlightRequest(new(default, Axis(axis, sign * 1e-6)), 0, AliasedStep, AliasedStep);
        var pair = RawStep(request, a, AliasedStep);
        // This intentionally verifies that the existing six-component tolerances
        // pass: rejection must come from the additional physical invariants.
        Assert.DoesNotThrow(() => DragNumericalEvaluation.CheckAgreement(pair.Full, pair.Diagnostic));
        Assert.That(Math.Abs(DragFixtures.Components(pair.Full)[axis + 3]), Is.GreaterThan(.09));
        Assert.That(sign * DragFixtures.Components(pair.Full)[axis], Is.LessThan(0));
        SimulationResult? partial = null;
        var error = Assert.Throws<ArithmeticException>(() => partial = GravityDragSimulator.Simulate(request, a));
        Assert.That(error!.Message, Does.Contain($"velocity.{"XYZ"[axis]} magnitude increased").And.Contain("Integration step 1"));
        Assert.That(partial, Is.Null);
    }

    [TestCase(0, 1)]
    [TestCase(0, -1)]
    [TestCase(1, 1)]
    [TestCase(1, -1)]
    [TestCase(2, 1)]
    [TestCase(2, -1)]
    public void SameExtremeParametersWorkWithRefinedTimestep(int axis, int sign)
    {
        var a = new AerodynamicSettings(Math.PI / 2, 1, 1e12, 1);
        var request = new FlightRequest(new(default, Axis(axis, sign * 1e-6)), 0, AliasedStep, AliasedStep / 1024);
        var result = GravityDragSimulator.Simulate(request, a);
        var values = DragFixtures.Components(result.FinalState);
        var denominator = 1 + 1e12 * 1e-6 * AliasedStep;
        Assert.That(values[axis + 3], Is.EqualTo(sign * 1e-6 / denominator).Within(1e-15));
        Assert.That(values[axis], Is.EqualTo(sign * Math.Log(denominator) / 1e12).Within(1e-20));
    }

    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(1048576, false)]
    [TestCase(1048576, true)]
    public void OrdinaryEndpointsMustAgreeOnContactSideInBothDirections(double height, bool opposite)
    {
        var a = new AerodynamicSettings(.145, 1, 1.225, .35);
        var request = ClearanceRequest(height, opposite, 1);
        var pair = RawStep(request, a, 1);
        Assert.That(pair.Full.Position.Z > height, Is.EqualTo(opposite));
        Assert.That(pair.Diagnostic.Position.Z > height, Is.EqualTo(!opposite));
        SimulationResult? partial = null;
        var error = Assert.Throws<ArithmeticException>(() =>
            partial = GravityDragSimulator.SimulateUntilGroundContact(request, a, height - 1));
        Assert.That(error!.Message, Does.Contain("contact geometry disagreement").And.Contain("at 1 s"));
        Assert.That(partial, Is.Null);
    }

    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(1048576, false)]
    [TestCase(1048576, true)]
    public void RefinementCannotUpdateBracketFromContradictoryGeometry(double height, bool opposite)
    {
        var a = new AerodynamicSettings(.145, 1, 1.225, .35);
        var request = ClearanceRequest(height, opposite, 2);
        var ordinary = RawStep(request, a, 2);
        Assert.That(ordinary.Full.Position.Z, Is.LessThanOrEqualTo(height));
        Assert.That(ordinary.Diagnostic.Position.Z, Is.LessThanOrEqualTo(height));
        var trial = RawStep(request, a, 1);
        Assert.That(trial.Full.Position.Z > height, Is.EqualTo(opposite));
        Assert.That(trial.Diagnostic.Position.Z > height, Is.EqualTo(!opposite));
        var error = Assert.Throws<ArithmeticException>(() => GravityDragSimulator.SimulateUntilGroundContact(request, a, height - 1));
        Assert.That(error!.Message, Does.Contain("contact geometry disagreement").And.Contain("at 1 s"));
    }

    [TestCase(1)]
    [TestCase(1048576)]
    public void ExactPrimaryRootWithDiagnosticBelowRequiresFurtherLocalization(double height)
    {
        var spacing = Math.BitIncrement(height) - height;
        var a = new AerodynamicSettings(.145, 1, 1.225, .35);
        var request = new FlightRequest(new(new(0, 0, height + spacing), new(0, 0, -1.25 * spacing)), 0, 1, 1);
        var ordinary = RawStep(request, a, 1);
        Assert.That(ordinary.Full.Position.Z, Is.EqualTo(height));
        Assert.That(ordinary.Diagnostic.Position.Z, Is.LessThan(height));
        var error = Assert.Throws<ArithmeticException>(() => GravityDragSimulator.SimulateUntilGroundContact(request, a, height - 1));
        Assert.That(error!.Message, Does.Contain("contact geometry disagreement").And.Contain("at 0.5 s"));
    }

    [TestCase(0, 1, false)]
    [TestCase(0, -1, false)]
    [TestCase(1, 1, false)]
    [TestCase(1, -1, false)]
    [TestCase(2, 1, false)]
    [TestCase(2, -1, false)]
    [TestCase(0, 1, true)]
    [TestCase(0, -1, true)]
    [TestCase(1, 1, true)]
    [TestCase(1, -1, true)]
    [TestCase(2, 1, true)]
    [TestCase(2, -1, true)]
    public void DisplacementChecksAreIndependentOfVelocityGrowth(int axis, int sign, bool tooFar)
    {
        var from = new FlightState(default, Axis(axis, sign));
        var to = new FlightState(Axis(axis, sign * (tooFar ? .2 : -.01)), Axis(axis, sign * .5));
        var numerical = new DragNumericalEvaluation(new(from, 0, 1, .1), DragFixtures.Baseball);
        Assert.DoesNotThrow(() => numerical.CheckTransition(from, to)); // Sign and magnitude alone pass.
        var error = Assert.Throws<ArithmeticException>(() => numerical.CheckTransition(from, to, .1));
        Assert.That(error!.Message, Does.Contain(tooFar ? "initial-speed motion bound" : "opposes its velocity direction"));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void AZeroVelocityAxisCannotMove(int axis)
    {
        var numerical = new DragNumericalEvaluation(new(default, 0, 1, 1), DragFixtures.Baseball);
        Assert.That(Assert.Throws<ArithmeticException>(() => numerical.CheckTransition(default, new(Axis(axis, .01), default), 1))!.Message,
            Does.Contain("zero velocity on an invariant axis"));
    }

    [Test]
    public void MotionEnvelopeRejectsUnrepresentableBoundArithmetic()
    {
        var from = new FlightState(default, new(1, 0, 0));
        var to = new FlightState(new(.5, 0, 0), new(.5, 0, 0));
        var numerical = new DragNumericalEvaluation(new(from, 0, 1, 1), DragFixtures.Baseball);
        Assert.That(Assert.Throws<ArithmeticException>(() => numerical.CheckTransition(from, to, double.MaxValue))!.Message,
            Does.Contain("motion bound is not representable"));
    }

    [Test]
    public void PositiveGravityStillPermitsLegitimateDownwardAcceleration()
    {
        var a = new AerodynamicSettings(.145, .0366, 1e-20, .35);
        var request = new FlightRequest(new(new(0, 0, 20), new(0, 0, -1)), 10, .1, .1);
        var result = GravityDragSimulator.Simulate(request, a);
        Assert.That(result.FinalState.Velocity.Z, Is.EqualTo(-2).Within(1e-14));
        Assert.That(result.FinalState.Position.Z, Is.EqualTo(19.85).Within(1e-14));
    }

    [Test]
    public void ExactPrimaryEndpointMayCompleteThroughBoundedPairedLocalization()
    {
        var a = new AerodynamicSettings(Math.PI / 2, 1, 1, 1);
        const double time = .01;
        var initial = new FlightRequest(new(default, new(0, 0, -1)), 0, time, time);
        var shift = -RawStep(initial, a, time).Full.Position.Z;
        var request = new FlightRequest(new(new(0, 0, shift), initial.InitialState.Velocity), 0, time, time);
        var pair = RawStep(request, a, time);
        Assert.That(pair.Full.Position.Z, Is.Zero);
        Assert.That(pair.Diagnostic.Position.Z, Is.LessThan(0));
        var result = GravityDragSimulator.SimulateUntilGroundContact(request, a, -1);
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
        Assert.That(result.FinalTime, Is.EqualTo(time));
        Assert.That(result.FinalState, Is.EqualTo(pair.Full));
    }

    [TestCase(1)]
    [TestCase(1048576)]
    public void EvenPairedRoundedRootsMustBePhysicallyReachable(double height)
    {
        var spacing = Math.BitIncrement(height) - height;
        var a = new AerodynamicSettings(.145, 1, 1.225, .35);
        // Two rounded updates can cover two ULPs while actual motion is <1.75 ULPs.
        var request = new FlightRequest(new(new(0, 0, height + 2 * spacing), new(0, 0, -1.75 * spacing)), 0, 1, 1);
        var pair = RawStep(request, a, 1);
        Assert.That(pair.Full.Position.Z, Is.EqualTo(height));
        Assert.That(pair.Diagnostic.Position.Z, Is.EqualTo(height));
        SimulationResult? partial = null;
        var error = Assert.Throws<ArithmeticException>(() => partial = GravityDragSimulator.SimulateUntilGroundContact(request, a, height - 1));
        Assert.That(error!.Message, Does.Contain("contact at 1 s is unreachable").And.Contain("initial-speed motion bound"));
        Assert.That(partial, Is.Null);
    }

    private static FlightRequest ClearanceRequest(double height, bool opposite, double duration)
    {
        var spacing = Math.BitIncrement(height) - height;
        return new(new(new(0, 0, height + (opposite ? 2 : 1) * spacing),
            new(0, 0, -(opposite ? 1.25 : .75) * spacing)), 0, duration, duration);
    }

    private static Vector3d Axis(int axis, double value) =>
        new(axis == 0 ? value : 0, axis == 1 ? value : 0, axis == 2 ? value : 0);

    private static (FlightState Full, FlightState Diagnostic) RawStep(FlightRequest request, AerodynamicSettings a, double time)
    {
        var dynamics = new GravityDragDynamics(request.GravityMagnitude, a);
        Func<double, FlightState, FlightDerivative> evaluate = dynamics.Evaluate;
        var full = Rk4Integrator.Step(0, time, request.InitialState, evaluate);
        var half = Rk4Integrator.Step(0, time / 2, request.InitialState, evaluate);
        return (full, Rk4Integrator.Step(time / 2, time, half, evaluate));
    }
}
