using System.Reflection;
using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class BootstrapSmokeTests
{
    [Test]
    public void PhysicsAssemblyCanBeLoadedAndIdentified()
    {
        var assembly = Assembly.Load("Lpb.Physics");

        Assert.That(assembly.GetName().Name, Is.EqualTo("Lpb.Physics"));
    }
}
