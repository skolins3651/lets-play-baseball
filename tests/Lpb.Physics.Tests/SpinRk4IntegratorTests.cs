using NUnit.Framework;
using Q = Lpb.Physics.Tests.SpinReference.Rational;
using I = Lpb.Physics.Tests.SpinReference.Interval;

namespace Lpb.Physics.Tests;

public class SpinRk4IntegratorTests
{
    private static readonly string[] Names =
    [
        "position.X", "position.Y", "position.Z", "velocity.X", "velocity.Y", "velocity.Z",
        "angularVelocity.X", "angularVelocity.Y", "angularVelocity.Z"
    ];

    private const double UnitRoundoff = 1.1102230246251565e-16;
    private static SpinFlightState Initial => State(Enumerable.Range(1, 9).Select(x => (double)x).ToArray());

    [Test]
    public void NineComponentExponentialUsesFourCompleteClassicalStagesAndWeights()
    {
        var calls = new List<(double Time, SpinFlightState State)>();
        var result = SpinRk4Integrator.Step(0, 1, Initial, (time, state) =>
        {
            calls.Add((time, state));
            return Derivative(Components(state));
        });
        Assert.That(calls.Count, Is.EqualTo(4));
        Assert.That(calls.Select(c => c.Time), Is.EqualTo(new[] { 0d, .5, .5, 1 }));
        var factors = new[] { 1d, 1.5, 1.75, 2.75 };
        for (var stage = 0; stage < 4; stage++)
        {
            var values = Components(calls[stage].State);
            for (var i = 0; i < 9; i++)
                Assert.That(values[i], Is.EqualTo((i + 1) * factors[stage]), $"k{stage + 1} {Names[i]}");
        }
        // Independent stability polynomial R(1)=1+1+1/2+1/6+1/24=65/24.
        var completed = Components(result);
        for (var i = 0; i < 9; i++)
            Assert.That(completed[i], Is.EqualTo((i + 1) * (65d / 24)).Within(32 * UnitRoundoff * (i + 1)), Names[i]);
    }

    [Test]
    public void ZeroAndConstantNineComponentDerivativesAdvanceExactlyOnDyadicFixtures()
    {
        Assert.That(SpinRk4Integrator.Step(2, 2.5, Initial, (_, _) => default), Is.EqualTo(Initial));
        var rates = Enumerable.Range(1, 9).Select(i => i / 8d).ToArray();
        var result = SpinRk4Integrator.Step(2, 2.5, Initial, (_, _) => Derivative(rates));
        Assert.That(Components(result), Is.EqualTo(Enumerable.Range(1, 9).Select(i => i * 17d / 16)));
    }

    [Test]
    public void ShortenedIntervalUsesTheActualEndpointsForEveryStageAndComponent()
    {
        var times = new List<double>();
        var result = SpinRk4Integrator.Step(3, 3.125, Initial, (time, _) =>
        {
            times.Add(time);
            return Derivative(Enumerable.Range(1, 9).Select(i => 8d * i).ToArray());
        });
        Assert.That(times, Is.EqualTo(new[] { 3d, 3.0625, 3.0625, 3.125 }));
        Assert.That(Components(result), Is.EqualTo(Enumerable.Range(1, 9).Select(i => 2d * i)));
    }

    [Test]
    public void NonautonomousCubicRatesHaveTheIndependentExactIntegral()
    {
        const double start = 1, end = 1.125;
        var result = SpinRk4Integrator.Step(start, end, Initial, (time, _) =>
            Derivative(Enumerable.Range(1, 9).Select(i => i * time * time * time).ToArray()));
        var integral = (end * end * end * end - start * start * start * start) / 4;
        var values = Components(result);
        for (var i = 0; i < 9; i++)
        {
            var expected = (i + 1) * (1 + integral);
            Assert.That(values[i], Is.EqualTo(expected).Within(32 * UnitRoundoff * expected), Names[i]);
        }
    }

    [Test]
    public void SmoothManufacturedSystemShowsFourthOrderGlobalConvergence()
    {
        var lambda = new[] { .5, .625, .75, .875, 1, -.5, -.625, -.75, -1 };
        var errors = new List<double>();
        var certifiedErrors = new List<I>();
        var observedErrors = new List<I>();
        foreach (var count in new[] { 16, 32, 64 })
        {
            var state = Initial;
            for (var i = 0; i < count; i++)
                state = SpinRk4Integrator.Step((double)i / count, (double)(i + 1) / count, state, (_, s) =>
                {
                    var values = Components(s);
                    return Derivative(values.Select((v, axis) => lambda[axis] * v).ToArray());
                });
            var completed = Components(state);
            errors.Add(completed.Select((v, i) => Math.Abs(v / (i + 1) - Math.Exp(lambda[i]))).Max());
            // The exact closed stability polynomial is independent of stage assembly.
            // Each step has fewer than 64 rounded operations per scalar dependency;
            // 9 > exp(2) bounds the absolute-coefficient propagation for |lambda|<=1.
            var rounding = SpinReference.Gamma(64 * count) * Q.From(9);
            for (var axis = 0; axis < 9; axis++)
            {
                var x = Q.From(lambda[axis]) / Q.From(count);
                var polynomial = Q.One + x + x * x / Q.From(2) + x * x * x / Q.From(6) +
                    x * x * x * x / Q.From(24);
                var power = Q.One;
                for (var step = 0; step < count; step++) power *= polynomial;
                var normalized = Q.From(completed[axis]) / Q.From(axis + 1);
                Assert.That((normalized - power).Abs().CompareTo(rounding), Is.LessThanOrEqualTo(0), Names[axis]);
                if (axis != 4) continue;
                var exactExponential = SpinReference.Point(1) /
                    (SpinReference.Point(1) - SpinReference.OneMinusExp(SpinReference.Point(1)));
                var methodError = exactExponential - I.Point(power);
                certifiedErrors.Add(new(methodError.Lower - rounding, methodError.Upper + rounding));
                observedErrors.Add(exactExponential - I.Point(normalized));
            }
        }
        // Certify the halving-ratio range from exp(1)-R(1/N)^N, including rounding,
        // rather than choosing it from observed solver errors. The h^4 leading
        // term gives ratio 16; these finite-N enclosures prove the 15..17 band.
        for (var i = 0; i < 2; i++)
        {
            Assert.That(certifiedErrors[i + 1].Lower.Sign, Is.GreaterThan(0));
            var predicted = certifiedErrors[i] / certifiedErrors[i + 1];
            var observed = observedErrors[i] / observedErrors[i + 1];
            Assert.That(predicted.Lower.CompareTo(Q.From(15)), Is.GreaterThan(0));
            Assert.That(predicted.Upper.CompareTo(Q.From(17)), Is.LessThan(0));
            Assert.That(observed.Lower.CompareTo(predicted.Lower), Is.GreaterThanOrEqualTo(0));
            Assert.That(observed.Upper.CompareTo(predicted.Upper), Is.LessThanOrEqualTo(0));
        }
        TestContext.Out.WriteLine($"Manufactured exponential max errors: {string.Join(", ", errors.Select(e => e.ToString("R")))}");
    }

    [TestCase(32)]
    [TestCase(64)]
    public void ManufacturedRotationsMatchTheAnalyticalSineCosineSolution(int count)
    {
        var state = Initial;
        for (var i = 0; i < count; i++)
            state = SpinRk4Integrator.Step((double)i / count, (double)(i + 1) / count, state, (_, s) =>
            {
                var x = Components(s);
                return new(-x[1], x[0], 0, -x[4], x[3], 0, -x[7], x[6], 0);
            });
        var original = Components(Initial);
        var norm = Math.Sqrt(original.Sum(x => x * x));
        // ||J||=1. The exponential-series local defect is <= exp(h)*h^5/120;
        // telescoping count steps gives exp(1)*h^4/120 times the initial norm.
        // The additional allowance bounds ordinary arithmetic, not a runtime policy.
        var bound = Math.Exp(1) * norm * (1 / (120 * Math.Pow(count, 4)) + 64 * count * UnitRoundoff);
        var result = Components(state);
        for (var group = 0; group < 3; group++)
        {
            var i = 3 * group;
            Assert.That(result[i], Is.EqualTo(original[i] * Math.Cos(1) - original[i + 1] * Math.Sin(1)).Within(bound));
            Assert.That(result[i + 1], Is.EqualTo(original[i] * Math.Sin(1) + original[i + 1] * Math.Cos(1)).Within(bound));
            Assert.That(result[i + 2], Is.EqualTo(original[i + 2]));
        }
    }

    private static IEnumerable<TestCaseData> NonfiniteDerivatives()
    {
        for (var stage = 1; stage <= 4; stage++)
            for (var component = 0; component < 9; component++)
                foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                    yield return new TestCaseData(stage, component, value);
    }

    [TestCaseSource(nameof(NonfiniteDerivatives))]
    public void EveryDerivativeComponentAtEveryStageHasContextualRejection(int stage, int component, double value)
    {
        var calls = 0;
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(0, 1, default, (_, _) =>
            ++calls == stage ? Rate(component, value) : default));
        Assert.That(calls, Is.EqualTo(stage));
        Assert.That(error!.Message, Does.Contain($"k{stage} derivative").And.Contain(Names[component]).And.Contain("nonfinite"));
    }

    private static IEnumerable<TestCaseData> StateOverflows()
    {
        for (var stage = 2; stage <= 4; stage++)
            for (var component = 0; component < 9; component++)
                yield return new TestCaseData(stage, component);
    }

    [TestCaseSource(nameof(StateOverflows))]
    public void EveryIntermediateStateComponentRejectsOverflowBeforeVectorConstruction(int stage, int component)
    {
        var initial = new double[9];
        initial[component] = double.MaxValue;
        var calls = 0;
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(0, 1, State(initial), (_, _) =>
            ++calls == stage - 1 ? Rate(component, double.MaxValue) : default));
        Assert.That(calls, Is.EqualTo(stage - 1));
        Assert.That(error!.Message, Does.Contain($"k{stage} state").And.Contain(Names[component]));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void EveryScaledIncrementRejectsOverflow(int component)
    {
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(0, 4, default,
            (_, _) => Rate(component, double.MaxValue)));
        Assert.That(error!.Message, Does.Contain("k2 state").And.Contain(Names[component]));
    }

    [TestCase(double.MaxValue, 1e-308)]
    [TestCase(double.MaxValue / 4, 1)]
    public void WeightedProductAndSumOverflowAreNotSilentlyReordered(double rate, double duration)
    {
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(0, duration, default,
            (_, _) => Rate(6, rate)));
        Assert.That(error!.Message, Does.Contain("weighted combination").And.Contain("angularVelocity.X"));
    }

    private static IEnumerable<TestCaseData> FinalOverflows()
    {
        for (var component = 0; component < 9; component++)
            foreach (var completed in new[] { false, true })
                yield return new TestCaseData(component, completed);
    }

    [TestCaseSource(nameof(FinalOverflows))]
    public void FinalIncrementAndCompletedStateCheckAllNineComponents(int component, bool completed)
    {
        var initial = new double[9];
        if (completed) initial[component] = double.MaxValue;
        var calls = 0;
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(0, completed ? 12 : 48, State(initial),
            (_, _) => ++calls == 4 ? Rate(component, double.MaxValue / 4) : default));
        Assert.That(calls, Is.EqualTo(4));
        Assert.That(error!.Message, Does.Contain(completed ? "completed state" : "final increment").And.Contain(Names[component]));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void ArithmeticEvaluatorFailuresRetainStageTimeAndInnerCause(int stage)
    {
        var cause = new ArithmeticException("fixture angularVelocity.Y failure");
        var calls = 0;
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(3, 3.125, default, (_, _) =>
            ++calls == stage ? throw cause : default));
        var time = stage == 1 ? 3d : stage == 4 ? 3.125 : 3.0625;
        Assert.That(error!.Message, Does.Contain($"k{stage} derivative at {time:R} s:").And.Contain("angularVelocity.Y"));
        Assert.That(error.InnerException, Is.SameAs(cause));
    }

    [Test]
    public void NonArithmeticEvaluatorFailuresAreNotReclassified()
    {
        var cause = new InvalidOperationException("fixture");
        Assert.That(Assert.Throws<InvalidOperationException>(() => SpinRk4Integrator.Step(0, 1, default,
            (_, _) => throw cause)), Is.SameAs(cause));
    }

    [Test]
    public void NullEvaluatorFailsAsAnArgumentError()
    {
        Assert.That(Assert.Throws<ArgumentNullException>(() => SpinRk4Integrator.Step(0, 1, default, null!))!.ParamName,
            Is.EqualTo("evaluate"));
    }

    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(-1, 1)]
    [TestCase(double.NaN, 1)]
    [TestCase(double.NegativeInfinity, 1)]
    [TestCase(double.PositiveInfinity, double.PositiveInfinity)]
    [TestCase(0, double.NaN)]
    [TestCase(0, double.NegativeInfinity)]
    [TestCase(0, double.PositiveInfinity)]
    [TestCase(double.MaxValue, double.MaxValue)]
    public void InvalidTimeIntervalsRejectBeforeEvaluation(double start, double end)
    {
        var calls = 0;
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(start, end, default, (_, _) =>
        {
            calls++;
            return default;
        }));
        Assert.That(calls, Is.Zero);
        Assert.That(error!.Message, Does.Contain("finite, progressing times"));
    }

    [Test]
    public void RoundedTimeStagnationRejects()
    {
        var start = Math.ScaleB(1, 53);
        Assert.That(start + 1, Is.EqualTo(start));
        Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(start, start + 1, default, (_, _) => default));
    }

    [Test]
    public void AdjacentTimesWithoutAnInteriorMidpointReject()
    {
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(1, Math.BitIncrement(1), default, (_, _) => default));
        Assert.That(error!.Message, Does.Contain("midpoint"));
    }

    [TestCase(double.Epsilon)]
    [TestCase(2 * double.Epsilon)]
    [TestCase(3 * double.Epsilon)]
    public void UnrepresentablePositiveStepCoefficientsReject(double duration)
    {
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(0, duration, default, (_, _) => default));
        Assert.That(error!.Message, Does.Contain("half-step or sixth-step"));
    }

    [Test]
    public void OrdinaryRoundingIsNotAnUnimplementedResolutionAcceptancePolicy()
    {
        var values = new double[9];
        values[6] = double.MaxValue / 4;
        var state = State(values);
        Assert.That(SpinRk4Integrator.Step(0, 1, state, (_, _) => Rate(6, 1)), Is.EqualTo(state));
        Assert.That(SpinRk4Integrator.Step(0, 4 * double.Epsilon, Initial, (_, _) => default), Is.EqualTo(Initial));
    }

    [TestCase(0, .35)]
    [TestCase(1.225, 0)]
    [TestCase(1.225, .35)]
    public void ZeroSpinR0TranslationMatchesTheUnchangedSixComponentIntegratorBitwise(double density, double drag)
    {
        var a = new AerodynamicSettings(.145, .0366, density, drag);
        var legacy = new GravityDragDynamics(9.80665, a);
        var coupled = new ModelADynamics(9.80665, ModelAParameters.NathanAsrB(a, ModelARotationSettings.R0()));
        var translation = new FlightState(new(1, -2, 3), new(3, -4, 12));
        var expected = Rk4Integrator.Step(3, 3.125, translation, legacy.Evaluate);
        var actual = SpinRk4Integrator.Step(3, 3.125, new(translation, default), coupled.Evaluate);
        var legacyValues = new[] { expected.Position.X, expected.Position.Y, expected.Position.Z,
            expected.Velocity.X, expected.Velocity.Y, expected.Velocity.Z };
        Assert.That(Components(actual).Take(6).Select(BitConverter.DoubleToUInt64Bits),
            Is.EqualTo(legacyValues.Select(BitConverter.DoubleToUInt64Bits)));
        Assert.That(actual.AngularVelocity, Is.EqualTo(default(Vector3d)));
    }

    [Test]
    public void NewMachineryAndDerivativeRemainInternalAndTyped()
    {
        foreach (var type in new[] { typeof(ModelADynamics), typeof(SpinRk4Integrator), typeof(SpinFlightDerivative) })
            Assert.That(type.IsNotPublic, Is.True);
        Assert.That(typeof(SpinFlightDerivative).GetProperties().Length, Is.EqualTo(9));
        Assert.That(typeof(SpinFlightDerivative).GetProperties().All(p => p.PropertyType == typeof(double)), Is.True);
        Assert.That(typeof(SpinFlightDerivative).CustomAttributes.Any(a =>
            a.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute"), Is.True);
    }

    [Test]
    public void ActualLaterStageMagnusFailureRetainsTheCompleteContextChain()
    {
        var parameters = ModelAParameters.NathanAsrB(new(1, 1, 1, 0), ModelARotationSettings.R0());
        var dynamics = new ModelADynamics(double.MaxValue, parameters);
        var state = new SpinFlightState(new(default, new(1, 0, 0)), new(1, 0, 0));
        // k1 is gyrospin with finite gravity. At k2, finite velocity.Z=-Max/2
        // makes the nonzero transverse Magnus Y acceleration overflow.
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(0, 1, state, dynamics.Evaluate));
        Assert.That(error!.Message, Does.Contain("k2 derivative at 0.5 s:").And.Contain("coupled dynamics")
            .And.Contain("Magnus").And.Contain("acceleration.Y"));
        Assert.That(error.InnerException!.InnerException!.InnerException, Is.TypeOf<ArithmeticException>());
    }

    private static double[] Components(SpinFlightState s) =>
    [
        s.Translation.Position.X, s.Translation.Position.Y, s.Translation.Position.Z,
        s.Translation.Velocity.X, s.Translation.Velocity.Y, s.Translation.Velocity.Z,
        s.AngularVelocity.X, s.AngularVelocity.Y, s.AngularVelocity.Z
    ];

    private static SpinFlightState State(double[] x) => new(new(new(x[0], x[1], x[2]), new(x[3], x[4], x[5])), new(x[6], x[7], x[8]));
    private static SpinFlightDerivative Derivative(double[] x) => new(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8]);
    private static SpinFlightDerivative Rate(int component, double value)
    {
        var x = new double[9];
        x[component] = value;
        return Derivative(x);
    }
}
