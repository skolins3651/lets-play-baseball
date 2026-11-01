using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class Vector3dTests
{
    [Test]
    public void ConstructionPreservesDoublePrecisionComponents()
    {
        var vector = new Vector3d(1.0000000000000002, -2.0000000000000004, 3.0000000000000004);

        Assert.That(vector.X, Is.EqualTo(1.0000000000000002));
        Assert.That(vector.Y, Is.EqualTo(-2.0000000000000004));
        Assert.That(vector.Z, Is.EqualTo(3.0000000000000004));
    }

    [Test]
    public void ConstructionAcceptsSmallAndLargeFiniteComponents()
    {
        var vector = new Vector3d(double.Epsilon, double.MaxValue, double.MinValue);

        Assert.That(vector.X, Is.EqualTo(double.Epsilon));
        Assert.That(vector.Y, Is.EqualTo(double.MaxValue));
        Assert.That(vector.Z, Is.EqualTo(double.MinValue));
    }

    [Test]
    public void DefaultAndParameterlessConstructionProduceZeroVectors()
    {
        Vector3d defaultVector = default;
        var parameterlessVector = new Vector3d();
        var constructedZero = new Vector3d(0, 0, 0);

        Assert.That(defaultVector.X, Is.Zero);
        Assert.That(defaultVector.Y, Is.Zero);
        Assert.That(defaultVector.Z, Is.Zero);
        Assert.That(parameterlessVector, Is.EqualTo(constructedZero));
        Assert.That(defaultVector, Is.EqualTo(constructedZero));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ConstructionRejectsNonfiniteX(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Vector3d(value, 2, 3));

        Assert.That(exception!.ParamName, Is.EqualTo("x"));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ConstructionRejectsNonfiniteY(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Vector3d(1, value, 3));

        Assert.That(exception!.ParamName, Is.EqualTo("y"));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ConstructionRejectsNonfiniteZ(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Vector3d(1, 2, value));

        Assert.That(exception!.ParamName, Is.EqualTo("z"));
    }
}
