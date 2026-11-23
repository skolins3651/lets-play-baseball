using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class ModelAProvenanceTests
{
    [Test]
    public void ProvenanceRetainsOriginalImmutableGraphWithoutAttestingNumericalExecution()
    {
        var request = new ModelAFlightRequest(default, 0, 1, .3);
        var parameters = ModelAParameters.NathanAsrB(new(.145, .0366, 0, 0), ModelARotationSettings.R1U(.000078, 0));
        var source = new ModelAFlightProvenance(request, parameters);
        Assert.That(source.Request, Is.SameAs(request));
        Assert.That(source.Parameters, Is.SameAs(parameters));
        Assert.That(source.Parameters.Rotation.Kind, Is.EqualTo(ModelARotationKind.R1U));
        Assert.That(source.ModelIdentity, Is.EqualTo("model-a/nathan-asr-b-v1"));
        Assert.That(source.NumericalPolicyIdentity, Is.Null);
        Assert.That(ModelAFlightProvenance.ApprovedNumericalPolicyIdentity,
            Is.EqualTo("classical-rk4/indexed-v1;model-a-full-state-v1"));
    }

    [Test]
    public void ProvenanceAcceptsEquivalentValuesFromIndependentInputInstances()
    {
        var source = Source();
        Assert.DoesNotThrow(() => source.ValidateRequest(new(default, 0, 1, .25)));
        Assert.DoesNotThrow(() => source.ValidateParameters(ModelAParameters.NathanAsrB(
            new(.145, .0366, 1.225, .35), ModelARotationSettings.R1U(.000078, .009))));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)]
    public void RequestMatchingChecksEveryStateComponent(int component)
    {
        var components = new double[9];
        components[component] = double.Epsilon;
        var error = Assert.Throws<ArgumentException>(() => Source().ValidateRequest(
            new(SpinFlightContractTests.FromComponents(components), 0, 1, .25)));
        Assert.That(error!.ParamName, Is.EqualTo("request"));
    }

    [TestCase(1, 1, .25)] [TestCase(0, 2, .25)] [TestCase(0, 1, .5)]
    public void RequestMatchingChecksAllScalars(double gravity, double duration, double step) =>
        Assert.That(Assert.Throws<ArgumentException>(() => Source().ValidateRequest(new(default, gravity, duration, step)))!.ParamName,
            Is.EqualTo("request"));

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void ParameterMatchingChecksEachSuppliedValueExactly(int component)
    {
        var values = new[] { .145, .0366, 1.225, .35, .000078, .009 };
        values[component] = Math.BitIncrement(values[component]);
        var candidate = ModelAParameters.NathanAsrB(new(values[0], values[1], values[2], values[3]),
            ModelARotationSettings.R1U(values[4], values[5]));
        Assert.That(Assert.Throws<ArgumentException>(() => Source().ValidateParameters(candidate))!.ParamName,
            Is.EqualTo("parameters"));
    }

    [Test]
    public void MatchingDoesNotCollapseZeroBetaR1UIntoR0()
    {
        var aero = new AerodynamicSettings(.145, .0366, 0, 0);
        var source = new ModelAFlightProvenance(new(default, 0, 1, 1),
            ModelAParameters.NathanAsrB(aero, ModelARotationSettings.R1U(.000078, 0)));
        Assert.Throws<ArgumentException>(() => source.ValidateParameters(ModelAParameters.NathanAsrB(aero, ModelARotationSettings.R0())));
        Assert.DoesNotThrow(() => source.ValidateParameters(ModelAParameters.NathanAsrB(
            new(.145, .0366, 0, 0), ModelARotationSettings.R1U(.000078, 0))));
        Assert.Throws<ArgumentException>(() => source.ValidateParameters(ModelAParameters.NathanAsrB(
            new(.145, .0366, 0, 0), ModelARotationSettings.R1U(.000079, 0))));
    }

    [TestCase(0, 0, 0, .35)]
    [TestCase(1.225, 0, 0, 0)]
    [TestCase(0, .35, 1.225, 0)]
    [TestCase(1.225, .35, 2.45, .175)]
    public void EqualDerivedDragOrDisabledDynamicsDoNotMakeInputsEquivalent(double density, double drag,
        double otherDensity, double otherDrag)
    {
        var rotation = ModelARotationSettings.R0();
        var source = new ModelAFlightProvenance(new(default, 0, 1, 1),
            ModelAParameters.NathanAsrB(new(.145, .0366, density, drag), rotation));
        Assert.Throws<ArgumentException>(() => source.ValidateParameters(ModelAParameters.NathanAsrB(
            new(.145, .0366, otherDensity, otherDrag), rotation)));
    }

    [Test]
    public void NullInputsAreRejectedWithoutSuccessfulProvenance()
    {
        var source = Source();
        Assert.That(Assert.Throws<ArgumentNullException>(() => new ModelAFlightProvenance(null!, source.Parameters))!.ParamName,
            Is.EqualTo("request"));
        Assert.That(Assert.Throws<ArgumentNullException>(() => new ModelAFlightProvenance(source.Request, null!))!.ParamName,
            Is.EqualTo("parameters"));
        Assert.Throws<ArgumentNullException>(() => source.ValidateRequest(null!));
        Assert.Throws<ArgumentNullException>(() => source.ValidateParameters(null!));
    }

    private static ModelAFlightProvenance Source() => new(new(default, 0, 1, .25),
        ModelAParameters.NathanAsrB(new(.145, .0366, 1.225, .35), ModelARotationSettings.R1U(.000078, .009)));
}
