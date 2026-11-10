using System.Text.Json;
using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class DragReferenceTests
{
    private static JsonDocument ReadReference()
    {
        using var stream = typeof(DragReferenceTests).Assembly.GetManifestResourceStream("drag-reference.json")!;
        return JsonDocument.Parse(stream);
    }

    [Test]
    public void RecordedAndInteriorStatesAgreeWithIndependentHighPrecisionMethod()
    {
        using var reference = ReadReference();
        var request = DragFixtures.Oblique();
        var result = GravityDragSimulator.Simulate(request, DragFixtures.Baseball);
        foreach (var item in reference.RootElement.GetProperty("samples").EnumerateArray())
        {
            var time = item.GetProperty("time").GetDouble();
            var actual = GravityDragTrajectory.Evaluate(result, request, DragFixtures.Baseball, time);
            AssertState(actual, item.GetProperty("state"), 1e-8, 1e-9);
        }
        // The independently generated apex is generally inside an ordinary numerical interval.
        var apex = reference.RootElement.GetProperty("events").GetProperty("apex");
        AssertState(GravityDragTrajectory.Evaluate(result, request, DragFixtures.Baseball, apex.GetProperty("time").GetDouble()),
            apex.GetProperty("state"), 1e-8, 1e-9);
    }

    [Test]
    public void ContactAndApexAgreeWithIndependentReferenceAndVacuumComparison()
    {
        using var reference = ReadReference();
        var request = DragFixtures.Oblique();
        var result = GravityDragSimulator.SimulateUntilGroundContact(request, DragFixtures.Baseball, 0);
        var contact = reference.RootElement.GetProperty("events").GetProperty("contact");
        Assert.That(result.FinalTime, Is.EqualTo(contact.GetProperty("time").GetDouble()).Within(1e-8));
        AssertState(result.FinalState, contact.GetProperty("state"), 1e-7, 1e-8);
        var expectedApex = reference.RootElement.GetProperty("events").GetProperty("apex");
        var apex = GravityDragTrajectory.GetMaximumHeight(result, request, DragFixtures.Baseball);
        Assert.That(apex.Time, Is.EqualTo(expectedApex.GetProperty("time").GetDouble()).Within(1e-8));
        Assert.That(apex.State.Position.Z, Is.EqualTo(expectedApex.GetProperty("state")[2].GetDouble()).Within(1e-8));
        var vacuum = GravityOnlySimulator.SimulateUntilGroundContact(request, new(0, DragFixtures.Baseball.BallRadius));
        Assert.That(result.FinalState.Position.X, Is.LessThan(vacuum.FinalState.Position.X));
        Assert.That(result.FinalState.Position.Y, Is.LessThan(vacuum.FinalState.Position.Y));
        Assert.That(TrajectoryMeasurements.GetHorizontalDisplacement(result).Z, Is.Zero);
        Assert.That(TrajectoryMeasurements.GetDisplacement(result), Is.EqualTo(new Vector3d(
            result.FinalState.Position.X, result.FinalState.Position.Y, result.FinalState.Position.Z - request.InitialState.Position.Z)));
    }

    [Test]
    public void FocusedSmoothFixtureHasFourthOrderRefinementAgainstIndependentOracle()
    {
        using var reference = ReadReference();
        var expected = reference.RootElement.GetProperty("samples").EnumerateArray().Single(x => x.GetProperty("time").GetDouble() == 4)
            .GetProperty("state").EnumerateArray().Select(x => x.GetDouble()).ToArray();
        var errors = new List<double>();
        foreach (var step in new[] { .04, .02, .01 })
        {
            var state = GravityDragSimulator.Simulate(DragFixtures.Oblique(4, step), DragFixtures.Baseball).FinalState;
            errors.Add(DragFixtures.Components(state).Take(3).Select((x, i) => Math.Abs(x - expected[i])).Max());
        }
        Assert.That(errors[0] / errors[1], Is.InRange(12d, 20d));
        Assert.That(errors[1] / errors[2], Is.InRange(12d, 20d));
    }

    [Test]
    public void MechanicalEnergyDissipatesInRepresentativeFlight()
    {
        var a = DragFixtures.Baseball;
        var request = DragFixtures.Oblique();
        var result = GravityDragSimulator.Simulate(request, a);
        double Energy(FlightState s) => .5 * a.BallMass *
            (s.Velocity.X * s.Velocity.X + s.Velocity.Y * s.Velocity.Y + s.Velocity.Z * s.Velocity.Z) +
            a.BallMass * request.GravityMagnitude * s.Position.Z;
        for (var i = 1; i < result.Samples.Count; i++)
            Assert.That(Energy(result.Samples[i].State), Is.LessThanOrEqualTo(Energy(result.Samples[i - 1].State) + 1e-10));
    }

    private static void AssertState(FlightState actual, JsonElement expected, double positionTolerance, double velocityTolerance)
    {
        var values = DragFixtures.Components(actual);
        for (var i = 0; i < 6; i++)
            Assert.That(values[i], Is.EqualTo(expected[i].GetDouble()).Within(i < 3 ? positionTolerance : velocityTolerance), $"component {i}");
    }
}
