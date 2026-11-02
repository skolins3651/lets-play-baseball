using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class Rk4IntegratorTests
{
    private const double ManufacturedTolerance = 1e-13;

    [Test]
    public void ManufacturedExponentialUsesFourClassicalStagesAndWeightedCombination()
    {
        var initial = new FlightState(new Vector3d(1, 2, 3), new Vector3d(4, 5, 6));
        var calls = new List<(double Time, FlightState State)>();
        FlightDerivative Evaluate(double time, FlightState state)
        {
            calls.Add((time, state));
            return new FlightDerivative(state.Position.X, state.Position.Y, state.Position.Z,
                state.Velocity.X, state.Velocity.Y, state.Velocity.Z);
        }

        var result = Rk4Integrator.Step(0, 1, initial, Evaluate);

        Assert.That(calls.Count, Is.EqualTo(4));
        Assert.That(calls.Select(call => call.Time), Is.EqualTo(new[] { 0d, 0.5, 0.5, 1 }));
        var stageFactors = new[] { 1d, 1.5, 1.75, 2.75 };
        for (var stage = 0; stage < calls.Count; stage++)
        {
            AssertScaledState(calls[stage].State, stageFactors[stage]);
        }

        // R(1) = 1 + 1 + 1/2 + 1/6 + 1/24, independently derived for y' = y.
        AssertScaledState(result, 65d / 24);
    }

    [Test]
    public void StageTimesUseSuppliedEndpointsAndTheirMidpoint()
    {
        var times = new List<double>();
        Rk4Integrator.Step(3, 3.5, default, (time, _) =>
        {
            times.Add(time);
            return default;
        });

        Assert.That(times, Is.EqualTo(new[] { 3d, 3.25, 3.25, 3.5 }));
    }

    [TestCaseSource(nameof(NonfiniteRates))]
    public void EveryDerivativeComponentRejectsNonfiniteValues(int component, double value)
    {
        var rates = new double[6];
        rates[component] = value;
        var exception = Assert.Throws<ArithmeticException>(() => Rk4Integrator.Step(0, 1, default,
            (_, _) => new FlightDerivative(rates[0], rates[1], rates[2], rates[3], rates[4], rates[5])));

        Assert.That(exception!.Message, Does.Contain("k1 derivative").And.Contain("nonfinite"));
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void LaterStagesAlsoRejectNonfiniteDerivatives(int invalidStage)
    {
        var calls = 0;
        var exception = Assert.Throws<ArithmeticException>(() => Rk4Integrator.Step(0, 1, default, (_, _) =>
            ++calls == invalidStage ? new FlightDerivative(0, 0, 0, 0, 0, double.PositiveInfinity) : default));

        Assert.That(calls, Is.EqualTo(invalidStage));
        Assert.That(exception!.Message, Does.Contain($"k{invalidStage} derivative"));
    }

    [Test]
    public void ScaledDerivativeOverflowIsNumericalFailure()
    {
        var exception = Assert.Throws<ArithmeticException>(() => Rk4Integrator.Step(0, 4, default,
            (_, _) => new FlightDerivative(double.MaxValue, 0, 0, 0, 0, 0)));
        Assert.That(exception!.Message, Does.Contain("k2 state").And.Contain("position.X"));
    }

    [Test]
    public void IntermediateStateOverflowIsCheckedBeforePublicVectorConstruction()
    {
        var initial = new FlightState(new Vector3d(double.MaxValue, 0, 0), default);
        var exception = Assert.Throws<ArithmeticException>(() => Rk4Integrator.Step(0, 1, initial,
            (_, _) => new FlightDerivative(double.MaxValue, 0, 0, 0, 0, 0)));
        Assert.That(exception!.Message, Does.Contain("k2 state"));
    }

    [TestCase(double.MaxValue, 1e-308)]
    [TestCase(double.MaxValue / 4, 1)]
    public void WeightedProductOrSumOverflowFailsExplicitly(double rate, double duration)
    {
        var exception = Assert.Throws<ArithmeticException>(() => Rk4Integrator.Step(0, duration, default,
            (_, _) => new FlightDerivative(rate, 0, 0, 0, 0, 0)));
        Assert.That(exception!.Message, Does.Contain("weighted combination"));
    }

    [TestCase(48, false, "final increment")]
    [TestCase(12, true, "completed state")]
    public void FinalIncrementAndCompletedStateAreChecked(double duration, bool largeInitial, string expectedStage)
    {
        var initial = new FlightState(new Vector3d(largeInitial ? double.MaxValue : 0, 0, 0), default);
        var calls = 0;
        var exception = Assert.Throws<ArithmeticException>(() => Rk4Integrator.Step(0, duration, initial, (_, _) =>
            ++calls == 4 ? new FlightDerivative(double.MaxValue / 4, 0, 0, 0, 0, 0) : default));

        Assert.That(calls, Is.EqualTo(4));
        Assert.That(exception!.Message, Does.Contain(expectedStage));
    }

    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(-1, 1)]
    [TestCase(double.NaN, 1)]
    [TestCase(0, double.PositiveInfinity)]
    public void InvalidTimeIntervalsFailBeforeEvaluation(double start, double end)
    {
        var evaluated = false;
        Assert.Throws<ArithmeticException>(() => Rk4Integrator.Step(start, end, default, (_, _) =>
        {
            evaluated = true;
            return default;
        }));
        Assert.That(evaluated, Is.False);
    }

    [Test]
    public void FloatingPointTimeStagnationFailsExplicitly()
    {
        var start = Math.ScaleB(1, 53);
        var end = start + 1;
        Assert.That(end, Is.EqualTo(start));
        Assert.Throws<ArithmeticException>(() => Rk4Integrator.Step(start, end, default, (_, _) => default));
    }

    [TestCase(double.Epsilon)]
    [TestCase(2 * double.Epsilon)]
    public void RequiredStepCoefficientUnderflowFails(double duration)
    {
        var exception = Assert.Throws<ArithmeticException>(() => Rk4Integrator.Step(0, duration, default, (_, _) => default));
        Assert.That(exception!.Message, Does.Contain("half-step or sixth-step"));
    }

    [Test]
    public void AdjacentEndpointTimesWithoutInteriorMidpointFail()
    {
        var exception = Assert.Throws<ArithmeticException>(() =>
            Rk4Integrator.Step(1, Math.BitIncrement(1d), default, (_, _) => default));
        Assert.That(exception!.Message, Does.Contain("midpoint"));
    }

    [Test]
    public void OrdinaryStateRoundingDoesNotCauseFailure()
    {
        var state = new FlightState(new Vector3d(double.MaxValue / 4, 0, 0), default);
        var result = Rk4Integrator.Step(0, 1, state, (_, _) => new FlightDerivative(1, 0, 0, 0, 0, 0));
        Assert.That(result, Is.EqualTo(state));
    }

    private static IEnumerable<TestCaseData> NonfiniteRates()
    {
        for (var component = 0; component < 6; component++)
        {
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                yield return new TestCaseData(component, value);
            }
        }
    }

    private static void AssertScaledState(FlightState state, double factor)
    {
        var components = new[] { state.Position.X, state.Position.Y, state.Position.Z,
            state.Velocity.X, state.Velocity.Y, state.Velocity.Z };
        for (var i = 0; i < components.Length; i++)
        {
            Assert.That(components[i], Is.EqualTo((i + 1) * factor).Within(ManufacturedTolerance), $"component {i}");
        }
    }
}
