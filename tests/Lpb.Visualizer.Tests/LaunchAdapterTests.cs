using Lpb.Physics;
using Lpb.Visualizer;
using NUnit.Framework;

namespace Lpb.Visualizer.Tests;

internal static class Fixtures
{
    public static LaunchInput Default => new()
    {
        SpeedMph = 100, ElevationDegrees = 30, FieldAzimuthDegrees = 45, CenterHeightFeet = 3,
        GravityMagnitude = 9.80665, TimeStepSeconds = .01, DurationSeconds = 10,
        GroundElevationFeet = 0, BallRadiusInches = 1.45, Mode = "contact"
    };
    public static Experiment Run(LaunchInput? input = null) => Experiment.Run(LaunchAdapter.Convert(input ?? Default));
    public static LaunchInput Drag => Default with
    {
        PhysicalModel = "gravity-drag", BallMassGrams = 145, AirDensity = 1.225, DragCoefficient = .35
    };
}

public class LaunchAdapterTests
{
    [Test]
    public void ModelAndApplicableSiSettingsAreCapturedWithOneRadius()
    {
        var gravity = LaunchAdapter.Convert(Fixtures.Default);
        var explicitGravity = LaunchAdapter.Convert(Fixtures.Default with { PhysicalModel = "gravity-only" });
        Assert.That(gravity.Scenario.ModelKind, Is.EqualTo(FlightModelKind.GravityOnly));
        Assert.That(gravity.Scenario.Aerodynamics, Is.Null);
        Assert.That(gravity.Request.InitialState, Is.EqualTo(explicitGravity.Request.InitialState));
        var drag = LaunchAdapter.Convert(Fixtures.Drag with { GroundElevationFeet = 10, BallRadiusInches = 1.5 });
        var a = drag.Scenario.Aerodynamics!;
        Assert.That(drag.Scenario.ModelKind, Is.EqualTo(FlightModelKind.GravityAndDrag));
        Assert.That(a.BallMass, Is.EqualTo(.145));
        Assert.That(a.AirDensity, Is.EqualTo(1.225));
        Assert.That(a.DragCoefficient, Is.EqualTo(.35));
        Assert.That(a.BallRadius, Is.EqualTo(1.5 * .0254));
        Assert.That(a.BallRadius, Is.EqualTo(drag.Contact.BallRadius).And.EqualTo(drag.Scenario.BallRadiusMetres));
        Assert.That(drag.Request.InitialState, Is.EqualTo(LaunchAdapter.Convert(Fixtures.Default with { GroundElevationFeet = 10, BallRadiusInches = 1.5 }).Request.InitialState));
    }

    [TestCase(null)] [TestCase("")] [TestCase("drag")]
    public void InvalidPhysicalModelHasItsOwnFieldError(string? model)
    {
        var error = Assert.Throws<InputException>(() => LaunchAdapter.Convert(Fixtures.Default with { PhysicalModel = model! }));
        Assert.That(error!.Errors.Keys, Is.EqualTo(new[] { "physicalModel" }));
    }

    [TestCase("ballMassGrams")] [TestCase("airDensity")] [TestCase("dragCoefficient")]
    public void ApplicableAerodynamicInputsRequireValidExplicitValues(string field)
    {
        foreach (var value in new double?[] { null, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var input = WithAerodynamicValue(Fixtures.Drag, field, value);
            var error = Assert.Throws<InputException>(() => LaunchAdapter.Convert(input));
            Assert.That(error!.Errors.Keys, Is.EqualTo(new[] { field }));
        }
        if (field == "ballMassGrams")
            Assert.Throws<InputException>(() => LaunchAdapter.Convert(WithAerodynamicValue(Fixtures.Drag, field, 0)));
        else Assert.DoesNotThrow(() => LaunchAdapter.Convert(WithAerodynamicValue(Fixtures.Drag, field, 0)));
    }

    private static LaunchInput WithAerodynamicValue(LaunchInput input, string field, double? value) => field switch
    {
        "ballMassGrams" => input with { BallMassGrams = value },
        "airDensity" => input with { AirDensity = value },
        "dragCoefficient" => input with { DragCoefficient = value },
        _ => throw new ArgumentException(nameof(field))
    };

    [Test]
    public void InactiveAerodynamicInputsAreIgnoredAndExcludedFromExecutedSettings()
    {
        var converted = LaunchAdapter.Convert(Fixtures.Default with { BallMassGrams = double.NaN, AirDensity = -1, DragCoefficient = double.PositiveInfinity });
        Assert.That(converted.Scenario.Launch, Is.EqualTo(Fixtures.Default));
        Assert.That(converted.Scenario.Aerodynamics, Is.Null);
    }

    [Test]
    public void PositiveValuesLostDuringSiConversionAreRejectedByField()
    {
        var mass = Assert.Throws<InputException>(() => LaunchAdapter.Convert(Fixtures.Drag with { BallMassGrams = double.Epsilon }));
        Assert.That(mass!.Errors.Keys, Is.EqualTo(new[] { "ballMassGrams" }));
        foreach (var input in new[] { Fixtures.Default, Fixtures.Drag })
        {
            var radius = Assert.Throws<InputException>(() => LaunchAdapter.Convert(input with { BallRadiusInches = double.Epsilon }));
            Assert.That(radius!.Errors.Keys, Is.EqualTo(new[] { "ballRadiusInches" }));
        }
    }

    [Test]
    public void FriendlyUnitsAndGroundRelativeCenterMapToSi()
    {
        var converted = LaunchAdapter.Convert(Fixtures.Default with { GroundElevationFeet = 10, ElevationDegrees = 0, FieldAzimuthDegrees = 0 });
        Assert.That(converted.Request.InitialState.Position.Z, Is.EqualTo(3.9624).Within(1e-14));
        Assert.That(converted.Request.InitialState.Velocity.X, Is.EqualTo(44.704).Within(1e-12));
        Assert.That(converted.Contact.GroundElevation, Is.EqualTo(3.048).Within(1e-14));
        Assert.That(converted.Contact.BallRadius, Is.EqualTo(.03683).Within(1e-14));
        Assert.That(converted.Scenario.Launch, Is.EqualTo(Fixtures.Default with { GroundElevationFeet = 10, ElevationDegrees = 0, FieldAzimuthDegrees = 0 }));
    }

    [TestCase(0, 1, 0)]
    [TestCase(90, 0, 1)]
    [TestCase(-90, 0, -1)]
    [TestCase(180, -1, 0)]
    [TestCase(-180, -1, 0)]
    public void ExactAzimuthUsesExactDirectionAndPositiveZeros(double azimuth, double x, double y)
    {
        var launch = LaunchAdapter.Convert(Fixtures.Default with { ElevationDegrees = 0, FieldAzimuthDegrees = azimuth });
        Assert.That(launch.Scenario.Direction, Is.EqualTo(new Direction(x, y)));
        Assert.That(launch.Request.InitialState.Velocity, Is.EqualTo(new Vector3d(x == 0 ? 0 : x * 44.704, y == 0 ? 0 : y * 44.704, 0)));
        foreach (var value in new[] { launch.Request.InitialState.Velocity.X, launch.Request.InitialState.Velocity.Y, launch.Request.InitialState.Velocity.Z })
            if (value == 0) Assert.That(BitConverter.DoubleToInt64Bits(value), Is.Zero);
    }

    [TestCase(-90)]
    [TestCase(90)]
    public void VerticalLaunchHasExactlyZeroHorizontalVelocity(double elevation)
    {
        var velocity = LaunchAdapter.Convert(Fixtures.Default with { ElevationDegrees = elevation }).Request.InitialState.Velocity;
        Assert.That(BitConverter.DoubleToInt64Bits(velocity.X), Is.Zero);
        Assert.That(BitConverter.DoubleToInt64Bits(velocity.Y), Is.Zero);
        Assert.That(velocity.Z, Is.EqualTo(elevation > 0 ? 44.704 : -44.704));
    }

    [Test]
    public void ZeroSpeedHasPositiveZeroComponentsEvenWithNegativeDirections()
    {
        var velocity = LaunchAdapter.Convert(Fixtures.Default with { SpeedMph = 0, ElevationDegrees = -90, FieldAzimuthDegrees = -180 }).Request.InitialState.Velocity;
        Assert.That(new[] { velocity.X, velocity.Y, velocity.Z }.Select(BitConverter.DoubleToInt64Bits), Is.All.Zero);
    }

    [Test]
    public void CenterFieldAndNegativeQuadrantUseDocumentedFrame()
    {
        var center = LaunchAdapter.Convert(Fixtures.Default with { ElevationDegrees = 0 }).Request.InitialState.Velocity;
        Assert.That(center.X, Is.EqualTo(44.704 / Math.Sqrt(2)).Within(1e-12));
        Assert.That(center.Y, Is.EqualTo(center.X).Within(1e-12));
        var outside = LaunchAdapter.Convert(Fixtures.Default with { FieldAzimuthDegrees = -135 }).Request.InitialState.Velocity;
        Assert.That(outside.X, Is.Negative); Assert.That(outside.Y, Is.Negative);
    }

    [TestCase(-180)] [TestCase(-90)] [TestCase(0)] [TestCase(90)] [TestCase(180)]
    public void NearestNonCardinalAzimuthIsNotSnapped(double cardinal)
    {
        var angle = cardinal == 180 ? Math.BitDecrement(cardinal) : Math.BitIncrement(cardinal);
        var expected = Math.SinCos(angle * (Math.PI / 180));
        var launch = LaunchAdapter.Convert(Fixtures.Default with { ElevationDegrees = 0, FieldAzimuthDegrees = angle });
        Assert.That(launch.Scenario.Direction, Is.EqualTo(new Direction(expected.Cos, expected.Sin)));
        if (cardinal is -90 or 90) Assert.That(launch.Request.InitialState.Velocity.X, Is.Not.Zero);
        if (cardinal is -180 or 180) Assert.That(launch.Request.InitialState.Velocity.Y, Is.Not.Zero);
    }

    [TestCase(-90)] [TestCase(90)]
    public void NearestNonVerticalElevationRetainsHorizontalComponent(double cardinal)
    {
        var angle = cardinal < 0 ? Math.BitIncrement(cardinal) : Math.BitDecrement(cardinal);
        var velocity = LaunchAdapter.Convert(Fixtures.Default with { ElevationDegrees = angle }).Request.InitialState.Velocity;
        Assert.That(velocity.X, Is.Not.Zero); Assert.That(velocity.Y, Is.Not.Zero);
    }

    [Test]
    public void EveryNumericInputRejectsNonfiniteValues()
    {
        foreach (var property in typeof(LaunchInput).GetProperties().Where(p => p.PropertyType == typeof(double)))
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var input = Fixtures.Default;
            property.SetValue(input, value);
            Assert.Throws<InputException>(() => LaunchAdapter.Convert(input), property.Name);
        }
    }

    [Test]
    public void InvalidRangesAndAbsorbedRadiusAreRejected()
    {
        foreach (var input in new[] { Fixtures.Default with { SpeedMph = -1 }, Fixtures.Default with { ElevationDegrees = 91 },
                     Fixtures.Default with { FieldAzimuthDegrees = 181 }, Fixtures.Default with { GravityMagnitude = -1 },
                     Fixtures.Default with { TimeStepSeconds = 0 }, Fixtures.Default with { DurationSeconds = 0 },
                     Fixtures.Default with { BallRadiusInches = 0 }, Fixtures.Default with { Mode = "drag" },
                     Fixtures.Default with { GroundElevationFeet = 1e300 } })
            Assert.Throws<InputException>(() => LaunchAdapter.Convert(input));
    }

    [Test]
    public void UnconstrainedSignedHeightAndStepLargerThanDurationRemainValid()
    {
        var experiment = Fixtures.Run(Fixtures.Default with { Mode = "unconstrained", CenterHeightFeet = -3, TimeStepSeconds = 2, DurationSeconds = 1, GravityMagnitude = 0 });
        Assert.That(experiment.Result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
        Assert.That(experiment.Result.Samples[0].State.Position.Z, Is.Negative);
    }
}
