using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class GroundContactSettingsTests
{
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ElevationMustBeFinite(double value) =>
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new GroundContactSettings(value, 1))!.ParamName,
            Is.EqualTo("groundElevation"));

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void RadiusMustBeFiniteAndPositive(double value) =>
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new GroundContactSettings(0, value))!.ParamName,
            Is.EqualTo("ballRadius"));

    [TestCase(double.MaxValue, double.MaxValue)]
    [TestCase(9007199254740992d, 1)]
    [TestCase(-9007199254740992d, 0.25)]
    public void RejectsOverflowAndAbsorbedRadius(double ground, double radius) =>
        Assert.That(Assert.Throws<ArgumentException>(() => new GroundContactSettings(ground, radius))!.ParamName,
            Is.EqualTo("ballRadius"));

    [TestCase(9007199254740992d, 2)]
    [TestCase(-9007199254740992d, 1)]
    [TestCase(0, double.Epsilon)]
    [TestCase(-0.05, 0.05)]
    public void PreservesRepresentableGeometry(double ground, double radius)
    {
        var contact = new GroundContactSettings(ground, radius);
        Assert.That(contact.GroundElevation, Is.EqualTo(ground));
        Assert.That(contact.BallRadius, Is.EqualTo(radius));
        Assert.That(contact.ContactHeight, Is.EqualTo(ground + radius).And.GreaterThan(ground));
        foreach (var property in typeof(GroundContactSettings).GetProperties())
            Assert.That(property.SetMethod, Is.Null);
    }
}
