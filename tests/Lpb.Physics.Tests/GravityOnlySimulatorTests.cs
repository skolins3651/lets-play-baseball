using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class GravityOnlySimulatorTests
{
    // Absolute SI tolerances for these small analytical fixtures only, not every accepted input.
    private const double FixtureTolerance = 1e-9;

    [TestCase(0, 0, 20, 0, 0, 0, TestName = "CaseA_VerticalDrop")]
    [TestCase(0, 0, 20, 10, 0, 0, TestName = "CaseB_HorizontalLaunch")]
    [TestCase(0, 0, 0, 10, 0, 10, TestName = "CaseC_ObliqueLaunch")]
    [TestCase(0, 0, 0, 6, 8, 10, TestName = "CaseD_ThreeDimensionalLaunch")]
    public void CanonicalGravityFixturesMatchEveryAnalyticalSample(double x, double y, double z, double vx, double vy, double vz)
    {
        var initial = new FlightState(new Vector3d(x, y, z), new Vector3d(vx, vy, vz));
        var result = GravityOnlySimulator.Simulate(new FlightRequest(initial, 10, 2, 0.25));

        Assert.That(result.Samples.Count, Is.EqualTo(9));
        Assert.That(result.Samples[0], Is.EqualTo(new TrajectorySample(0, initial)));
        for (var i = 0; i < 9; i++)
        {
            var time = i / 4d;
            Assert.That(result.Samples[i].Time, Is.EqualTo(time));
            AssertAnalyticalState(result.Samples[i].State, initial, 10, time);
        }

        AssertCompletion(result, 2);
    }

    [Test]
    public void StandardGravityMatchesIndependentReference()
    {
        var initial = new FlightState(new Vector3d(0, 0, 20), default);
        var result = GravityOnlySimulator.Simulate(new FlightRequest(initial, 9.80665, 2, 0.25));

        foreach (var sample in result.Samples)
        {
            AssertAnalyticalState(sample.State, initial, 9.80665, sample.Time);
        }

        Assert.That(result.Samples[4].State.Position.Z, Is.EqualTo(15.096675).Within(FixtureTolerance));
        Assert.That(result.FinalState.Position.Z, Is.EqualTo(0.3867).Within(FixtureTolerance));
        Assert.That(result.FinalState.Velocity.Z, Is.EqualTo(-19.6133).Within(FixtureTolerance));
        AssertCompletion(result, 2);
    }

    [Test]
    public void ZeroGravityPreservesVelocityIncludingNegativeComponentsAndBelowGroundMotion()
    {
        var initial = new FlightState(new Vector3d(-1, -2, -3), new Vector3d(-4, 5, -6));
        var result = GravityOnlySimulator.Simulate(new FlightRequest(initial, 0, 2, 0.25));

        foreach (var sample in result.Samples)
        {
            AssertAnalyticalState(sample.State, initial, 0, sample.Time);
            Assert.That(sample.State.Velocity, Is.EqualTo(initial.Velocity));
            Assert.That(sample.State.Position.Z, Is.LessThan(0));
        }

        AssertCompletion(result, 2);
    }

    [Test]
    public void GravityDoesNotStopMotionAtOrBelowReferenceGround()
    {
        var initial = new FlightState(new Vector3d(0, 0, -1), new Vector3d(-2, -3, -4));
        var result = GravityOnlySimulator.Simulate(new FlightRequest(initial, 10, 2, 0.25));

        Assert.That(result.Samples.Count, Is.EqualTo(9));
        Assert.That(result.FinalState.Position.Z, Is.EqualTo(-29).Within(FixtureTolerance));
        AssertAnalyticalState(result.FinalState, initial, 10, 2);
        AssertCompletion(result, 2);
    }

    [Test]
    public void NondivisibleDurationUsesIndexedTimesAndShortenedFinalStep()
    {
        var initial = new FlightState(new Vector3d(1, 2, 3), new Vector3d(4, -5, 6));
        var result = GravityOnlySimulator.Simulate(new FlightRequest(initial, 10, 1, 0.3));
        var expectedTimes = new[] { 0d, 0.3, 0.6, 0.8999999999999999, 1 };

        Assert.That(result.Samples.Select(sample => sample.Time), Is.EqualTo(expectedTimes));
        foreach (var sample in result.Samples)
        {
            AssertAnalyticalState(sample.State, initial, 10, sample.Time);
        }

        Assert.That(result.Samples[^1].Time - result.Samples[^2].Time, Is.LessThan(0.3));
        AssertCompletion(result, 1);
    }

    [TestCase(2)]
    [TestCase(double.MaxValue)]
    public void TimeStepLargerThanDurationProducesOneShortenedStep(double step)
    {
        var initial = new FlightState(new Vector3d(1, 2, 3), new Vector3d(4, 5, 6));
        var result = GravityOnlySimulator.Simulate(new FlightRequest(initial, 10, 1, step));

        Assert.That(result.Samples.Count, Is.EqualTo(2));
        Assert.That(result.Samples[0], Is.EqualTo(new TrajectorySample(0, initial)));
        AssertAnalyticalState(result.FinalState, initial, 10, 1);
        AssertCompletion(result, 1);
    }

    [Test]
    public void MaximumPermittedStepsProduceMaximumSampleCount()
    {
        // The only full-limit simulation: about 11.2 MB for the source and copied sample arrays.
        var result = GravityOnlySimulator.Simulate(new FlightRequest(default, 0, 100000, 1));

        Assert.That(result.Samples.Count, Is.EqualTo(100001));
        Assert.That(result.Samples[0], Is.EqualTo(default(TrajectorySample)));
        for (var i = 1; i < result.Samples.Count; i++)
        {
            Assert.That(result.Samples[i].Time, Is.EqualTo((double)i));
            Assert.That(result.Samples[i].State, Is.EqualTo(default(FlightState)));
        }

        AssertCompletion(result, 100000);
    }

    [TestCase(100001, 1)]
    [TestCase(double.MaxValue, double.Epsilon)]
    public void ExcessiveRequestsFailAtSimulationBoundary(double duration, double step)
    {
        var request = new FlightRequest(default, 0, duration, step);
        Assert.Throws<InvalidOperationException>(() => GravityOnlySimulator.Simulate(request));
    }

    [Test]
    public void MaximumFiniteDurationAndTimestepCanSimulateStationaryState()
    {
        var result = GravityOnlySimulator.Simulate(new FlightRequest(default, 0, double.MaxValue, double.MaxValue));

        Assert.That(result.Samples.Count, Is.EqualTo(2));
        Assert.That(result.FinalState, Is.EqualTo(default(FlightState)));
        AssertCompletion(result, double.MaxValue);
    }

    [Test]
    public void NullRequestIsInvocationFailure()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => GravityOnlySimulator.Simulate(null!));
        Assert.That(exception!.ParamName, Is.EqualTo("request"));
    }

    [Test]
    public void LargeFiniteStateOverflowIsNumericalFailureWithStepContext()
    {
        var initial = new FlightState(new Vector3d(double.MaxValue, 0, 0), new Vector3d(double.MaxValue, 0, 0));
        var exception = Assert.Throws<ArithmeticException>(() => GravityOnlySimulator.Simulate(new FlightRequest(initial, 0, 1, 1)));

        Assert.That(exception!.Message, Does.Contain("Integration step 1").And.Contain("k2 state").And.Contain("position.X"));
    }

    [Test]
    public void LargeFiniteGravityCanFailConservativeWeightedArithmetic()
    {
        var exception = Assert.Throws<ArithmeticException>(() =>
            GravityOnlySimulator.Simulate(new FlightRequest(default, double.MaxValue, 1, 1)));
        Assert.That(exception!.Message, Does.Contain("weighted combination"));
    }

    [Test]
    public void FailureAfterAValidStepDoesNotReturnPartialSuccessfulResult()
    {
        SimulationResult? result = null;
        var request = new FlightRequest(default, 0, Math.BitIncrement(1d), 1);
        var exception = Assert.Throws<ArithmeticException>(() => result = GravityOnlySimulator.Simulate(request));

        Assert.That(result, Is.Null);
        Assert.That(exception!.Message, Does.Contain("Integration step 2").And.Contain("midpoint"));
        Assert.That(exception.InnerException, Is.TypeOf<ArithmeticException>());
    }

    [Test]
    public void CoefficientUnderflowIsExecutionFailureWithoutChangingRequestValidation()
    {
        var request = new FlightRequest(default, 0, double.Epsilon, double.Epsilon);
        var exception = Assert.Throws<ArithmeticException>(() => GravityOnlySimulator.Simulate(request));
        Assert.That(exception!.Message, Does.Contain("half-step or sixth-step"));
    }

    private static void AssertCompletion(SimulationResult result, double duration)
    {
        Assert.That(result.FinalTime, Is.EqualTo(duration));
        Assert.That(result.FinalTime, Is.EqualTo(result.Samples[^1].Time));
        Assert.That(result.FinalState, Is.EqualTo(result.Samples[^1].State));
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
        for (var i = 1; i < result.Samples.Count; i++)
        {
            Assert.That(result.Samples[i].Time, Is.GreaterThan(result.Samples[i - 1].Time).And.LessThanOrEqualTo(duration));
        }
    }

    private static void AssertAnalyticalState(FlightState actual, FlightState initial, double gravity, double time)
    {
        var expected = new[]
        {
            initial.Position.X + initial.Velocity.X * time,
            initial.Position.Y + initial.Velocity.Y * time,
            initial.Position.Z + initial.Velocity.Z * time - 0.5 * gravity * time * time,
            initial.Velocity.X,
            initial.Velocity.Y,
            initial.Velocity.Z - gravity * time
        };
        var components = new[] { actual.Position.X, actual.Position.Y, actual.Position.Z,
            actual.Velocity.X, actual.Velocity.Y, actual.Velocity.Z };
        for (var i = 0; i < components.Length; i++)
        {
            Assert.That(components[i], Is.EqualTo(expected[i]).Within(FixtureTolerance), $"t={time:R}, component {i}");
        }
    }
}
