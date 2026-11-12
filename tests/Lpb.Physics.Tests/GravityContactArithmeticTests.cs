using System.Buffers.Binary;
using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class GravityContactArithmeticTests
{
    private static double[] Values => [0, -0d, double.Epsilon, -double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon,
        Math.ScaleB(1, -1022), Math.BitDecrement(Math.ScaleB(1, -1022)), Math.ScaleB(1, -969), Math.ScaleB(1, -968),
        Math.BitDecrement(Math.ScaleB(1, -968)), Math.BitIncrement(Math.ScaleB(1, -968)),
        .5, -.5, 1, -1, Math.BitDecrement(1d), Math.BitIncrement(1d),
        Math.ScaleB(1, 53), Math.ScaleB(1, 500), Math.ScaleB(1, -500), double.MaxValue, -double.MaxValue];

    [Test]
    public void DeterministicArithmeticMatrixContainsIndependentExactValues()
    {
        var pairs = Values.SelectMany(a => Values.Select(b => (a, b))).ToList();
        foreach (var exponent in new[] { -1022, -1000, -970, -969, -968, -967 })
        foreach (var factor in new[] { Math.BitDecrement(1d), Math.BitIncrement(1d) })
        {
            var scale = Math.ScaleB(1, exponent);
            pairs.Add((scale, factor));
            pairs.Add((Math.BitIncrement(scale), factor));
        }
        Assert.That(pairs, Has.Count.EqualTo(553));
        CheckPairs(pairs, "deterministic");
    }

    [Test]
    public void SeededFiniteBitPatternsContainIndependentExactValues()
    {
        var random = new Random(22);
        var pairs = Enumerable.Range(0, 10_000).Select(_ => (NextFinite(random), NextFinite(random)));
        CheckPairs(pairs, "System.Random(22), .NET 10, little-endian 64-bit patterns");
    }

    [TestCase(-1022)] [TestCase(-1000)] [TestCase(-970)]
    [TestCase(-969)] [TestCase(-968)] [TestCase(-967)]
    public void ProductThresholdAndItsNeighborsNeverInventExactness(int exponent)
    {
        var a = Math.BitIncrement(Math.ScaleB(1, exponent));
        var b = Math.BitIncrement(1d);
        var exact = ExactBinary.From(a) * ExactBinary.From(b);
        Contains(GravityContactEvidence.Multiply(a, b), exact, $"product at 2^{exponent}");
        var rounded = a * b;
        var residual = GravityContactEvidence.Affine(a, b, -rounded);
        Contains(residual, exact - ExactBinary.From(rounded), $"residual at 2^{exponent}");
        if (Math.Abs(rounded) >= Math.ScaleB(1, -968))
            Assert.That(residual.Lower, Is.EqualTo(residual.Upper), "Safe-range product residual must be retained exactly.");
    }

    [TestCase(1)] [TestCase(3)] [TestCase(-1)] [TestCase(-3)]
    public void UnderflowedZeroFmaResidualDoesNotProveAnExactProduct(int units)
    {
        var a = units * double.Epsilon;
        var rounded = a * .5;
        Assert.That(Math.FusedMultiplyAdd(a, .5, -rounded), Is.Zero);
        var exact = (ExactBinary.From(a)).Half();
        Assert.That(exact.CompareTo(ExactBinary.From(rounded)), Is.Not.Zero);
        var bound = GravityContactEvidence.Multiply(a, .5);
        Contains(bound, exact, "subnormal half-product");
        Assert.That(bound.Lower, Is.LessThan(bound.Upper));
    }

    [Test]
    public void StructuralProductsPreserveRepresentableSubnormalsAndLargeExactValues()
    {
        foreach (var value in new[] { -double.MaxValue, -2 * double.Epsilon, 0d, 2 * double.Epsilon, double.MaxValue })
        foreach (var factor in new[] { 0d, 1, -1, .5, -.5 })
        {
            var bound = GravityContactEvidence.Multiply(value, factor);
            Contains(bound, ExactBinary.From(value) * ExactBinary.From(factor), "structural product");
            Assert.That(bound.Lower, Is.EqualTo(bound.Upper));
        }
    }

    [Test]
    public void TwoSumPreservesCancellationAndSubnormalSums()
    {
        var cancellation = GravityContactEvidence.Add(double.MaxValue, -double.MaxValue);
        Assert.That(cancellation.IsZero, Is.True);
        var tiny = GravityContactEvidence.Add(double.Epsilon, 2 * double.Epsilon);
        Assert.That(tiny.Lower, Is.EqualTo(3 * double.Epsilon));
        Assert.That(tiny.Upper, Is.EqualTo(tiny.Lower));
        var halfway = GravityContactEvidence.Add(1, Math.ScaleB(1, -53));
        Assert.That(halfway.Lower, Is.EqualTo(1));
        Assert.That(halfway.Upper, Is.EqualTo(Math.BitIncrement(1d)));
    }

    [Test]
    public void CompensatedAffineRetainsCancellingProductAndSumErrors()
    {
        var u = Math.ScaleB(1, -52);
        var rate = GravityContactEvidence.Affine(-2 * (1 + u), (1 - u) / 2, -u * u);
        Assert.That(rate.Lower, Is.EqualTo(-1));
        Assert.That(rate.Upper, Is.EqualTo(-1));
        Contains(rate, ExactBinary.From(-2 * (1 + u)) * ExactBinary.From((1 - u) / 2) +
            ExactBinary.From(-u * u), "compensated cancellation");
    }

    [Test]
    public void UnrepresentableArithmeticAndOutwardEndpointsThrowIntentionally()
    {
        Assert.Throws<ArithmeticException>(() => GravityContactEvidence.Add(double.MaxValue, double.MaxValue));
        // The rounded sum is finite but its required outward upper endpoint is not.
        Assert.That(double.IsFinite(double.MaxValue + Math.ScaleB(1, 969)), Is.True);
        Assert.Throws<ArithmeticException>(() => GravityContactEvidence.Add(double.MaxValue, Math.ScaleB(1, 969)));
        Assert.Throws<ArithmeticException>(() => GravityContactEvidence.Multiply(double.MaxValue, 2));
        Assert.Throws<ArithmeticException>(() => GravityContactEvidence.Affine(double.MaxValue, 2, -double.MaxValue));
        Assert.That(GravityContactEvidence.Add(double.MaxValue, 0).Upper, Is.EqualTo(double.MaxValue));
    }

    [Test]
    public void CompositeEnclosuresContainExpandedExactReferenceFromNonzeroOrigins()
    {
        var count = 0;
        foreach (var start in new[] { 0d, 1, 3, Math.ScaleB(1, 19) })
        foreach (var time in new[] { Math.BitIncrement(start), start + .75, start + 1 })
        foreach (var height in new[] { 0d, 1, Math.ScaleB(1, 20), -1 })
        foreach (var z in new[] { height, Math.BitIncrement(height), height + 1 })
        foreach (var g in new[] { 0d, 10, 3 * double.Epsilon, Math.ScaleB(1, 60) })
        foreach (var velocity in new[] { 0d, 1, -1, double.Epsilon, -1e9 })
        {
            var origin = new TrajectorySample(start, FlightFixtures.State(z, velocity));
            var bound = GravityContactEvidence.Evaluate(origin, time, height, g);
            var elapsed = ExactBinary.From(time) - ExactBinary.From(start);
            var q = ExactBinary.From(z) - ExactBinary.From(height) + ExactBinary.From(velocity) * elapsed -
                (ExactBinary.From(g) * elapsed * elapsed).Half();
            var w = ExactBinary.From(velocity) - ExactBinary.From(g) * elapsed;
            Contains(bound.Clearance, q, $"Q at {start:R}->{time:R}, z={z:R}, v={velocity:R}, g={g:R}");
            Contains(bound.Velocity, w, $"W at {start:R}->{time:R}, v={velocity:R}, g={g:R}");
            count++;
        }
        TestContext.Out.WriteLine($"Expanded exact Q/W references checked for {count} origin/time/geometry/velocity combinations.");
    }

    internal static void Contains(GravityContactEvidence.Bounds bounds, ExactBinary exact, string context)
    {
        Assert.That(double.IsFinite(bounds.Lower) && double.IsFinite(bounds.Upper), Is.True, context);
        Assert.That(ExactBinary.From(bounds.Lower).CompareTo(exact), Is.LessThanOrEqualTo(0), context);
        Assert.That(ExactBinary.From(bounds.Upper).CompareTo(exact), Is.GreaterThanOrEqualTo(0), context);
    }

    private static void CheckPairs(IEnumerable<(double A, double B)> pairs, string description)
    {
        var accepted = 0;
        var rejected = 0;
        var index = 0;
        foreach (var (a, b) in pairs)
        {
            var x = ExactBinary.From(a);
            var y = ExactBinary.From(b);
            var context = $"{description}, pair {index++}, a=0x{BitConverter.DoubleToUInt64Bits(a):x16}, b=0x{BitConverter.DoubleToUInt64Bits(b):x16}";
            Check(() => GravityContactEvidence.Add(a, b), x + y, context + " sum");
            Check(() => GravityContactEvidence.Multiply(a, b), x * y, context + " product");
            foreach (var c in new[] { 0d, -a, b })
                Check(() => GravityContactEvidence.Affine(a, b, c), x * y + ExactBinary.From(c), context + $" affine c={c:R}");
        }
        Assert.That(accepted, Is.GreaterThan(index * 3), "Range rejection cannot replace the arithmetic verification.");
        TestContext.Out.WriteLine($"{description}: {index} pairs, {accepted} exact-containing enclosures, {rejected} explicit arithmetic rejections.");
        return;

        void Check(Func<GravityContactEvidence.Bounds> evaluate, ExactBinary exact, string context)
        {
            GravityContactEvidence.Bounds result;
            try { result = evaluate(); }
            catch (ArithmeticException error)
            {
                Assert.That(error.Message, Does.Contain("Gravity contact"), context);
                rejected++;
                return;
            }
            Contains(result, exact, context);
            accepted++;
        }
    }

    private static double NextFinite(Random random)
    {
        Span<byte> bytes = stackalloc byte[8];
        while (true)
        {
            random.NextBytes(bytes);
            var bits = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
            if (((bits >> 52) & 0x7ff) != 0x7ff)
                return BitConverter.UInt64BitsToDouble(bits);
        }
    }
}
