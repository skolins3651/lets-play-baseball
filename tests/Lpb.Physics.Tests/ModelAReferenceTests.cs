using System.Globalization;
using System.Text.Json;
using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class ModelAReferenceTests
{
    private static readonly double[] Steps = [.04, .02, .01];
    private static readonly string[] StateNames = ["x", "y", "z", "vx", "vy", "vz", "wx", "wy", "wz"];
    private const double StateTolerance = 1e-9;

    [TestCase("transverse", "R0", -100, 150, 0)]
    [TestCase("transverse", "R1U", -100, 150, 0)]
    [TestCase("mixed", "R0", -50, 200, 80)]
    [TestCase("mixed", "R1U", -50, 200, 80)]
    [TestCase("initiallyGyro", "R0", 150, 100, 125)]
    [TestCase("initiallyGyro", "R1U", 150, 100, 125)]
    public void RecordedStateAndEnergyBalanceRefineAgainstIndependentReference(string name, string rotation,
        double wx, double wy, double wz)
    {
        using var document = ReadReference("model-a-reference.json");
        var root = document.RootElement;
        var input = root.GetProperty("inputs");
        var a = new AerodynamicSettings(.145, .0366, 1.225, .35);
        const double gravity = 9.80665;
        const double inertia = (.4 * .145) * (.0366 * .0366);
        const double beta = .009;
        const double duration = 2;
        var initial = new SpinFlightState(new(new(0, 0, 1.0366), new(30, 20, 25)), new(wx, wy, wz));
        Assert.That(new[] { a.BallMass, a.BallRadius, a.AirDensity, a.DragCoefficient, gravity, inertia, beta, duration },
            Is.EqualTo(new[] { "mass", "radius", "density", "drag", "gravity", "analyticalInertia", "beta", "duration" }
                .Select(property => input.GetProperty(property).GetDouble())));
        Assert.That(input.GetProperty("initialTranslation").EnumerateArray().Select(e => e.GetDouble()),
            Is.EqualTo(Components(initial).Take(6)));
        Assert.That(root.GetProperty("timesteps").EnumerateArray().Select(e => e.GetDouble()), Is.EqualTo(Steps));

        var scenario = root.GetProperty("scenarios").EnumerateArray().Single(s =>
            s.GetProperty("name").GetString() == name && s.GetProperty("rotation").GetString() == rotation);
        Assert.That(scenario.GetProperty("spin").EnumerateArray().Select(e => e.GetDouble()),
            Is.EqualTo(new[] { wx, wy, wz }));
        Assert.That(scenario.GetProperty("selectedPrecision").GetInt32(), Is.EqualTo(name == "initiallyGyro" ? 90 : 80));
        CheckStabilization(scenario, name == "initiallyGyro");
        var reference = scenario.GetProperty("samples").EnumerateArray()
            .Select(row => row.EnumerateArray().Select(e => e.GetDouble()).ToArray())
            .ToDictionary(row => row[0]);
        Assert.That(reference.Count, Is.EqualTo(201));
        Assert.That(reference.Values.All(row => row.Length == 12 && row.All(double.IsFinite)), Is.True);
        var damping = rotation == "R1U";
        var parameters = ModelAParameters.NathanAsrB(a, damping
            ? ModelARotationSettings.R1U(inertia, beta) : ModelARotationSettings.R0());
        var observations = Steps.Select(step => Measure(name, rotation, step, reference, parameters,
            new ModelAFlightRequest(initial, gravity, duration, step), inertia)).ToArray();

        for (var i = 1; i < observations.Length; i++)
        {
            var coarse = observations[i - 1];
            var fine = observations[i];
            var finalRatios = coarse.Final.Zip(fine.Final, (c, f) => c / f).Take(damping ? 3 : 2).ToArray();
            var recordedRatios = coarse.Maximum.Zip(fine.Maximum, (c, f) => c / f).Take(damping ? 3 : 2).ToArray();
            TestContext.Out.WriteLine(FormattableString.Invariant(
                $"I4 ratios {name}/{rotation} h={Steps[i - 1]:R}/{Steps[i]:R} max={Format(recordedRatios)} final={Format(finalRatios)}"));
            Assert.That(finalRatios, Has.All.InRange(12d, 20d), "Final position, velocity, and applicable spin ratios.");
            Assert.That(recordedRatios, Has.All.InRange(12d, 20d), "Maximum sampled errors under step refinement.");
            Assert.That(fine.TranslationBalance, Is.LessThan(coarse.TranslationBalance), "Translation energy residual.");
            if (damping)
                Assert.That(fine.RotationBalance, Is.LessThan(coarse.RotationBalance), "Rotation energy residual.");
        }
        if (name == "mixed" && damping)
            Assert.That(observations[0].Final[0], Is.GreaterThan(1e-8),
                "This accepted coarse flight exceeds the local absolute position floor in accumulated position error.");
    }

    private static Observation Measure(string name, string rotation, double step, Dictionary<double, double[]> reference,
        ModelAParameters parameters, ModelAFlightRequest request, double inertia)
    {
        var result = ModelASimulator.Simulate(request, parameters);
        Assert.That(result.Samples.Count, Is.EqualTo((int)Math.Round(request.Duration / step) + 1));
        Assert.That(result.FinalTime, Is.EqualTo(request.Duration));
        var maximum = new double[3];
        var final = new double[3];
        var translationBalance = 0d;
        var rotationBalance = 0d;
        var release = request.InitialState;
        var mass = parameters.Aerodynamics.BallMass;
        foreach (var sample in result.Samples)
        {
            Assert.That(reference.TryGetValue(sample.Time, out var expected), Is.True, $"Missing exact time {sample.Time:R}");
            var actual = Components(sample.State);
            Array.Clear(final);
            for (var i = 0; i < actual.Length; i++)
            {
                var error = Math.Abs(actual[i] - expected![i + 1]);
                if (step == .01)
                    Assert.That(error, Is.LessThanOrEqualTo(StateTolerance),
                        $"{name}/{rotation} t={sample.Time:R} {StateNames[i]}");
                final[i / 3] = Math.Max(final[i / 3], error);
                maximum[i / 3] = Math.Max(maximum[i / 3], error);
            }
            var translationChange = .5 * mass * DeltaSquaredNorm(sample.State.Translation.Velocity, release.Translation.Velocity)
                + mass * request.GravityMagnitude * (sample.State.Translation.Position.Z - release.Translation.Position.Z);
            var rotationChange = .5 * inertia * DeltaSquaredNorm(sample.State.AngularVelocity, release.AngularVelocity);
            translationBalance = Math.Max(translationBalance, Math.Abs(translationChange + expected![10]));
            rotationBalance = Math.Max(rotationBalance, Math.Abs(rotationChange + expected[11]));
            if (rotation == "R0")
                Assert.That(sample.State.AngularVelocity, Is.EqualTo(release.AngularVelocity));
        }
        var terminal = reference[result.FinalTime];
        Assert.That(terminal[10], Is.Positive);
        if (rotation == "R1U")
            Assert.That(terminal[11], Is.Positive);
        else
            Assert.That(rotationBalance, Is.Zero);
        TestContext.Out.WriteLine(FormattableString.Invariant(
            $"I4 accepted {name}/{rotation} h={step:R} max={Format(maximum)} final={Format(final)} energyJ={translationBalance:E6},{rotationBalance:E6}"));
        return new(maximum, final, translationBalance, rotationBalance);
    }

    private static void CheckStabilization(JsonElement scenario, bool gyro)
    {
        var comparisons = scenario.GetProperty("settingDifferences");
        Assert.That(comparisons.EnumerateObject().Select(p => p.Name),
            Is.EquivalentTo(gyro ? new[] { "60vs80", "80vs90" } : ["60vs80"]));
        foreach (var setting in comparisons.EnumerateObject())
        {
            var differences = setting.Value.EnumerateArray()
                .Select(x => double.Parse(x.GetString()!, CultureInfo.InvariantCulture)).ToArray();
            Assert.That(differences, Has.Length.EqualTo(11));
            Assert.That(differences, Has.All.InRange(0d, 1e-16));
        }
    }

    internal static JsonDocument ReadReference(string resource)
    {
        using var stream = typeof(ModelAReferenceTests).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded reference {resource}.");
        return JsonDocument.Parse(stream);
    }

    internal static double[] Components(SpinFlightState s)
    {
        var (x, v, w) = (s.Translation.Position, s.Translation.Velocity, s.AngularVelocity);
        return [x.X, x.Y, x.Z, v.X, v.Y, v.Z, w.X, w.Y, w.Z];
    }

    private static double DeltaSquaredNorm(Vector3d a, Vector3d b) =>
        (a.X - b.X) * (a.X + b.X) + (a.Y - b.Y) * (a.Y + b.Y) + (a.Z - b.Z) * (a.Z + b.Z);

    private static string Format(IEnumerable<double> values) =>
        string.Join(",", values.Select(x => x.ToString("E6", CultureInfo.InvariantCulture)));

    private sealed record Observation(double[] Maximum, double[] Final, double TranslationBalance, double RotationBalance);
}
