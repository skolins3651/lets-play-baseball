using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class ModelAParameterContractTests
{
    [Test]
    public void R0HasNoUnusedInertiaOrBetaAndR1UZeroBetaRemainsDistinct()
    {
        var r0 = ModelARotationSettings.R0();
        var r1 = ModelARotationSettings.R1U(.000078, 0);
        Assert.That(r0.Kind, Is.EqualTo(ModelARotationKind.R0));
        Assert.That(r0.RotationalInertia, Is.Null);
        Assert.That(r0.Beta, Is.Null);
        Assert.That(r1.Kind, Is.EqualTo(ModelARotationKind.R1U));
        Assert.That(r1.RotationalInertia, Is.EqualTo(.000078));
        Assert.That(r1.Beta, Is.Zero);
        Assert.That(r0.Equals(r1), Is.False);
        Assert.That(r1.Equals(ModelARotationSettings.R1U(.000078, 0)), Is.False);
    }

    [TestCase(double.Epsilon, 0)]
    [TestCase(double.Epsilon, double.Epsilon)]
    [TestCase(double.MaxValue, double.MaxValue)]
    public void R1UAcceptsExplicitFiniteExtremesWithoutComputingDynamics(double inertia, double beta)
    {
        var rotation = ModelARotationSettings.R1U(inertia, beta);
        Assert.That(rotation.RotationalInertia, Is.EqualTo(inertia));
        Assert.That(rotation.Beta, Is.EqualTo(beta));
    }

    [TestCase(0)] [TestCase(-1)] [TestCase(-double.Epsilon)] [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
    public void R1URejectsInvalidInertiaEvenWithZeroBeta(double value) =>
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => ModelARotationSettings.R1U(value, 0))!.ParamName,
            Is.EqualTo("rotationalInertia"));

    [TestCase(-1)] [TestCase(-double.Epsilon)] [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
    public void R1URejectsInvalidBeta(double value) =>
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => ModelARotationSettings.R1U(1, value))!.ParamName,
            Is.EqualTo("beta"));

    [TestCase(0, .35)] [TestCase(1.225, 0)] [TestCase(0, 0)] [TestCase(1.225, .35)]
    public void NamedFactoryRetainsOriginalSettingsAndPrescriptionWhenDragOrDensityIsZero(double density, double drag)
    {
        var aero = new AerodynamicSettings(.145, .0366, density, drag);
        var rotation = ModelARotationSettings.R1U(.000078, 0);
        var parameters = ModelAParameters.NathanAsrB(aero, rotation);
        Assert.That(parameters.Aerodynamics, Is.SameAs(aero));
        Assert.That(parameters.Rotation, Is.SameAs(rotation));
        Assert.That(parameters.Aerodynamics.AirDensity, Is.EqualTo(density));
        Assert.That(parameters.Aerodynamics.DragCoefficient, Is.EqualTo(drag));
        Assert.That(parameters.Rotation.Kind, Is.EqualTo(ModelARotationKind.R1U));
        Assert.That(parameters.PrescriptionIdentity, Is.EqualTo("model-a/nathan-asr-b-v1"));
        Assert.That(parameters.Equals(ModelAParameters.NathanAsrB(aero, rotation)), Is.False);
    }

    [Test]
    public void R0ParametersDoNotInferInertiaFromAerodynamicMassAndRadius()
    {
        var parameters = ModelAParameters.NathanAsrB(new(1, 2, 0, 0), ModelARotationSettings.R0());
        Assert.That(parameters.Rotation.RotationalInertia, Is.Null);
        Assert.That(parameters.Rotation.Beta, Is.Null);
    }

    [Test]
    public void NamedFactoryRejectsNullSettingsWithSpecificNames()
    {
        Assert.That(Assert.Throws<ArgumentNullException>(() =>
            ModelAParameters.NathanAsrB(null!, ModelARotationSettings.R0()))!.ParamName, Is.EqualTo("aerodynamics"));
        Assert.That(Assert.Throws<ArgumentNullException>(() =>
            ModelAParameters.NathanAsrB(new(1, 1, 0, 0), null!))!.ParamName, Is.EqualTo("rotation"));
    }
}
