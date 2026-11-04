using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class TrajectoryMeasurementsTests
{
    [Test]
    public void DisplacementAndLandingSemanticsAreExplicit()
    {
        var request = new FlightRequest(FlightFixtures.State(2, -1, 3, -4, 6, 8), 0, 3, .5);
        var result = GravityOnlySimulator.SimulateUntilGroundContact(request, FlightFixtures.ZeroPlane);
        Assert.That(result.FinalTime, Is.EqualTo(2));
        Assert.That(TrajectoryMeasurements.GetDisplacement(result), Is.EqualTo(new Vector3d(12, 16, -2)));
        Assert.That(TrajectoryMeasurements.GetHorizontalDisplacement(result), Is.EqualTo(new Vector3d(12, 16, 0)));
        var contact = TrajectoryMeasurements.GetGroundContact(result)!.Value;
        Assert.That(contact, Is.EqualTo(result.Samples[^1]));
        Assert.That(contact.State.Position, Is.EqualTo(new Vector3d(15, 12, 0)));
        Assert.That(contact.State.Position.Z, Is.Not.EqualTo(FlightFixtures.ZeroPlane.GroundElevation));
        Assert.That(TrajectoryMeasurements.GetGroundContact(GravityOnlySimulator.Simulate(request)), Is.Null);
    }

    [Test]
    public void MeasurementsCheckFiniteSubtractionsBeforeVectorConstruction()
    {
        var initial = new FlightState(new Vector3d(-double.MaxValue, 0, -double.MaxValue), default);
        var final = new FlightState(new Vector3d(double.MaxValue, 0, double.MaxValue), default);
        var result = new SimulationResult([new(0, initial), new(1, final)], 1);
        Assert.Throws<ArithmeticException>(() => TrajectoryMeasurements.GetDisplacement(result));
        Assert.Throws<ArithmeticException>(() => TrajectoryMeasurements.GetHorizontalDisplacement(result));
        var verticalOnly = new SimulationResult([new(0, FlightFixtures.State(-double.MaxValue, 0)), new(1, FlightFixtures.State(double.MaxValue, 0))], 1);
        Assert.Throws<ArithmeticException>(() => TrajectoryMeasurements.GetDisplacement(verticalOnly));
        Assert.That(TrajectoryMeasurements.GetHorizontalDisplacement(verticalOnly), Is.EqualTo(default(Vector3d)));
    }

    [Test]
    public void MeasurementsRejectNullResults()
    {
        Assert.Throws<ArgumentNullException>(() => TrajectoryMeasurements.GetDisplacement(null!));
        Assert.Throws<ArgumentNullException>(() => TrajectoryMeasurements.GetHorizontalDisplacement(null!));
        Assert.Throws<ArgumentNullException>(() => TrajectoryMeasurements.GetGroundContact(null!));
    }
}
