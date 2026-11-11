using Lpb.Physics;
using Lpb.Visualizer;
using NUnit.Framework;

namespace Lpb.Visualizer.Tests;

public class ExperimentTests
{
    [TestCase("contact")]
    [TestCase("unconstrained")]
    public void ExactIllustrativeDragDefaultsProduceUsefulSuccessfulExperiment(string mode)
    {
        var experiment = Fixtures.Run(Fixtures.Drag with { Mode = mode });
        var response = experiment.Display(Guid.NewGuid(), 300);
        Assert.That(experiment.Result.ModelKind, Is.EqualTo(FlightModelKind.GravityAndDrag));
        Assert.That(experiment.Scenario.Aerodynamics!.BallMass, Is.EqualTo(.145));
        Assert.That(experiment.Scenario.Aerodynamics.BallRadius, Is.EqualTo(1.45 * .0254));
        Assert.That(experiment.Request.TimeStep, Is.EqualTo(.01));
        Assert.That(experiment.Result.CompletionReason, Is.EqualTo(mode == "contact" ? CompletionReason.GroundContact : CompletionReason.DurationReached));
        Assert.That(experiment.Result.FinalTime, Is.GreaterThan(1));
        Assert.That(experiment.Landmarks.Maximum, Is.Not.Null);
        Assert.That(experiment.Measurements.MaximumHeightAboveGroundMetres, Is.GreaterThan(.9144));
        Assert.That(response.SamplingSource, Is.EqualTo("Numerical evaluation"));
        Assert.That(response.Points, Has.Length.EqualTo(300));
        Assert.That(response.Warnings, Is.Empty);
        TestContext.Out.WriteLine($"{mode}: elapsed={response.ActualElapsedSeconds:R}s; maximum={response.Measurements.MaximumHeightAboveGroundMetres:R}m; recorded={response.OriginalSampleCount}; display={response.ActualDisplayPointCount}; source={response.SamplingSource}");
    }

    [TestCase("contact", false)]
    [TestCase("unconstrained", false)]
    [TestCase("contact", true)]
    [TestCase("unconstrained", true)]
    public void BothModesMatchTheAuthoritativePhysicsApi(string mode, bool drag)
    {
        var launch = LaunchAdapter.Convert((drag ? Fixtures.Drag : Fixtures.Default) with { Mode = mode });
        var a = launch.Scenario.Aerodynamics;
        var expected = drag
            ? mode == "contact" ? GravityDragSimulator.SimulateUntilGroundContact(launch.Request, a!, launch.Contact.GroundElevation) : GravityDragSimulator.Simulate(launch.Request, a!)
            : mode == "contact" ? GravityOnlySimulator.SimulateUntilGroundContact(launch.Request, launch.Contact) : GravityOnlySimulator.Simulate(launch.Request);
        var actual = Experiment.Run(launch);
        Assert.That(actual.Result.Samples, Is.EqualTo(expected.Samples));
        Assert.That(actual.Result.ModelKind, Is.EqualTo(expected.ModelKind).And.EqualTo(actual.Scenario.ModelKind));
        Assert.That(actual.Measurements.HorizontalDisplacementMetres, Is.EqualTo(TrajectoryMeasurements.GetHorizontalDisplacement(expected)));
        Assert.That(actual.Landmarks.Maximum, Is.EqualTo(PlotPoint.From(drag
            ? GravityDragTrajectory.GetMaximumHeight(expected, launch.Request, a!)
            : GravityOnlyTrajectory.GetMaximumHeight(expected, launch.Request))));
        foreach (var point in actual.Display(Guid.NewGuid(), 23).Points)
        {
            var state = drag ? GravityDragTrajectory.Evaluate(expected, launch.Request, a!, point.TimeSeconds)
                : GravityOnlyTrajectory.Evaluate(expected, launch.Request, point.TimeSeconds);
            Assert.That(point, Is.EqualTo(PlotPoint.From(new(point.TimeSeconds, state))));
        }
    }

    [TestCase("contact", true)] [TestCase("unconstrained", true)]
    [TestCase("contact", false)] [TestCase("unconstrained", false)]
    public void DisabledDragKeepsItsIdentitySettingsAndMatchingAnalysis(string mode, bool zeroDensity)
    {
        var input = Fixtures.Drag with { Mode = mode, AirDensity = zeroDensity ? 0 : 1.225, DragCoefficient = zeroDensity ? .35 : 0 };
        var drag = Fixtures.Run(input);
        var gravity = Fixtures.Run(input with { PhysicalModel = "gravity-only" });
        Assert.That(drag.Result.Samples, Is.EqualTo(gravity.Result.Samples));
        Assert.That(drag.Result.ModelKind, Is.EqualTo(FlightModelKind.GravityAndDrag));
        Assert.That(drag.Scenario.Model, Does.Contain("drag disabled by executed settings"));
        Assert.That(drag.Scenario.Launch, Is.EqualTo(input));
        Assert.That(drag.Landmarks.Maximum, Is.EqualTo(gravity.Landmarks.Maximum));
        Assert.That(drag.Display(Guid.NewGuid(), 37).Points, Is.EqualTo(gravity.Display(Guid.NewGuid(), 37).Points));
        Assert.Throws<ArgumentException>(() => GravityOnlyTrajectory.Evaluate(drag.Result, drag.Request, 0));
    }

    [Test]
    public void DragResamplingUsesRetainedContextAfterNewInputsAreAdapted()
    {
        var experiment = Fixtures.Run(Fixtures.Drag);
        var before = experiment.Display(Guid.NewGuid(), 17);
        var samples = experiment.Result.Samples.ToArray();
        LaunchAdapter.Convert(Fixtures.Drag with { BallMassGrams = 200, AirDensity = 0, DragCoefficient = .9 });
        LaunchAdapter.Convert(Fixtures.Default);
        var after = experiment.Display(before.ResultId, 47);
        Assert.That(after.ExecutedScenario, Is.SameAs(before.ExecutedScenario));
        Assert.That(after.Landmarks, Is.SameAs(before.Landmarks));
        Assert.That(after.Measurements, Is.SameAs(before.Measurements));
        Assert.That(experiment.Result.Samples, Is.EqualTo(samples));
        foreach (var point in after.Points)
            Assert.That(point, Is.EqualTo(PlotPoint.From(new(point.TimeSeconds,
                GravityDragTrajectory.Evaluate(experiment.Result, experiment.Request, experiment.Scenario.Aerodynamics!, point.TimeSeconds)))));
    }

    [Test]
    public void OptionalDragMaximumFailureRemainsUnavailableWithoutInvalidatingFlight()
    {
        var a = new AerodynamicSettings(Math.PI / 2, 1, 1e-110, 1);
        var request = new FlightRequest(new(new(0, 0, 1), new(0, 0, 1.1e-100)), 1e-200, 3e100, 3e100);
        var result = GravityDragSimulator.Simulate(request, a);
        var scenario = LaunchAdapter.Convert(Fixtures.Drag).Scenario with { Aerodynamics = a, InitialState = request.InitialState };
        var experiment = Experiment.Analyze(request, result, scenario);
        Assert.That(experiment.Landmarks.Maximum, Is.Null);
        Assert.That(experiment.Measurements.MaximumHeightAboveGroundMetres, Is.Null);
        Assert.That(experiment.Warnings, Has.Some.Contains("Maximum").And.Some.Contains("underflow"));
        Assert.That(experiment.Display(Guid.NewGuid(), 2).Points, Has.Length.EqualTo(2));
        Assert.That(experiment.Result, Is.SameAs(result));
    }

    [Test]
    public void DragDisplayFailureFallsBackToBoundedRecordedSamples()
    {
        var experiment = Fixtures.Run(Fixtures.Drag with { Mode = "unconstrained", GravityMagnitude = 0, SpeedMph = 0,
            DurationSeconds = 8 * double.Epsilon, TimeStepSeconds = 8 * double.Epsilon });
        var response = experiment.Display(Guid.NewGuid(), 300);
        Assert.That(response.SamplingSource, Is.EqualTo("Recorded-sample fallback"));
        Assert.That(response.Points, Is.EqualTo(experiment.Result.Samples.Select(PlotPoint.From)));
        Assert.That(response.Warnings, Has.Some.Contains("Display evaluation unavailable"));
        Assert.That(response.ExecutedScenario.ModelKind, Is.EqualTo(FlightModelKind.GravityAndDrag));
    }

    [Test]
    public void UnexpectedModelOrProvenanceMismatchIsNotInputValidation()
    {
        var launch = LaunchAdapter.Convert(Fixtures.Drag);
        var result = GravityDragSimulator.Simulate(launch.Request, launch.Scenario.Aerodynamics!);
        Assert.Throws<ArgumentException>(() => Experiment.Analyze(launch.Request, result, LaunchAdapter.Convert(Fixtures.Default).Scenario));
        Assert.Throws<ArgumentException>(() => Experiment.Analyze(launch.Request, result,
            launch.Scenario with { Aerodynamics = new(.2, launch.Contact.BallRadius, 1.225, .35) }));
    }

    [TestCase(0, false)] [TestCase(-90, false)]
    [TestCase(0, true)] [TestCase(-90, true)]
    public void ExactTouchingAtRestOrDownwardIsOnePoint(double elevation, bool drag)
    {
        var experiment = Fixtures.Run((drag ? Fixtures.Drag : Fixtures.Default) with { CenterHeightFeet = 1d / 12, BallRadiusInches = 1, ElevationDegrees = elevation, SpeedMph = elevation == 0 ? 0 : 1 });
        Assert.That(experiment.Result.Samples, Has.Count.EqualTo(1));
        var response = experiment.Display(Guid.NewGuid(), 300);
        Assert.That(response.ActualElapsedSeconds, Is.Zero);
        Assert.That(response.Points, Has.Length.EqualTo(1));
        Assert.That(response.Landmarks.Contact, Is.EqualTo(response.Landmarks.Launch));
        Assert.That(response.Landmarks.Maximum, Is.EqualTo(response.Landmarks.Launch));
    }

    [TestCase(false)] [TestCase(true)]
    public void ExactTouchingUpwardLaunchesAndPenetrationRejects(bool drag)
    {
        var input = (drag ? Fixtures.Drag : Fixtures.Default) with { CenterHeightFeet = 1d / 12, BallRadiusInches = 1, ElevationDegrees = 90, SpeedMph = 1 };
        Assert.That(Fixtures.Run(input).Result.FinalTime, Is.GreaterThan(0));
        Assert.Throws<InputException>(() => Fixtures.Run(input with { CenterHeightFeet = Math.BitDecrement(input.CenterHeightFeet) }));
    }

    [TestCase(false)] [TestCase(true)]
    public void DurationTerminalDoesNotInventLandingOrFutureMaximum(bool drag)
    {
        var result = Fixtures.Run((drag ? Fixtures.Drag : Fixtures.Default) with { DurationSeconds = .1 });
        Assert.That(result.Landmarks.Contact, Is.Null);
        Assert.That(result.Result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
        Assert.That(result.Landmarks.Maximum, Is.EqualTo(result.Landmarks.Terminal));
    }

    [TestCase(false)] [TestCase(true)]
    public void NonzeroGroundSeparatesFieldZFromHeightAndContactCenter(bool drag)
    {
        var result = Fixtures.Run((drag ? Fixtures.Drag : Fixtures.Default) with { GroundElevationFeet = 10, BallRadiusInches = 1.5 });
        Assert.That(result.Landmarks.Launch.ZMetres / .3048, Is.EqualTo(13).Within(1e-12));
        Assert.That((result.Landmarks.Launch.ZMetres - result.Scenario.GroundElevationMetres) / .3048, Is.EqualTo(3).Within(1e-12));
        Assert.That(result.Landmarks.Contact!.ZMetres / .3048, Is.EqualTo(10.125).Within(1e-12));
        Assert.That((result.Landmarks.Contact.ZMetres - result.Scenario.GroundElevationMetres) / .3048, Is.EqualTo(.125).Within(1e-12));
        var below = Fixtures.Run((drag ? Fixtures.Drag : Fixtures.Default) with { GroundElevationFeet = 10, Mode = "unconstrained", SpeedMph = 0, DurationSeconds = 2 });
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
