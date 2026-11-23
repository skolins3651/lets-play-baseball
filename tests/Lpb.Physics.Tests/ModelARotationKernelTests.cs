using NUnit.Framework;
using Q = Lpb.Physics.Tests.SpinReference.Rational;
using I = Lpb.Physics.Tests.SpinReference.Interval;

namespace Lpb.Physics.Tests;

public class ModelARotationKernelTests
{
    private static AerodynamicSettings Unit(double density = 1, double radius = 1, double mass = 1, double drag = 0) =>
        new(mass, radius, density, drag);

    [Test]
    public void R0ReturnsCanonicalZeroForAllFiniteScales()
    {
        var kernel = ModelARotationKernel.R0();
        foreach (var v in new[] { default(Vector3d), new(3, 4, 12), new(double.MaxValue, double.Epsilon, -double.MaxValue) })
        foreach (var w in new[] { default(Vector3d), new(1, 2, 3), new(double.Epsilon, -double.MaxValue, double.MaxValue) })
        {
            var result = kernel.Evaluate(v, w);
            foreach (var value in new[] { result.X, result.Y, result.Z })
                Assert.That(BitConverter.DoubleToUInt64Bits(value), Is.Zero);
        }
    }

    [TestCase(0)] [TestCase(1)]
    public void R1UValidatesInertiaAndBetaEvenWhenTheDerivativeIsDisabled(double density)
    {
        Assert.Throws<ArgumentNullException>(() => ModelARotationKernel.R1U(null!, 1, 0));
        foreach (var invalid in new[] { 0d, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => ModelARotationKernel.R1U(Unit(density), invalid, 0))!.ParamName,
                Is.EqualTo("rotationalInertia"));
        foreach (var invalid in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => ModelARotationKernel.R1U(Unit(density), 1, invalid))!.ParamName,
                Is.EqualTo("beta"));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void ExactZeroCasesAndSubsequentSpeedReactivation(int zeroCase)
    {
        var a = Unit(zeroCase == 0 ? 0 : 1, double.MaxValue, double.Epsilon);
        var kernel = ModelARotationKernel.R1U(a, double.Epsilon, zeroCase == 1 ? 0 : 1);
        var v = zeroCase == 2 ? default : new Vector3d(double.MaxValue, 0, 0);
        var w = zeroCase == 3 ? default : new Vector3d(0, double.MaxValue, 0);
        Assert.That(kernel.Evaluate(v, w), Is.EqualTo(default(Vector3d)));
        var ordinary = ModelARotationKernel.R1U(Unit(), 1, 1);
        Assert.That(ordinary.Evaluate(default, new(0, 0, 2)), Is.EqualTo(default(Vector3d)));
        Assert.That(ordinary.Evaluate(new(3, 4, 12), new(0, 0, 2)).Z, Is.LessThan(0));
    }

    [Test]
    public void AnalyticalDerivativeHasTheCancelledHalfFactorAndIgnoresDragAndMass()
    {
        var v = new Vector3d(3, 4, 12); var w = new Vector3d(0, 0, 2);
        var result = ModelARotationKernel.R1U(Unit(), 1, 1).Evaluate(v, w);
        SpinReference.AssertVector(result, [SpinReference.Point(0), SpinReference.Point(0), -SpinReference.Pi * SpinReference.Point(26)], 32);
        Assert.That(result, Is.EqualTo(ModelARotationKernel.R1U(Unit(mass: 2, drag: 99), 1, 1).Evaluate(v, w)));
    }

    [TestCase(.5, 1, 1, 1)] [TestCase(1, 2, 1, 1)] [TestCase(1, 1, 2, 1)] [TestCase(1, 1, 1, 2)]
    public void IndependentReferencesVerifyDensityRadiusInertiaAndBeta(double density, double radius, double inertia, double beta)
    {
        var a = Unit(density, radius);
        var v = new Vector3d(3, -4, 12); var w = new Vector3d(5, -7, 11);
        var result = ModelARotationKernel.R1U(a, inertia, beta).Evaluate(v, w);
        SpinReference.AssertVector(result, SpinReference.Rotation(a, inertia, beta, v, w), 32);
    }

    [Test]
    public void GyrospinDampsAndEveryNonzeroComponentOpposesSpin()
    {
        var a = Unit(); var kernel = ModelARotationKernel.R1U(a, 1, 1);
        var v = new Vector3d(3, -4, 12); var w = new Vector3d(5, -7, 11);
        var result = kernel.Evaluate(v, w);
        var reference = SpinReference.Rotation(a, 1, 1, v, w);
        SpinReference.AssertVector(result, reference, 32);
        Assert.That(result.X, Is.LessThan(0)); Assert.That(result.Y, Is.GreaterThan(0)); Assert.That(result.Z, Is.LessThan(0));
        var reversed = kernel.Evaluate(v, new(-w.X, -w.Y, -w.Z));
        Assert.That(reversed, Is.EqualTo(new Vector3d(-result.X, -result.Y, -result.Z)));
        var gyro = kernel.Evaluate(v, v);
        Assert.That(gyro, Is.Not.EqualTo(default(Vector3d)));
        Assert.That(new ModelAMagnusKernel(a).Evaluate(v, v), Is.EqualTo(default(Vector3d)));
        SpinReference.AssertVector(gyro, SpinReference.Rotation(a, 1, 1, v, v), 32);
    }

    [Test]
    public void InstantaneousSpeedAndFixedWorldAxisAreRecomputed()
    {
        var a = Unit(); var kernel = ModelARotationKernel.R1U(a, 1, 1);
        var v = new Vector3d(3, 4, 12); var w = new Vector3d(2, -3, 5);
        var first = kernel.Evaluate(v, w);
        var next = kernel.Evaluate(new(6, 8, 24), w);
        Assert.That(next, Is.EqualTo(new Vector3d(2 * first.X, 2 * first.Y, 2 * first.Z)));
        var rotated = kernel.Evaluate(new(-4, 3, 12), new(3, 2, 5));
        Assert.That(rotated, Is.EqualTo(new Vector3d(-first.Y, first.X, first.Z)));
        SpinReference.AssertVector(next, SpinReference.Rotation(a, 1, 1, new(6, 8, 24), w), 32);
    }

    [TestCase(1)] [TestCase(-1)]
    public void ApprovedCompensatingPrefactorsRemainFinite(int sign)
    {
        var a = Unit(Math.ScaleB(1, sign * 800), Math.ScaleB(1, sign * 100));
        var inertia = Math.ScaleB(1, -sign * 100);
        var v = new Vector3d(Math.ScaleB(1, -sign * 700), 0, 0);
        var w = new Vector3d(0, Math.ScaleB(1, -sign * 500), 0);
        var result = ModelARotationKernel.R1U(a, inertia, 1).Evaluate(v, w);
        SpinReference.AssertVector(result, SpinReference.Rotation(a, inertia, 1, v, w), 32);
        SpinReference.AssertComponent(result.Y, -SpinReference.Pi * I.Point(Q.Pow2(sign * 100)), 32);
    }

    [Test]
    public void SubnormalParametersAndWideNormalScalesMeetCertifiedReferences()
    {
        var a = Unit(double.MaxValue, double.Epsilon);
        var v = new Vector3d(Math.ScaleB(1, 1023), 0, 0);
        var w = new Vector3d(0, Math.ScaleB(1, 1023), 0);
        SpinReference.AssertVector(ModelARotationKernel.R1U(a, double.Epsilon, double.MaxValue).Evaluate(v, w),
            SpinReference.Rotation(a, double.Epsilon, double.MaxValue, v, w), 32);
        var random = new Random(7003);
        for (var i = 0; i < 32; i++)
        {
            var ve = random.Next(-400, 401); var we = random.Next(-400, 401);
            double Component(int exponent) => Math.ScaleB((2 * random.NextDouble() - 1), exponent);
            v = new(Component(ve), Component(ve), Component(ve));
            w = new(Component(we), Component(we), Component(we));
            SpinReference.AssertVector(ModelARotationKernel.R1U(Unit(), 1, 1).Evaluate(v, w),
                SpinReference.Rotation(Unit(), 1, 1, v, w), 32);
        }
        TestContext.Out.WriteLine("Random(7003): 32 wide-scale states; 96 R1-U components against certified references.");
    }

    [TestCase(800, "range")] [TestCase(-800, "rounds to zero")]
    public void FinalAngularRangeFailuresAreEvaluationErrors(int exponent, string reason)
    {
        var kernel = ModelARotationKernel.R1U(Unit(), 1, 1);
        var scale = Math.ScaleB(1, exponent);
        var error = Assert.Throws<ArithmeticException>(() => kernel.Evaluate(new(scale, 0, 0), new(0, scale, 0)));
        Assert.That(error!.Message, Does.Contain("R1-U").And.Contain("derivative.Y").And.Contain(reason));
        TestContext.Out.WriteLine(error.ToString());
    }

    [TestCase(0)] [TestCase(1)]
    public void RotationalEnergyDerivativeMatchesTheIndependentAnalyticalIdentity(int beta)
    {
        var v = new Vector3d(3, 4, 12); var w = new Vector3d(0, 0, 2);
        var result = ModelARotationKernel.R1U(Unit(), 1, beta).Evaluate(v, w);
        var reference = SpinReference.Rotation(Unit(), 1, beta, v, w);
        var energy = Q.From(2) * Q.From(result.Z);
        var expected = -SpinReference.Pi * SpinReference.Point(52 * beta);
        var allowance = Q.From(2) * ModelAMagnusKernelTests.ErrorBudget(reference[2], 32);
        Assert.That((energy - expected.Lower).Abs().CompareTo(allowance), Is.LessThanOrEqualTo(0));
        Assert.That((energy - expected.Upper).Abs().CompareTo(allowance), Is.LessThanOrEqualTo(0));
        Assert.That(energy.Sign, Is.LessThanOrEqualTo(0));
    }
}
