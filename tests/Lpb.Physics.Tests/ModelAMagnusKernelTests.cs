using NUnit.Framework;
using Q = Lpb.Physics.Tests.SpinReference.Rational;
using I = Lpb.Physics.Tests.SpinReference.Interval;

namespace Lpb.Physics.Tests;

public class ModelAMagnusKernelTests
{
    private static AerodynamicSettings Unit(double density = 1, double radius = 1, double mass = 1, double drag = 0) =>
        new(mass, radius, density, drag);
    private static Vector3d Negate(Vector3d v) => new(-v.X, -v.Y, -v.Z);

    [TestCase(0, 0)] [TestCase(0, 1)] [TestCase(0, 2)]
    [TestCase(1, 0)] [TestCase(1, 1)] [TestCase(1, 2)]
    [TestCase(2, 0)] [TestCase(2, 1)] [TestCase(2, 2)]
    public void AllCardinalOrientationsAndSpinSignsMatchTheNormalizedForce(int velocityAxis, int spinAxis)
    {
        var axes = new[] { new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Vector3d(0, 0, 1) };
        var a = Unit();
        foreach (var sign in new[] { -1, 1 })
        {
            var w = sign > 0 ? axes[spinAxis] : Negate(axes[spinAxis]);
            var result = new ModelAMagnusKernel(a).Evaluate(axes[velocityAxis], w);
            SpinReference.AssertVector(result, SpinReference.Magnus(a, axes[velocityAxis], w), 128);
        }
    }

    [Test]
    public void GyrospinDoesNotChangeAsrBLiftAndInstantaneousZeroDoesNotLatch()
    {
        var a = Unit();
        var kernel = new ModelAMagnusKernel(a);
        var v = new Vector3d(4, 0, 0);
        var transverse = new Vector3d(0, 2, 0);
        var mixed = new Vector3d(7, 2, 0);
        Assert.That(kernel.Evaluate(v, mixed), Is.EqualTo(kernel.Evaluate(v, transverse)));
        SpinReference.AssertVector(kernel.Evaluate(v, mixed), SpinReference.Magnus(a, v, mixed), 128);
        Assert.That(kernel.Evaluate(v, new(7, 0, 0)), Is.EqualTo(default(Vector3d)));
        Assert.That(kernel.Evaluate(v, new(-7, 0, 0)), Is.EqualTo(default(Vector3d)));
        Assert.That(kernel.Evaluate(default, mixed), Is.EqualTo(default(Vector3d)));
        Assert.That(kernel.Evaluate(v, default), Is.EqualTo(default(Vector3d)));
        var turned = new Vector3d(0, 4, 0);
        var reactivated = kernel.Evaluate(turned, new(7, 0, 0));
        Assert.That(reactivated.Z, Is.GreaterThan(0));
        SpinReference.AssertVector(reactivated, SpinReference.Magnus(a, turned, new(7, 0, 0)), 128);
    }

    [Test]
    public void ZeroDensityAndNullValidationAreIndependentOfDragDisablement()
    {
        Assert.Throws<ArgumentNullException>(() => new ModelAMagnusKernel(null!));
        var huge = new Vector3d(double.MaxValue, double.MaxValue, double.Epsilon);
        Assert.That(new ModelAMagnusKernel(Unit(0, double.MaxValue, double.Epsilon))
            .Evaluate(huge, new(double.Epsilon, double.MaxValue, -double.MaxValue)), Is.EqualTo(default(Vector3d)));
        var v = new Vector3d(3, 4, 12); var w = new Vector3d(5, 2, -7);
        Assert.That(new ModelAMagnusKernel(Unit(drag: 0)).Evaluate(v, w),
            Is.EqualTo(new ModelAMagnusKernel(Unit(drag: 99)).Evaluate(v, w)));
        Assert.That(new ModelAMagnusKernel(Unit()).Evaluate(v, w), Is.Not.EqualTo(default(Vector3d)));
    }

    [Test]
    public void ObliqueLiftIsOrthogonalAndReversesWithSpin()
    {
        var a = new AerodynamicSettings(.145, .0366, 1.225, .35);
        var v = new Vector3d(31, -17, 8); var w = new Vector3d(53, 211, -97);
        var result = new ModelAMagnusKernel(a).Evaluate(v, w);
        var reference = SpinReference.Magnus(a, v, w);
        SpinReference.AssertVector(result, reference, 128);
        AssertDotZero(result, v, reference, 128);
        AssertDotZero(result, w, reference, 128);
        var opposite = new ModelAMagnusKernel(a).Evaluate(v, Negate(w));
        Assert.That(opposite, Is.EqualTo(Negate(result)));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void ProperRotationCovarianceIncludesRoundedInputs(int rotation)
    {
        Vector3d Rotate(Vector3d v) => rotation switch
        {
            0 => new(-v.Y, v.X, v.Z),
            1 => new(v.Y, v.Z, v.X),
            _ => new(.6 * v.X - .8 * v.Y, .8 * v.X + .6 * v.Y, v.Z)
        };
        var a = Unit();
        var v = new Vector3d(3, -4, 12); var w = new Vector3d(5, 2, -7);
        var kernel = new ModelAMagnusKernel(a);
        var original = kernel.Evaluate(v, w);
        var rotated = kernel.Evaluate(Rotate(v), Rotate(w));
        SpinReference.AssertVector(rotated, SpinReference.Magnus(a, Rotate(v), Rotate(w)), 128);
        var transformed = Rotate(original);
        var scale = SpinReference.Norm(original).Upper;
        var allowance = new Q(512, System.Numerics.BigInteger.One << 53) * scale;
        foreach (var delta in new[] { Q.From(rotated.X) - Q.From(transformed.X),
            Q.From(rotated.Y) - Q.From(transformed.Y), Q.From(rotated.Z) - Q.From(transformed.Z) })
            Assert.That(delta.Abs().CompareTo(allowance), Is.LessThanOrEqualTo(0));
    }

    [TestCase(1, 1, 2)] [TestCase(2, 1, 1)] [TestCase(1, 2, 1)]
    public void IndependentReferencesCheckDensityMassAndRadiusNormalization(double density, double mass, double radius)
    {
        var a = Unit(density, radius, mass);
        var v = new Vector3d(4, 0, 0); var w = new Vector3d(0, 2 / radius, 0);
        var result = new ModelAMagnusKernel(a).Evaluate(v, w);
        SpinReference.AssertVector(result, SpinReference.Magnus(a, v, w), 128);
        var baseline = new ModelAMagnusKernel(Unit()).Evaluate(v, new(0, 2, 0));
        var factor = density * radius * radius / mass;
        SpinReference.AssertComponent(result.Y, I.Point(Q.From(0)), 128);
        var allowed = SpinReference.Gamma(256) * Q.From(baseline.Z * factor).Abs();
        Assert.That((Q.From(result.Z) - Q.From(baseline.Z * factor)).Abs().CompareTo(allowed), Is.LessThanOrEqualTo(0));
    }

    private static IEnumerable<TestCaseData> ExtremeCases()
    {
        double P(int e) => Math.ScaleB(1, e);
        yield return new TestCaseData(Unit(), new Vector3d(1 + P(-52), 1, 0), new Vector3d(1, 1 - P(-52), 0)).SetName("Magnus_LostCancellation");
        yield return new TestCaseData(Unit(), new Vector3d(P(600), P(-600), 0), new Vector3d(0, 0, 1)).SetName("Magnus_TinyRetainedComponent");
        yield return new TestCaseData(Unit(P(-800)), new Vector3d(P(800), 0, 0), new Vector3d(0, P(800), 0)).SetName("Magnus_CrossOverflowFiniteLift");
        yield return new TestCaseData(Unit(P(800)), new Vector3d(P(-800), 0, 0), new Vector3d(0, P(-800), 0)).SetName("Magnus_CrossUnderflowFiniteLift");
        yield return new TestCaseData(Unit(), new Vector3d(P(-500), 0, 0), new Vector3d(0, P(800), 0)).SetName("Magnus_ArgumentOverflow");
        yield return new TestCaseData(Unit(), new Vector3d(P(800), 0, 0), new Vector3d(0, P(-800), 0)).SetName("Magnus_ArgumentUnderflow");
        yield return new TestCaseData(Unit(), new Vector3d(1.5 * P(1023), 1.5 * P(1023), 0), new Vector3d(0, 0, P(-1023))).SetName("Magnus_NormOverflowFiniteLift");
        yield return new TestCaseData(Unit(1, double.Epsilon, double.Epsilon),
            new Vector3d(P(1023), 0, 0), new Vector3d(0, P(1023), 0)).SetName("Magnus_SubnormalParametersFiniteLift");
    }

    [TestCaseSource(nameof(ExtremeCases))]
    public void ApprovedExtremeFixturesMeetTheUnchangedComponentBudget(AerodynamicSettings a, Vector3d v, Vector3d w)
    {
        var result = new ModelAMagnusKernel(a).Evaluate(v, w);
        SpinReference.AssertVector(result, SpinReference.Magnus(a, v, w), 128);
    }

    [Test]
    public void SeededOrdinaryStatesMeetCertifiedNormalizedForceReferences()
    {
        var random = new Random(7002);
        for (var i = 0; i < 24; i++)
        {
            double Component(double scale) => (2 * random.NextDouble() - 1) * scale;
            var a = new AerodynamicSettings(.1 + random.NextDouble() * .1, .02 + random.NextDouble() * .04,
                .6 + random.NextDouble(), 0);
            var v = new Vector3d(Component(50), Component(50), Component(50));
            var w = new Vector3d(Component(400), Component(400), Component(400));
            SpinReference.AssertVector(new ModelAMagnusKernel(a).Evaluate(v, w), SpinReference.Magnus(a, v, w), 128);
        }
        TestContext.Out.WriteLine("Random(7002): 24 states; 72 Magnus components against certified normalized-force enclosures.");
    }

    [TestCase(800)] [TestCase(-800)]
    public void ExactParallelExtremeInputsRemainCanonicalZero(int exponent)
    {
        var scale = Math.ScaleB(1, exponent);
        var result = new ModelAMagnusKernel(Unit()).Evaluate(new(scale, -scale, scale), new(scale, -scale, scale));
        foreach (var value in new[] { result.X, result.Y, result.Z })
            Assert.That(BitConverter.DoubleToUInt64Bits(value), Is.Zero);
    }

    [TestCase(1)] [TestCase(-1)]
    public void MandatoryFinalSubnormalFixtureRequiresExactBitsInAdditionToTolerance(int sign)
    {
        var small = Math.ScaleB(1, -537);
        var v = new Vector3d(small, 0, 0); var w = new Vector3d(0, sign * small, 0);
        var a = Unit();
        var result = new ModelAMagnusKernel(a).Evaluate(v, w);
        Assert.That(BitConverter.DoubleToUInt64Bits(result.X), Is.Zero);
        Assert.That(BitConverter.DoubleToUInt64Bits(result.Y), Is.Zero);
        Assert.That(BitConverter.DoubleToUInt64Bits(result.Z), Is.EqualTo(sign > 0 ? 0x8000_0000_0000_0001UL : 1UL));
        Assert.That(Math.Sign(result.Z), Is.EqualTo(-sign));
        SpinReference.AssertVector(result, SpinReference.Magnus(a, v, w), 128);
    }

    [TestCase(800, "range")] [TestCase(-800, "rounds to zero")]
    public void GenuinelyUnrepresentableFinalLiftFailsContextually(int exponent, string reason)
    {
        var scale = Math.ScaleB(1, exponent);
        var kernel = new ModelAMagnusKernel(Unit());
        Vector3d? result = null;
        var error = Assert.Throws<ArithmeticException>(() => result = kernel.Evaluate(new(scale, 0, 0), new(0, scale, 0)));
        Assert.That(result, Is.Null);
        Assert.That(error!.Message, Does.Contain("Magnus").And.Contain("acceleration.Z").And.Contain(reason));
        TestContext.Out.WriteLine(error.ToString());
    }

    [Test]
    public void OneLostComponentRejectsTheWholeOtherwiseRepresentableVector()
    {
        var error = Assert.Throws<ArithmeticException>(() => new ModelAMagnusKernel(Unit())
            .Evaluate(new(Math.ScaleB(1, -800), 1, 0), new(0, 0, Math.ScaleB(1, -800))));
        Assert.That(error!.Message, Does.Contain("acceleration.Y").And.Contain("rounds to zero"));
    }

    [TestCase(0)] [TestCase(1)]
    public void DerivativeLevelMechanicalEnergyMatchesIndependentAnalyticalBalance(int drag)
    {
        var a = Unit(drag: drag);
        var v = new Vector3d(3, 4, 12); var w = new Vector3d(0, 0, 2);
        const double gravity = 2;
        var lift = new ModelAMagnusKernel(a).Evaluate(v, w);
        var referenceLift = SpinReference.Magnus(a, v, w);
        var legacy = new GravityDragDynamics(gravity, a).Evaluate(0, new(default, v));
        var d = new[] { legacy.VelocityXRate, legacy.VelocityYRate, legacy.VelocityZRate };
        var l = new[] { lift.X, lift.Y, lift.Z };
        var velocities = new[] { v.X, v.Y, v.Z };
        var composed = d.Zip(l, (x, y) => x + y).ToArray();
        var energy = Q.From(gravity * v.Z);
        var allowance = Q.Zero;
        for (var i = 0; i < 3; i++)
        {
            var expectedDrag = -SpinReference.Pi * I.Point(new Q(13 * drag, 2)) * I.Point(Q.From(velocities[i])) -
                SpinReference.Point(i == 2 ? 2 : 0);
            // For this positive-component fixture legacy drag has only positive
            // products and same-sign gravity addition, fewer than 32 error sites.
            var dragError = drag == 0 ? Q.Zero : ErrorBudget(expectedDrag, 32);
            var liftError = ErrorBudget(referenceLift[i], 128);
            var additionError = (Q.From(composed[i]) - Q.From(d[i]) - Q.From(l[i])).Abs();
            allowance += Q.From(velocities[i]).Abs() * (dragError + liftError + additionError);
            energy += Q.From(velocities[i]) * Q.From(composed[i]);
        }
        var expected = -SpinReference.Pi * I.Point(new Q(2197 * drag, 2));
        Assert.That((energy - expected.Lower).Abs().CompareTo(allowance), Is.LessThanOrEqualTo(0));
        Assert.That((energy - expected.Upper).Abs().CompareTo(allowance), Is.LessThanOrEqualTo(0));
    }

    internal static Q ErrorBudget(I reference, int gamma) =>
        SpinReference.Gamma(gamma) * new[] { reference.Lower.Abs(), reference.Upper.Abs() }.Max() + new Q(2, 1) * Q.Pow2(-1074);

    internal static void AssertDotZero(Vector3d value, Vector3d basis, I[] reference, int gamma)
    {
        var a = new[] { value.X, value.Y, value.Z }; var b = new[] { basis.X, basis.Y, basis.Z };
        var dot = Q.Zero; var allowance = Q.Zero;
        for (var i = 0; i < 3; i++)
        {
            dot += Q.From(a[i]) * Q.From(b[i]);
            allowance += Q.From(b[i]).Abs() * ErrorBudget(reference[i], gamma);
        }
        Assert.That(dot.Abs().CompareTo(allowance), Is.LessThanOrEqualTo(0));
    }
}
