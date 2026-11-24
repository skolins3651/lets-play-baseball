using System.Reflection;
using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class ModelAPublicContractTests
{
    private static readonly Type[] NewContracts =
    [
        typeof(SpinFlightState), typeof(ModelAFlightRequest), typeof(ModelARotationSettings),
        typeof(ModelARotationKind), typeof(ModelAParameters), typeof(SpinTrajectorySample), typeof(ModelASimulationResult)
    ];

    [Test]
    public void ResultExposesOnlyTheFiveApprovedPropertiesAndNoPublicConstructor()
    {
        var properties = typeof(ModelASimulationResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.That(properties.Select(p => p.Name), Is.EquivalentTo(new[]
        {
            "Samples", "FinalState", "FinalTime", "CompletionReason", "ModelIdentity"
        }));
        Assert.That(typeof(ModelASimulationResult).GetConstructors(), Is.Empty);
        Assert.That(typeof(ModelAFlightProvenance).IsNotPublic, Is.True);
    }

    [Test]
    public void OnlyNamedFactoriesCanConstructParametersAndRotationSettings()
    {
        Assert.That(typeof(ModelAParameters).GetConstructors(), Is.Empty);
        Assert.That(typeof(ModelARotationSettings).GetConstructors(), Is.Empty);
        Assert.That(FactoryNames(typeof(ModelAParameters)), Is.EqualTo(new[] { "NathanAsrB" }));
        Assert.That(FactoryNames(typeof(ModelARotationSettings)), Is.EquivalentTo(new[] { "R0", "R1U" }));
        Assert.That(Enum.GetNames<ModelARotationKind>(), Is.EqualTo(new[] { "R0", "R1U" }));
    }

    [Test]
    public void NewContractsAreImmutableAndDoNotOverrideReferenceEqualityForClasses()
    {
        foreach (var type in NewContracts.Where(t => !t.IsEnum))
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Assert.That(property.SetMethod, Is.Null, $"{type.Name}.{property.Name}");
            if (type.IsClass)
            {
                Assert.That(type.IsSealed, Is.True, type.Name);
                Assert.That(type.GetMethod(nameof(object.Equals), [typeof(object)])!.DeclaringType,
                    Is.EqualTo(typeof(object)), type.Name);
            }
            else
                Assert.That(type.CustomAttributes.Any(a =>
                    a.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute"), Is.True, type.Name);
        }
        foreach (var field in typeof(ModelAFlightProvenance).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            Assert.That(field.IsInitOnly, Is.True, field.Name);
    }

    [Test]
    public void PublicTypeInventoryContainsOnlyApprovedAdditionsAndPreservesLegacyModelKinds()
    {
        var legacyNames = new[]
        {
            "AerodynamicSettings", "CompletionReason", "FlightModelKind", "FlightRequest", "FlightState",
            "GravityDragSimulator", "GravityDragTrajectory", "GravityOnlySimulator", "GravityOnlyTrajectory",
            "GroundContactSettings", "SimulationResult", "TrajectoryMeasurements", "TrajectorySample", "Vector3d"
        };
        var exported = typeof(FlightState).Assembly.GetExportedTypes();
        Assert.That(exported.Select(t => t.Name), Is.EquivalentTo(legacyNames.Concat(NewContracts.Select(t => t.Name)).Append(nameof(ModelASimulator))));
        Assert.That(Enum.GetNames<FlightModelKind>(), Is.EqualTo(new[] { "GravityOnly", "GravityAndDrag" }));
        Assert.That((int)FlightModelKind.GravityOnly, Is.Zero);
        Assert.That((int)FlightModelKind.GravityAndDrag, Is.EqualTo(1));
        Assert.That(typeof(FlightState).GetProperties().Select(p => p.Name), Is.EquivalentTo(new[] { "Position", "Velocity" }));
        Assert.That(typeof(FlightRequest).GetConstructor([typeof(FlightState), typeof(double), typeof(double), typeof(double)]), Is.Not.Null);
        foreach (var type in exported.OrderBy(t => t.Name))
        {
            TestContext.Out.WriteLine($"PUBLIC TYPE {type.FullName}");
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                TestContext.Out.WriteLine($"  PROPERTY {property.PropertyType} {property.Name}");
            foreach (var constructor in type.GetConstructors().OrderBy(c => c.ToString()))
                TestContext.Out.WriteLine($"  CONSTRUCTOR {constructor}");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .OrderBy(m => m.ToString()))
                TestContext.Out.WriteLine($"  METHOD {method}");
            if (type.IsEnum)
                foreach (var value in Enum.GetValues(type))
                    TestContext.Out.WriteLine($"  ENUM {value} = {Convert.ToInt32(value)}");
        }
    }

    private static IEnumerable<string> FactoryNames(Type type) => type
        .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.Name);
}
