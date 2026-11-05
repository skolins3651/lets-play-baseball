using Lpb.Physics;
using Lpb.Visualizer;
using NUnit.Framework;

namespace Lpb.Visualizer.Tests;

public class ExperimentTests
{
    [TestCase("contact")]
    [TestCase("unconstrained")]
    public void BothModesMatchTheAuthoritativePhysicsApi(string mode)
    {
        var launch = LaunchAdapter.Convert(Fixtures.Default with { Mode = mode });
        var expected = mode == "contact" ? GravityOnlySimulator.SimulateUntilGroundContact(launch.Request, launch.Contact) : GravityOnlySimulator.Simulate(launch.Request);
        var actual = Experiment.Run(launch);
        Assert.That(actual.Result.Samples, Is.EqualTo(expected.Samples));
        Assert.That(actual.Measurements.HorizontalDisplacementMetres, Is.EqualTo(TrajectoryMeasurements.GetHorizontalDisplacement(expected)));
        Assert.That(actual.Landmarks.Maximum, Is.EqualTo(PlotPoint.From(GravityOnlyTrajectory.GetMaximumHeight(expected, launch.Request))));
    }

    [TestCase(0)] [TestCase(-90)]
    public void ExactTouchingAtRestOrDownwardIsOnePoint(double elevation)
    {
        var experiment = Fixtures.Run(Fixtures.Default with { CenterHeightFeet = 1d / 12, BallRadiusInches = 1, ElevationDegrees = elevation, SpeedMph = elevation == 0 ? 0 : 1 });
        Assert.That(experiment.Result.Samples, Has.Count.EqualTo(1));
        var response = experiment.Display(Guid.NewGuid(), 300);
        Assert.That(response.ActualElapsedSeconds, Is.Zero);
        Assert.That(response.Points, Has.Length.EqualTo(1));
        Assert.That(response.Landmarks.Contact, Is.EqualTo(response.Landmarks.Launch));
        Assert.That(response.Landmarks.Maximum, Is.EqualTo(response.Landmarks.Launch));
    }

    [Test]
    public void ExactTouchingUpwardLaunchesAndPenetrationRejects()
    {
        var input = Fixtures.Default with { CenterHeightFeet = 1d / 12, BallRadiusInches = 1, ElevationDegrees = 90, SpeedMph = 1 };
        Assert.That(Fixtures.Run(input).Result.FinalTime, Is.GreaterThan(0));
        Assert.Throws<InputException>(() => Fixtures.Run(input with { CenterHeightFeet = Math.BitDecrement(input.CenterHeightFeet) }));
    }

    [Test]
    public void DurationTerminalDoesNotInventLandingOrFutureMaximum()
    {
        var result = Fixtures.Run(Fixtures.Default with { DurationSeconds = .1 });
        Assert.That(result.Landmarks.Contact, Is.Null);
        Assert.That(result.Result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
        Assert.That(result.Landmarks.Maximum, Is.EqualTo(result.Landmarks.Terminal));
    }

    [Test]
    public void NonzeroGroundSeparatesFieldZFromHeightAndContactCenter()
    {
        var result = Fixtures.Run(Fixtures.Default with { GroundElevationFeet = 10, BallRadiusInches = 1.5 });
        Assert.That(result.Landmarks.Launch.ZMetres / .3048, Is.EqualTo(13).Within(1e-12));
        Assert.That((result.Landmarks.Launch.ZMetres - result.Scenario.GroundElevationMetres) / .3048, Is.EqualTo(3).Within(1e-12));
        Assert.That(result.Landmarks.Contact!.ZMetres / .3048, Is.EqualTo(10.125).Within(1e-12));
        Assert.That((result.Landmarks.Contact.ZMetres - result.Scenario.GroundElevationMetres) / .3048, Is.EqualTo(.125).Within(1e-12));
        var below = Fixtures.Run(Fixtures.Default with { GroundElevationFeet = 10, Mode = "unconstrained", SpeedMph = 0, DurationSeconds = 2 });
        Assert.That(below.Landmarks.Terminal.ZMetres, Is.LessThan(below.Scenario.GroundElevationMetres));
        Assert.That(below.Landmarks.Contact, Is.Null);
    }

    [Test]
    public void NumericalMaximumFailureDoesNotUseRecordedMaximumOrHideSuccess()
    {
        var request = new FlightRequest(new FlightState(new Vector3d(0, 0, 1073741824), new Vector3d(0, 0, .007603029409634734)),
            242.45617275970022, 7.839591505441008e-5, 7.839591505441008e-5);
        var result = GravityOnlySimulator.Simulate(request);
        var scenario = LaunchAdapter.Convert(Fixtures.Default).Scenario with { InitialState = request.InitialState };
        var experiment = Experiment.Analyze(request, result, scenario);
        Assert.That(experiment.Landmarks.Maximum, Is.Null);
        Assert.That(experiment.Measurements.MaximumHeightAboveGroundMetres, Is.Null);
        Assert.That(experiment.Warnings.Single(), Does.Contain("Maximum").And.Contain("precision"));
        Assert.That(experiment.Display(Guid.NewGuid(), 2).Points, Has.Length.EqualTo(2));
    }

    [Test]
    public void HorizontalMagnitudeOverflowIsAnOptionalLimitation()
    {
        var request = new FlightRequest(new FlightState(default, new Vector3d(1e307, 1e307, 0)), 0, 14, 1);
        var result = GravityOnlySimulator.Simulate(request);
        var experiment = Experiment.Analyze(request, result, LaunchAdapter.Convert(Fixtures.Default).Scenario);
        Assert.That(experiment.Measurements.HorizontalDisplacementMetres, Is.Not.Null);
        Assert.That(experiment.Measurements.HorizontalDistanceMetres, Is.Null);
        Assert.That(experiment.Warnings, Has.Some.Contains("Horizontal distance"));
    }

    [TestCase(2)] [TestCase(300)] [TestCase(2000)]
    public void SamplingPreservesEndpointsLandmarksAndOriginalResult(int count)
    {
        var experiment = Fixtures.Run(Fixtures.Default with { TimeStepSeconds = .25 });
        var original = experiment.Result.Samples.ToArray();
        var response = experiment.Display(Guid.NewGuid(), count);
        Assert.That(response.Points, Has.Length.EqualTo(count));
        Assert.That(response.Points[0], Is.EqualTo(experiment.Landmarks.Launch));
        Assert.That(response.Points[^1], Is.EqualTo(experiment.Landmarks.Terminal));
        Assert.That(response.Points.Zip(response.Points.Skip(1)), Has.All.Matches<(PlotPoint First, PlotPoint Second)>(pair => pair.First.TimeSeconds < pair.Second.TimeSeconds));
        foreach (var point in response.Points)
            Assert.That(point, Is.EqualTo(PlotPoint.From(new TrajectorySample(point.TimeSeconds, GravityOnlyTrajectory.Evaluate(experiment.Result, experiment.Request, point.TimeSeconds)))));
        Assert.That(experiment.Result.Samples, Is.EqualTo(original));
        Assert.That(response.Landmarks, Is.SameAs(experiment.Landmarks));
        Assert.That(response.ExecutedScenario, Is.SameAs(experiment.Scenario));
    }

    [Test]
    public void LargeSourceCanProduceSmallPayloadWithoutLosingMaximum()
    {
        var experiment = Fixtures.Run(Fixtures.Default with { Mode = "unconstrained", DurationSeconds = 1, TimeStepSeconds = .00001 });
        var response = experiment.Display(Guid.NewGuid(), 2);
        Assert.That(response.OriginalSampleCount, Is.EqualTo(100001));
        Assert.That(response.Points, Has.Length.EqualTo(2));
        Assert.That(response.Landmarks.Maximum, Is.Not.Null);
        Assert.That(response.Warnings, Does.Not.Contain(DisplaySampler.SmoothnessNote));
    }

    [Test]
    public void TinySuccessfulFlightFallsBackWithoutFabricatingInteriorPoints()
    {
        var experiment = Fixtures.Run(Fixtures.Default with { Mode = "unconstrained", GravityMagnitude = 0, SpeedMph = 0, DurationSeconds = 8 * double.Epsilon, TimeStepSeconds = 8 * double.Epsilon });
        var response = experiment.Display(Guid.NewGuid(), 300);
        Assert.That(response.SamplingSource, Is.EqualTo("Recorded-sample fallback"));
        Assert.That(response.Points, Is.EqualTo(experiment.Result.Samples.Select(PlotPoint.From)));
        Assert.That(response.Warnings, Does.Contain(DisplaySampler.SmoothnessNote));
        Assert.That(response.Warnings, Has.Some.Contains("Display evaluation unavailable"));
    }

    [TestCase(-1)] [TestCase(0)] [TestCase(1)] [TestCase(2001)]
    public void DisplayCountIsBounded(int count) => Assert.Throws<InputException>(() => DisplaySampler.ValidateCount(count));

    [Test]
    public void NonrepresentableTimestampDensityIsDeduplicatedWithoutChangingEndpoints()
    {
        var times = DisplaySampler.Times(8 * double.Epsilon, 300);
        Assert.That(times, Is.EqualTo(Enumerable.Range(0, 9).Select(i => i * double.Epsilon)));
        Assert.That(DisplaySampler.Times(0, 300), Is.EqualTo(new[] { 0d }));
        var large = DisplaySampler.Times(double.MaxValue, 2000);
        Assert.That(large, Has.Count.EqualTo(2000));
        Assert.That(large[^1], Is.EqualTo(double.MaxValue));
        Assert.That(large, Is.All.Matches<double>(double.IsFinite));
    }

    [Test]
    public void ConstantHeightMaximumIsEarliestRecordedState()
    {
        var experiment = Fixtures.Run(Fixtures.Default with { Mode = "unconstrained", GravityMagnitude = 0, ElevationDegrees = 0 });
        Assert.That(experiment.Landmarks.Maximum, Is.EqualTo(experiment.Landmarks.Launch));
        Assert.That(experiment.Measurements.MaximumHeightAboveGroundMetres, Is.EqualTo(.9144).Within(1e-14));
    }
}
