using NUnit.Framework;
using Q = Lpb.Physics.Tests.SpinReference.Rational;
using I = Lpb.Physics.Tests.SpinReference.Interval;

namespace Lpb.Physics.Tests;

public class ModelADynamicsTests
{
    private static ModelAParameters Parameters(bool damping, double density = 1.2, double drag = .2) =>
        ModelAParameters.NathanAsrB(new(1, .25, density, drag),
            damping ? ModelARotationSettings.R1U(.5, .3) : ModelARotationSettings.R0());

    private static SpinFlightState Mixed => new(new(new(1, -2, 3), new(3, -4, 12)), new(2, 3, -1));

    [TestCase(false)]
    [TestCase(true)]
    public void MixedSpinCompositionMatchesIndependentForceAndTorqueReferences(bool damping)
    {
        var parameters = Parameters(damping);
        var dynamics = new ModelADynamics(.75, parameters);
        AssertDerivative(dynamics.Evaluate(4, Mixed), .75, parameters, Mixed);
    }

    [Test]
    public void ConstructorRejectsNullParameters()
    {
        Assert.That(Assert.Throws<ArgumentNullException>(() => new ModelADynamics(0, null!))!.ParamName,
            Is.EqualTo("parameters"));
    }

    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ConstructorRetainsGravityValidation(double gravity)
    {
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new ModelADynamics(gravity, Parameters(false)))!.ParamName,
            Is.EqualTo("gravityMagnitude"));
    }

    [Test]
    public void TimeAndPositionDoNotReplaceInstantaneousVelocityOrSpin()
    {
        var parameters = Parameters(true);
        var dynamics = new ModelADynamics(2, parameters);
        var relocated = new SpinFlightState(new(new(double.MaxValue, -50, -100), Mixed.Translation.Velocity), Mixed.AngularVelocity);
        Assert.That(dynamics.Evaluate(100, relocated), Is.EqualTo(dynamics.Evaluate(0, Mixed)));
        var changed = new SpinFlightState(new(default, new(-4, 2, 1)), new(-1, 5, 2));
        AssertDerivative(dynamics.Evaluate(101, changed), 2, parameters, changed);
        Assert.That(dynamics.Evaluate(101, changed), Is.Not.EqualTo(dynamics.Evaluate(0, Mixed)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ZeroDensityRemovesAllAerodynamicsWithoutEvaluatingUnsafeMagnitudes(bool damping)
    {
        var aero = new AerodynamicSettings(double.Epsilon, double.MaxValue, 0, double.MaxValue);
        var parameters = ModelAParameters.NathanAsrB(aero,
            damping ? ModelARotationSettings.R1U(double.Epsilon, double.MaxValue) : ModelARotationSettings.R0());
        var state = new SpinFlightState(new(default, new(double.MaxValue, double.MaxValue, 0)),
            new(double.MaxValue, -double.MaxValue, double.MaxValue));
        Assert.That(new ModelADynamics(10, parameters).Evaluate(0, state),
            Is.EqualTo(new SpinFlightDerivative(double.MaxValue, double.MaxValue, 0, 0, 0, -10, 0, 0, 0)));
    }

    [Test]
    public void ZeroDragLeavesBothMagnusAndR1UDampingActive()
    {
        var parameters = Parameters(true, drag: 0);
        var result = new ModelADynamics(0, parameters).Evaluate(0, Mixed);
        AssertDerivative(result, 0, parameters, Mixed);
        Assert.That(new[] { result.VelocityXRate, result.VelocityYRate, result.VelocityZRate }.Any(x => x != 0), Is.True);
        Assert.That(new[] { result.AngularVelocityXRate, result.AngularVelocityYRate, result.AngularVelocityZRate }.Any(x => x != 0), Is.True);
    }

    [Test]
    public void ZeroSpinRemovesLiftAndTorqueButLeavesDragAndGravity()
    {
        var parameters = Parameters(true);
        var state = new SpinFlightState(Mixed.Translation, default);
        var result = new ModelADynamics(2, parameters).Evaluate(0, state);
        AssertDerivative(result, 2, parameters, state);
        Assert.That(result.VelocityXRate, Is.LessThan(0));
        Assert.That(result.VelocityYRate, Is.GreaterThan(0));
        Assert.That(new[] { result.AngularVelocityXRate, result.AngularVelocityYRate, result.AngularVelocityZRate },
            Is.EqualTo(new[] { 0d, 0, 0 }));
    }

    [Test]
    public void ZeroBetaAndR0HaveIdenticalDerivativesWithoutChangingTheirSelections()
    {
        var r0 = Parameters(false);
        var r1 = ModelAParameters.NathanAsrB(r0.Aerodynamics, ModelARotationSettings.R1U(.5, 0));
        Assert.That(new ModelADynamics(2, r1).Evaluate(0, Mixed), Is.EqualTo(new ModelADynamics(2, r0).Evaluate(0, Mixed)));
        Assert.That(r1.Rotation.Kind, Is.EqualTo(ModelARotationKind.R1U));
        Assert.That(r0.Rotation.RotationalInertia, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InitiallyGyrospinCanProduceLiftAtLaterTurningStages(bool damping)
    {
        var parameters = Parameters(damping, drag: 0);
        var dynamics = new ModelADynamics(1, parameters);
        var initial = new SpinFlightState(new(default, new(1, 0, 0)), new(1, 0, 0));
        var rates = new List<SpinFlightDerivative>();
        SpinRk4Integrator.Step(0, .125, initial, (time, state) =>
        {
            var derivative = dynamics.Evaluate(time, state);
            AssertDerivative(derivative, 1, parameters, state);
            rates.Add(derivative);
            return derivative;
        });
        Assert.That(rates.Count, Is.EqualTo(4));
        Assert.That(rates[0].VelocityYRate, Is.Zero);
        Assert.That(rates[1].VelocityYRate, Is.GreaterThan(0));
        Assert.That(rates[0].AngularVelocityXRate, damping ? Is.LessThan(0) : Is.Zero);
    }

    [Test]
    public void ZeroSpeedCanReactivateBothLiftAndDampingAtSubsequentStages()
    {
        var parameters = Parameters(true, drag: 0);
        var dynamics = new ModelADynamics(1, parameters);
        var rates = new List<SpinFlightDerivative>();
        SpinRk4Integrator.Step(0, .125, new(default, new(0, 1, 0)), (time, state) =>
        {
            var derivative = dynamics.Evaluate(time, state);
            AssertDerivative(derivative, 1, parameters, state);
            rates.Add(derivative);
            return derivative;
        });
        Assert.That(rates[0], Is.EqualTo(new SpinFlightDerivative(0, 0, 0, 0, 0, -1, 0, 0, 0)));
        Assert.That(rates[1].VelocityXRate, Is.LessThan(0));
        Assert.That(rates[1].AngularVelocityYRate, Is.LessThan(0));
    }

    [Test]
    public void EveryCoupledR1UStageUsesItsOwnCompleteState()
    {
        var parameters = Parameters(true);
        var dynamics = new ModelADynamics(.75, parameters);
        var calls = new List<SpinFlightState>();
        SpinRk4Integrator.Step(3, 3.125, Mixed, (time, state) =>
        {
            calls.Add(state);
            var derivative = dynamics.Evaluate(time, state);
            AssertDerivative(derivative, .75, parameters, state);
            return derivative;
        });
        Assert.That(calls.Count, Is.EqualTo(4));
        for (var stage = 1; stage < calls.Count; stage++)
        {
            Assert.That(calls[stage].Translation.Velocity, Is.Not.EqualTo(calls[0].Translation.Velocity));
            Assert.That(calls[stage].AngularVelocity, Is.Not.EqualTo(calls[0].AngularVelocity));
        }
    }

    [TestCase(800)]
    [TestCase(-800)]
    public void MagnusFailureIsPropagatedWithItsOriginalComponentAndCause(int exponent)
    {
        var scale = Math.ScaleB(1, exponent);
        var parameters = ModelAParameters.NathanAsrB(new(1, 1, 1, 0), ModelARotationSettings.R0());
        var error = Assert.Throws<ArithmeticException>(() => new ModelADynamics(0, parameters)
            .Evaluate(0, new(new(default, new(scale, 0, 0)), new(0, scale, 0))));
        Assert.That(error!.Message, Does.Contain("coupled dynamics").And.Contain("Magnus").And.Contain("acceleration.Z"));
        Assert.That(error.InnerException, Is.TypeOf<ArithmeticException>());
        Assert.That(error.InnerException!.InnerException, Is.TypeOf<ArithmeticException>());
    }

    [TestCase(800)]
    [TestCase(-800)]
    public void R1UFailureIsNotReplacedByTheGyrospinZeroLiftReduction(int exponent)
    {
        var scale = Math.ScaleB(1, exponent);
        var parameters = ModelAParameters.NathanAsrB(new(1, 1, 1, 0), ModelARotationSettings.R1U(1, 1));
        var error = Assert.Throws<ArithmeticException>(() => new ModelADynamics(0, parameters)
            .Evaluate(0, new(new(default, new(scale, 0, 0)), new(scale, 0, 0))));
        Assert.That(error!.Message, Does.Contain("coupled dynamics").And.Contain("R1-U").And.Contain("derivative.X"));
        Assert.That(error.InnerException, Is.TypeOf<ArithmeticException>());
    }

    [TestCase(800)]
    [TestCase(-800)]
    public void LegacyDragArithmeticFailuresRetainTheirContext(int exponent)
    {
        var parameters = ModelAParameters.NathanAsrB(new(1, 1, 1, 1), ModelARotationSettings.R0());
        var state = new SpinFlightState(new(default, new(Math.ScaleB(1, exponent), 0, 0)), default);
        var error = Assert.Throws<ArithmeticException>(() => new ModelADynamics(0, parameters).Evaluate(0, state));
        Assert.That(error!.Message, Does.Contain("coupled dynamics").And.Contain("Drag").And.Contain("velocity.X"));
        Assert.That(error.InnerException, Is.TypeOf<ArithmeticException>());
    }

    [TestCase(0, "velocity.X")]
    [TestCase(1, "velocity.Y")]
    [TestCase(2, "velocity.Z")]
    public void FiniteConstituentsWithAnOverflowingCombinedAccelerationReject(int axis, string component)
    {
        // X/Y: individual drag and lift components are about .67*Max and .55*Max.
        // Z: finite downward Magnus acceleration is added to gravity -Max.
        var parameters = ModelAParameters.NathanAsrB(new(1, 1, double.MaxValue * (axis == 2 ? .25 : .75),
            axis == 2 ? 0 : .4), ModelARotationSettings.R0());
        var state = axis == 2
            ? new SpinFlightState(new(default, new(1, 0, 0)), new(0, 1, 0))
            : new SpinFlightState(new(default, new(1, 1, 0)), new(0, 0, axis == 0 ? 1 : -1));
        var error = Assert.Throws<ArithmeticException>(() => new ModelADynamics(axis == 2 ? double.MaxValue : 0, parameters).Evaluate(0, state));
        Assert.That(error!.Message, Does.Contain("Combined acceleration").And.Contain(component).And.Contain("nonfinite"));
    }

    [TestCase(0)]
    [TestCase(.5)]
    [TestCase(1)]
    [TestCase(2)]
    public void R0CircularMotionHasTheIndependentCentripetalDerivative(double angle)
    {
        var a = new AerodynamicSettings(1, 1, 2 / Math.PI, 0);
        var parameters = ModelAParameters.NathanAsrB(a, ModelARotationSettings.R0());
        var frequency = a.AirDensity * Math.PI / 2 * .336 * (1 - Math.Exp(-6.041));
        var state = new SpinFlightState(new(new(Math.Sin(angle) / frequency, (1 - Math.Cos(angle)) / frequency, -3),
            new(Math.Cos(angle), Math.Sin(angle), 0)), new(0, 0, 1));
        var result = new ModelADynamics(0, parameters).Evaluate(angle / frequency, state);
        AssertDerivative(result, 0, parameters, state);
        var rounding = 256 * Math.ScaleB(1, -53) * frequency;
        Assert.That(result.VelocityXRate, Is.EqualTo(-frequency * Math.Sin(angle)).Within(rounding));
        Assert.That(result.VelocityYRate, Is.EqualTo(frequency * Math.Cos(angle)).Within(rounding));
        Assert.That(result.VelocityZRate, Is.Zero);
        // This checks the ODE on a prescribed analytical circle, not a checked trajectory producer.
    }

    [TestCase(0)]
    [TestCase(.35)]
    public void CoupledDerivativePreservesStationaryAirEnergyIdentities(double drag)
    {
        var parameters = ModelAParameters.NathanAsrB(new(1, 1, 1, drag), ModelARotationSettings.R1U(1, .2));
        var state = new SpinFlightState(new(default, new(3, 4, 12)), new(0, 0, 2));
        var actual = new ModelADynamics(2, parameters).Evaluate(0, state);
        var reference = References(2, parameters, state);
        var velocities = new[] { 3d, 4, 12 };
        var rates = new[] { actual.VelocityXRate, actual.VelocityYRate, actual.VelocityZRate };
        var energy = Q.From(24);
        var allowance = Q.Zero;
        for (var i = 0; i < 3; i++)
        {
            energy += Q.From(velocities[i]) * Q.From(rates[i]);
            allowance += Q.From(velocities[i]).Abs() * AccelerationBudget(reference.Drag[i], reference.Lift[i], i == 2 ? 2 : 0);
        }
        AssertNear(energy, -SpinReference.Pi * I.Point(new Q(2197, 2) * Q.From(drag)), allowance);
        var rotational = Q.From(2) * Q.From(actual.AngularVelocityZRate);
        AssertNear(rotational, -SpinReference.Pi * I.Point(Q.From(.2) * Q.From(52)),
            Q.From(2) * ErrorBudget(reference.Angular[2], 32));
        Assert.That(rotational.Sign, Is.LessThan(0));
    }

    private static void AssertDerivative(SpinFlightDerivative actual, double gravity, ModelAParameters parameters, SpinFlightState state)
    {
        Assert.That(new[] { actual.PositionXRate, actual.PositionYRate, actual.PositionZRate },
            Is.EqualTo(new[] { state.Translation.Velocity.X, state.Translation.Velocity.Y, state.Translation.Velocity.Z }));
        var reference = References(gravity, parameters, state);
        var accelerations = new[] { actual.VelocityXRate, actual.VelocityYRate, actual.VelocityZRate };
        for (var i = 0; i < 3; i++)
            AssertNear(Q.From(accelerations[i]), reference.Acceleration[i],
                AccelerationBudget(reference.Drag[i], reference.Lift[i], i == 2 ? gravity : 0));
        SpinReference.AssertVector(new(actual.AngularVelocityXRate, actual.AngularVelocityYRate, actual.AngularVelocityZRate),
            reference.Angular, 32);
    }

    private static (I[] Acceleration, I[] Drag, I[] Lift, I[] Angular) References(
        double gravity, ModelAParameters parameters, SpinFlightState state)
    {
        var a = parameters.Aerodynamics;
        var v = state.Translation.Velocity;
        var lift = SpinReference.Magnus(a, v, state.AngularVelocity);
        var factor = -SpinReference.Pi * I.Point(new Q(1, 2)) * Point(a.AirDensity) *
            Point(a.BallRadius) * Point(a.BallRadius) * Point(a.DragCoefficient) /
            Point(a.BallMass) * SpinReference.Norm(v);
        var drag = new[] { v.X, v.Y, v.Z }.Select(x => factor * Point(x)).ToArray();
        var acceleration = Enumerable.Range(0, 3).Select(i => drag[i] + lift[i] - Point(i == 2 ? gravity : 0)).ToArray();
        var angular = parameters.Rotation.Kind == ModelARotationKind.R0
            ? new[] { SpinReference.Point(0), SpinReference.Point(0), SpinReference.Point(0) }
            : SpinReference.Rotation(a, parameters.Rotation.RotationalInertia!.Value,
                parameters.Rotation.Beta!.Value, v, state.AngularVelocity);
        return (acceleration, drag, lift, angular);
    }

    private static I Point(double value) => I.Point(Q.From(value));
    private static Q Magnitude(I value) => new[] { value.Lower.Abs(), value.Upper.Abs() }.Max();
    private static Q ErrorBudget(I reference, int operations) =>
        SpinReference.Gamma(operations) * Magnitude(reference) + Q.From(2) * Q.Pow2(-1074);

    private static Q AccelerationBudget(I drag, I lift, double gravity) =>
        ErrorBudget(drag, 32) + ErrorBudget(lift, 128) +
        SpinReference.Gamma(4) * (Magnitude(drag) + Magnitude(lift) + Q.From(gravity));

    private static void AssertNear(Q actual, I reference, Q allowance)
    {
        Assert.That((actual - reference.Lower).Abs().CompareTo(allowance), Is.LessThanOrEqualTo(0));
        Assert.That((actual - reference.Upper).Abs().CompareTo(allowance), Is.LessThanOrEqualTo(0));
    }
}
