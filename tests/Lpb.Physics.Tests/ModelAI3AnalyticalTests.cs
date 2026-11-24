using NUnit.Framework;
using I = Lpb.Physics.Tests.SpinReference.Interval;
using Q = Lpb.Physics.Tests.SpinReference.Rational;
using static Lpb.Physics.Tests.ModelAI3Fixtures;

namespace Lpb.Physics.Tests;

// Targeted I3 analytical checks only. No I4 coupled high-precision reference generator.
public class ModelAI3AnalyticalTests
{
    [TestCase(1)]
    [TestCase(-1)]
    public void ConstantSpeedCardinalGyroDampsByTheIndependentExponential(int sign)
    {
        var p = Baseball(drag: 0);
        var request = new ModelAFlightRequest(State(velocity: new(30, 0, 0), spin: new(sign * 100, 0, 0)), 0, 2, .01);
        var result = ModelASimulator.Simulate(request, p);
        var a = p.Aerodynamics; var r = Q.From(a.BallRadius);
        var gamma = I.Point(Q.From(a.AirDensity) * r * r * r * r * Q.From(p.Rotation.Beta!.Value) /
            Q.From(p.Rotation.RotationalInertia!.Value)) * SpinReference.Pi;
        var exponential = SpinReference.Point(1) - SpinReference.OneMinusExp(gamma * SpinReference.Point(60));
        var expected = SpinReference.Rounded(exponential * SpinReference.Point(sign * 100));
        Assert.That(result.FinalState.AngularVelocity.X, Is.EqualTo(expected).Within(1e-10));
        Assert.That(result.FinalState.AngularVelocity.Y, Is.Zero);
        Assert.That(result.FinalState.AngularVelocity.Z, Is.Zero);
        Assert.That(result.FinalState.Translation.Velocity, Is.EqualTo(new Vector3d(30, 0, 0)));
        Assert.That(result.FinalState.Translation.Position.X, Is.EqualTo(60).Within(1e-12));
    }

    [Test]
    public void R0TransverseMagnusFlightMatchesAnalyticalCircularPositionAndVelocity()
    {
        var a = new AerodynamicSettings(1, 1, 2 / Math.PI, 0);
        var p = ModelAParameters.NathanAsrB(a, ModelARotationSettings.R0());
        var frequency = a.AirDensity * Math.PI / 2 * .336 * (1 - Math.Exp(-6.041));
        var request = new ModelAFlightRequest(State(new(0, 0, -3), new(1, 0, 0), new(0, 0, 1)), 0, 1, .01);
        var result = ModelASimulator.Simulate(request, p);
        var x = result.FinalState.Translation.Position; var v = result.FinalState.Translation.Velocity;
        Assert.That(x.X, Is.EqualTo(Math.Sin(frequency) / frequency).Within(5e-10));
        Assert.That(x.Y, Is.EqualTo((1 - Math.Cos(frequency)) / frequency).Within(5e-10));
        Assert.That(v.X, Is.EqualTo(Math.Cos(frequency)).Within(5e-10));
        Assert.That(v.Y, Is.EqualTo(Math.Sin(frequency)).Within(5e-10));
        Assert.That(x.Z, Is.EqualTo(-3)); Assert.That(v.Z, Is.Zero);
        Assert.That(result.FinalState.AngularVelocity, Is.EqualTo(request.InitialState.AngularVelocity));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TestOnlyPrescribedSpeedAndDensityMatchPolynomialDampingIntegrals(bool varyingDensity)
    {
        // U(t)=1+t, rho(t)=1 or 1+t^2; beta=.1, r=I=1.
        // Integral rho*U dt = t+t^2/2 [+ t^3/3+t^4/4]. No production environment API.
        const double duration = .25;
        var state = State(spin: new(2, -3, 0));
        for (var i = 0; i < 100; i++)
            state = SpinRk4Integrator.Step(i * duration / 100, (i + 1) * duration / 100, state, (time, s) =>
            {
                var rho = varyingDensity ? 1 + time * time : 1;
                var kernel = ModelARotationKernel.R1U(new(1, 1, rho, 0), 1, .1);
                var rate = kernel.Evaluate(new(1 + time, 0, 0), s.AngularVelocity);
                return new(0, 0, 0, 0, 0, 0, rate.X, rate.Y, rate.Z);
            });
        var t = Q.From(duration);
        var integral = t + t * t / new Q(2, 1);
        if (varyingDensity) integral += t * t * t / new Q(3, 1) + t * t * t * t / new Q(4, 1);
        var exponential = SpinReference.Point(1) - SpinReference.OneMinusExp(SpinReference.Pi * I.Point(Q.From(.1) * integral));
        Assert.That(state.AngularVelocity.X, Is.EqualTo(SpinReference.Rounded(exponential * SpinReference.Point(2))).Within(2e-12));
        Assert.That(state.AngularVelocity.Y, Is.EqualTo(SpinReference.Rounded(exponential * SpinReference.Point(-3))).Within(2e-12));
        Assert.That(state.AngularVelocity.Z, Is.Zero);
    }

    [Test]
    public void AngularScratchStagesMayIncreaseFromStageTwoToThreeInsideTheirSignedRange()
    {
        var initial = State(velocity: new(1, 0, 0), spin: new(1e-6, 0, 0));
        var p = ModelAParameters.NathanAsrB(new(1e30, 1, 1 / Math.PI, 0), ModelARotationSettings.R1U(1, 1));
        var d = new ModelADynamics(0, p);
        var stages = new List<double>();
        SpinRk4Integrator.Step(0, .9, initial, (time, s) =>
        {
            stages.Add(s.AngularVelocity.X); return d.Evaluate(time, s);
        });
        Assert.That(stages[2], Is.GreaterThan(stages[1]));
        Assert.That(stages.All(x => x >= 0 && x <= initial.AngularVelocity.X), Is.True);
        Assert.DoesNotThrow(() => ModelASimulator.Simulate(new(initial, 0, .9, .9), p));
    }
}
