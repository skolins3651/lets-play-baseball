using NUnit.Framework;
using static Lpb.Physics.Tests.ModelAI3Fixtures;
using static Lpb.Physics.Tests.ModelAReferenceTests;

namespace Lpb.Physics.Tests;

public class ModelATrajectoryTests
{
    [Test]
    public void ZeroDensityTrajectoryMatchesGravityAndConservesMechanicalEnergy()
    {
        var initial = State(new(3, -1, -2), new(2, -3, 4), new(5, -6, 7));
        var request = new ModelAFlightRequest(initial, 8, 1, .125);
        var parameters = Baseball(density: 0);
        var result = ModelASimulator.Simulate(request, parameters);
        var gravity = GravityOnlySimulator.Simulate(request.TranslationRequest);
        foreach (var (sample, legacy) in result.Samples.Zip(gravity.Samples))
        {
            var t = sample.Time;
            AssertState(sample.State, [3 + 2 * t, -1 - 3 * t, -2 + 4 * t - 4 * t * t, 2, -3, 4 - 8 * t, 5, -6, 7], 1e-13);
            Assert.That(sample.Time, Is.EqualTo(legacy.Time));
            Assert.That(sample.State.Translation, Is.EqualTo(legacy.State));
            Assert.That(.5 * Dot(sample.State.Translation.Velocity, sample.State.Translation.Velocity)
                + 8 * sample.State.Translation.Position.Z, Is.EqualTo(-1.5).Within(1e-13));
        }
    }

    [Test]
    public void ZeroSpinTrajectoryMatchesIndependentObliqueDragReference()
    {
        var request = DragFixtures.Oblique();
        var a = DragFixtures.Baseball;
        var result = ModelASimulator.Simulate(new(new(request.InitialState, default),
            request.GravityMagnitude, request.Duration, request.TimeStep),
            ModelAParameters.NathanAsrB(a, ModelARotationSettings.R1U(.00007769448, .009)));
        var existing = GravityDragSimulator.Simulate(request, a);
        Assert.That(result.Samples.Select(s => new TrajectorySample(s.Time, s.State.Translation)),
            Is.EqualTo(existing.Samples));
        Assert.That(result.Samples.All(s => s.State.AngularVelocity == default), Is.True);
        using var reference = ReadReference("drag-reference.json");
        var recorded = result.Samples.ToDictionary(s => s.Time);
        foreach (var row in reference.RootElement.GetProperty("samples").EnumerateArray())
        {
            var time = row.GetProperty("time").GetDouble();
            var expected = row.GetProperty("state").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            var actual = Components(recorded[time].State);
            for (var i = 0; i < expected.Length; i++)
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(i < 3 ? 1e-8 : 1e-9), $"t={time}, component {i}");
        }
    }

    [Test]
    public void ZeroGravityZeroSpinDragFollowsAnalyticSpeedAndDisplacement()
    {
        var a = DragFixtures.Baseball;
        var initial = State(velocity: new(12, -16, 0));
        var p = ModelAParameters.NathanAsrB(a, ModelARotationSettings.R0());
        var result = ModelASimulator.Simulate(new(initial, 0, 1, .01), p);
        const double speed = 20;
        var k = a.AirDensity * Math.PI * a.BallRadius * a.BallRadius * a.DragCoefficient / (2 * a.BallMass);
        foreach (var s in result.Samples)
        {
            var decay = 1 + k * speed * s.Time;
            var traveled = Math.Log(decay) / k;
            AssertState(s.State, [.6 * traveled, -.8 * traveled, 0, 12 / decay, -16 / decay, 0, 0, 0, 0], 1e-9);
        }
    }

    [Test]
    public void TiltedMagnusCircleMatchesAnalyticPathThroughComponentReversal()
    {
        var origin = new Vector3d(2, -1, -3);
        var spin = new Vector3d(0, 3, 4);
        var initial = State(origin, new(1, 0, 0), spin);
        var a = new AerodynamicSettings(1, 1, 2 / Math.PI, 0);
        var p = ModelAParameters.NathanAsrB(a, ModelARotationSettings.R0());
        var frequency = a.AirDensity * Math.PI / 2 * .336 * (1 - Math.Exp(-6.041 * 5));
        var result = ModelASimulator.Simulate(new(initial, 0, 6, .02), p);
        var maxError = 0d;
        foreach (var sample in result.Samples)
        {
            var theta = frequency * sample.Time;
            var (sin, cos) = Math.SinCos(theta);
            var along = sin / frequency;
            var across = (1 - cos) / frequency;
            double[] expected = [origin.X + along, origin.Y + .8 * across, origin.Z - .6 * across,
                cos, .8 * sin, -.6 * sin, 0, 3, 4];
            AssertState(sample.State, expected, 1e-9);
            maxError = Math.Max(maxError, Components(sample.State).Zip(expected, (a, b) => Math.Abs(a - b)).Max());
            var v = sample.State.Translation.Velocity;
            Assert.That(Dot(v, v), Is.EqualTo(1).Within(1e-9));
            Assert.That(Dot(v, spin), Is.EqualTo(0).Within(1e-9));
            Assert.That(sample.State.AngularVelocity, Is.EqualTo(spin));
        }
        Assert.That(result.Samples[1].State.Translation.Velocity.Y, Is.Positive);
        Assert.That(result.Samples[1].State.Translation.Velocity.Z, Is.Negative);
        Assert.That(result.FinalState.Translation.Velocity.X, Is.Negative);
        Assert.That(result.Samples.Max(s => s.State.Translation.Position.X),
            Is.GreaterThan(result.FinalState.Translation.Position.X));
        TestContext.Out.WriteLine(FormattableString.Invariant($"I4 tilted circle maximum component error {maxError:E6}"));
    }

    [TestCase(0d)]
    [TestCase(.5)]
    public void PrescribedLinearSpeedAndDensityUseTheirAnalyticDampingIntegral(double densitySlope)
    {
        // Verification-only evaluator: U(t)=2(1+.25t), rho(t)=.75(1+bt).
        const double speedSlope = .25, duration = .25, h = 1d / 512, radius = .5, inertia = 1, beta = .125;
        var initialSpin = new Vector3d(2, -3, 0);
        var current = State(spin: initialSpin);
        for (var i = 0; i < duration / h; i++)
        {
            current = SpinRk4Integrator.Step(i * h, (i + 1) * h, current, (t, state) =>
            {
                var rotation = ModelARotationKernel.R1U(new(1, radius, .75 * (1 + densitySlope * t), 0), inertia, beta);
                var rate = rotation.Evaluate(new(2 * (1 + speedSlope * t), 0, 0), state.AngularVelocity);
                return new(0, 0, 0, 0, 0, 0, rate.X, rate.Y, rate.Z);
            });
            var time = (i + 1) * h;
            var integral = 1.5 * (time + (speedSlope + densitySlope) * time * time / 2
                + speedSlope * densitySlope * time * time * time / 3);
            var decay = Math.Exp(-Math.PI * Math.Pow(radius, 4) * beta * integral / inertia);
            Assert.That(current.AngularVelocity.X, Is.EqualTo(initialSpin.X * decay).Within(2e-12));
            Assert.That(current.AngularVelocity.Y, Is.EqualTo(initialSpin.Y * decay).Within(2e-12));
            Assert.That(current.AngularVelocity.Z, Is.Zero);
        }
    }

    private static void AssertState(SpinFlightState state, double[] expected, double tolerance)
    {
        var actual = Components(state);
        for (var i = 0; i < expected.Length; i++)
            Assert.That(actual[i], Is.EqualTo(expected[i]).Within(tolerance), $"Component {i}");
    }

    private static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}
