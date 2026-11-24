using System.Numerics;
using NUnit.Framework;
using D = Lpb.Physics.ModelANumericalArithmetic.Dyadic;
using B = Lpb.Physics.ModelANumericalArithmetic.Bounds;
using Q = Lpb.Physics.Tests.SpinReference.Rational;
using static Lpb.Physics.ModelANumericalArithmetic;

namespace Lpb.Physics.Tests;

// Expected answers use the independent B1 test-only rational oracle, never rounded replay.
public class ModelANumericalArithmeticTests
{
    internal static Q Exact(D d) => new Q(d.N, 1) * Q.Pow2(d.E);
    internal static void Contains(B enclosure, Q value)
    {
        Assert.That(Exact(enclosure.Lower).CompareTo(value), Is.LessThanOrEqualTo(0));
        Assert.That(Exact(enclosure.Upper).CompareTo(value), Is.GreaterThanOrEqualTo(0));
        Assert.That(BigInteger.Abs(enclosure.Lower.N).GetBitLength(), Is.LessThanOrEqualTo(128));
        Assert.That(BigInteger.Abs(enclosure.Upper.N).GetBitLength(), Is.LessThanOrEqualTo(128));
    }

    [TestCase(0d)]
    [TestCase(-0d)]
    [TestCase(double.Epsilon)]
    [TestCase(-double.Epsilon)]
    [TestCase(double.MaxValue)]
    [TestCase(-double.MaxValue)]
    [TestCase(.336)]
    [TestCase(6.041)]
    [TestCase(2.2250738585072014e-308)]
    [TestCase(1.0000000000000002)]
    public void DirectDecodeIsCanonicalAndExact(double value)
    {
        var d = D.From(value);
        Assert.That(Exact(d), Is.EqualTo(Q.From(value)));
        Assert.That(d.N.IsZero ? d.E == 0 : !d.N.IsEven, Is.True);
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void NonfiniteDecodeRejects(double value) => Assert.Throws<ArgumentOutOfRangeException>(() => D.From(value));

    [Test]
    public void SignedNormalizationAndCancellationAreExact()
    {
        Assert.That(D.FromRaw(-48, -5), Is.EqualTo(D.From(-1.5)));
        Assert.That(D.One + D.Pow2(-2000) - D.One, Is.EqualTo(D.Pow2(-2000)));
        Assert.That(D.From(-0d), Is.EqualTo(D.Zero));
        var wide = B.One + B.Exact(D.Pow2(-2000)) - B.One;
        Contains(wide, Q.Pow2(-2000));
        Assert.That(wide.Upper.Sign, Is.Positive, "A widened enclosure must not collapse to exact zero.");
    }

    [TestCase(800)]
    [TestCase(-800)]
    public void ExactAndBoundedProductsHaveNoPrematureBinary64RangeLoss(int exponent)
    {
        var x = D.Pow2(exponent);
        Assert.That(x * x * D.Pow2(-exponent), Is.EqualTo(x));
        Contains(B.Exact(x) * B.Exact(x) * B.Exact(D.Pow2(-exponent)), Q.Pow2(exponent));
    }

    [Test]
    public void ExactOperationsAgreeWithIndependentRationalsAcrossBinary64Range()
    {
        var random = new Random(713);
        for (var i = 0; i < 160; i++)
        {
            var a = Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1070, 1020));
            var b = Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1070, 1020));
            var x = D.From(a); var y = D.From(b);
            var qa = Q.From(a); var qb = Q.From(b);
            Assert.That(Exact(x + y), Is.EqualTo(qa + qb));
            Assert.That(Exact(x - y), Is.EqualTo(qa - qb));
            Assert.That(Exact(x * y), Is.EqualTo(qa * qb));
            Assert.That(Math.Sign(x.CompareTo(y)), Is.EqualTo(Math.Sign(qa.CompareTo(qb))));
            Contains(B.Exact(x + y), qa + qb);
            if (b != 0) Contains(B.Quotient(x, y), qa / qb);
        }
    }

    [TestCase(-7, -2)]
    [TestCase(-7, 2)]
    [TestCase(7, -2)]
    [TestCase(7, 2)]
    [TestCase(1, 3)]
    [TestCase(-1, 3)]
    [TestCase(1, -3)]
    public void QuotientsEncloseBothSigns(int numerator, int denominator) =>
        Contains(B.Quotient(D.Integer(numerator), D.Integer(denominator)), new Q(numerator, denominator));

    [Test]
    public void OneThirdHasTheApprovedIntegerCertificateAndUnresolvedEqualityRejects()
    {
        var lowerUnits = BigInteger.DivRem(BigInteger.One << 129, 3, out var remainder);
        Assert.That(remainder, Is.EqualTo(new BigInteger(2)));
        var b = B.Quotient(D.One, D.Integer(3));
        Assert.That(Exact(b.Lower), Is.EqualTo(new Q(lowerUnits, 1) * Q.Pow2(-129)));
        Assert.That(Exact(b.Upper), Is.EqualTo(new Q(lowerUnits + 1, 1) * Q.Pow2(-129)));
        var negative = B.Quotient(-D.One, D.Integer(3));
        Assert.That(negative.Lower, Is.EqualTo(-b.Upper));
        Assert.That(negative.Upper, Is.EqualTo(-b.Lower));
        Assert.That(Within(b, b), Is.False);
        Assert.That(Within(B.One, B.One), Is.True);
    }

    [Test]
    public void IntervalOperationsBoundAllEndpointsAndDenominatorIsCheckedBeforeZeroNumerator()
    {
        var a = B.Endpoints(D.Integer(-2), D.Integer(3));
        var b = B.Endpoints(D.Integer(-5), D.Integer(-4));
        foreach (var x in new[] { -2, 0, 3 })
            foreach (var y in new[] { -5, -4 })
            {
                Contains(a + b, new Q(x + y, 1));
                Contains(a - b, new Q(x - y, 1));
                Contains(a * b, new Q(x * y, 1));
                Contains(a / b, new Q(x, y));
            }
        Assert.That(a.Abs().Lower, Is.EqualTo(D.Zero));
        Assert.That(a.Abs().Upper, Is.EqualTo(D.Integer(3)));
        Assert.That(B.Max(a, b).Lower, Is.EqualTo(D.Integer(-2)));
        Assert.Throws<ArithmeticException>(() => _ = B.Zero / a);
        Assert.Throws<ArithmeticException>(() => B.Quotient(D.Zero, D.Zero));
        Assert.Throws<ArgumentException>(() => B.Endpoints(D.One, D.Zero));
    }

    [TestCase(-2000)]
    [TestCase(-1074)]
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(800)]
    [TestCase(2200)]
    public void RootCertificatesHoldAboveAndBelowBinary64Range(int exponent)
    {
        var q = D.FromRaw(3, exponent);
        var root = SquareRoot(q);
        var lo = Exact(root.Lower); var hi = Exact(root.Upper);
        Assert.That((lo * lo).CompareTo(Exact(q)), Is.LessThanOrEqualTo(0));
        Assert.That((hi * hi).CompareTo(Exact(q)), Is.GreaterThanOrEqualTo(0));
        Assert.That(root.Lower.N.GetBitLength(), Is.LessThanOrEqualTo(128));
        Assert.That(root.Upper.N.GetBitLength(), Is.LessThanOrEqualTo(128));
    }

    [Test]
    public void SqrtTwoUsesTheApprovedGridAndIntegerRootCertificate()
    {
        var target = BigInteger.One << 255;
        var n = IntegerSquareRoot(target, out var iterations);
        Assert.That(iterations, Is.EqualTo(128));
        Assert.That(n * n < target && target < (n + 1) * (n + 1), Is.True);
        var root = SquareRoot(D.Integer(2));
        Assert.That(Exact(root.Lower), Is.EqualTo(new Q(n, 1) * Q.Pow2(-127)));
        Assert.That(Exact(root.Upper), Is.EqualTo(new Q(n + 1, 1) * Q.Pow2(-127)));
        Assert.That(SquareRoot(D.Zero).Upper, Is.EqualTo(D.Zero));
        Assert.Throws<ArithmeticException>(() => SquareRoot(-D.One));
    }

    [Test]
    public void ExactSubnormalHugeAndIntervalNormsHaveIndependentCertificates()
    {
        var exact = Norm(D.Integer(3), D.Integer(4), D.Integer(12));
        Assert.That(exact.Lower, Is.EqualTo(D.Integer(13)));
        Assert.That(exact.Upper, Is.EqualTo(D.Integer(13)));
        var sub = Norm(new Vector3d(double.Epsilon, 0, 0));
        Assert.That(sub.Lower, Is.EqualTo(D.Pow2(-1074)));
        var huge = Norm(new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
        Assert.That(huge.Lower.CompareTo(D.From(double.MaxValue)), Is.Positive);
        var x = B.Endpoints(D.Integer(-3), D.Integer(4));
        var interval = Norm(x, B.Endpoints(D.Integer(-3), D.Integer(3)), B.Point(12));
        Assert.That(interval.Lower, Is.EqualTo(D.Integer(12)));
        Contains(interval, new Q(13, 1));
    }

    [TestCase(38, true)]
    [TestCase(39, true)]
    [TestCase(40, false)]
    public void SpacingAllocationBoundaryIncludesTheExactTie(int exponent, bool expected) =>
        Assert.That(Representation(Spacing(Math.ScaleB(1, exponent)), D.Integer(9_765_625)), Is.EqualTo(expected));

    [TestCase(-1, true)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    public void AgreementExactDecimalBoundary(int side, bool expected)
    {
        var gap = D.Pow2(-10) + (side < 0 ? -D.Pow2(-63) : side > 0 ? D.Pow2(-62) : D.Zero);
        Assert.That(Agreement(gap, D.Integer(9_765_625)), Is.EqualTo(expected));
        Assert.That((new Q(10_000_000_000, 1) * Exact(gap)).CompareTo(new Q(9_765_625, 1)) <= 0, Is.EqualTo(expected));
    }

    [TestCase(-1, true)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    public void IsolatedLostIncrementAllocationBoundary(int side, bool expected)
    {
        var increment = D.Pow2(-13) + (side < 0 ? -D.Pow2(-66) : side > 0 ? D.Pow2(-65) : D.Zero);
        var loss6 = StageLoss6(4, D.Pow2(41), D.Pow2(41), D.One, increment);
        Assert.That(UpdateLoss(loss6, D.Integer(9_765_625)), Is.EqualTo(expected));
        Assert.That(Math.ScaleB(1, 41) + Math.ScaleB(1, -13), Is.EqualTo(Math.ScaleB(1, 41)));
    }

    [TestCase(0d, -1074)]
    [TestCase(-0d, -1074)]
    [TestCase(double.Epsilon, -1074)]
    [TestCase(2.2250738585072014e-308, -1074)]
    [TestCase(1d, -52)]
    [TestCase(-1d, -52)]
    [TestCase(double.MaxValue, 971)]
    [TestCase(-double.MaxValue, 971)]
    public void SpacingUsesTheLargerGapIncludingVirtualMaximumSuccessor(double value, int exponent) =>
        Assert.That(Spacing(value), Is.EqualTo(D.Pow2(exponent)));

    [Test]
    public void SubnormalUpdateLossIsNotRoundedAway()
    {
        var eta = D.Pow2(-1074);
        var loss6 = FinalLoss6(D.Zero, D.Zero, D.From(.5), D.Integer(6) * eta);
        Assert.That(Exact(loss6) / new Q(6, 1), Is.EqualTo(Q.Pow2(-1075)));
        Assert.That(loss6.Sign, Is.Positive);
        Assert.That(UpdateLoss(loss6, D.Integer(100)), Is.True);
    }

    [Test]
    public void WeightedCancellationAuditUsesExactClassicalCoefficients()
    {
        var k1 = Math.ScaleB(1, 60); var k2 = 1d; var k3 = -Math.ScaleB(1, 59);
        var actualWeighted = ((k1 + 2 * k2) + 2 * k3) + 0;
        Assert.That(actualWeighted, Is.Zero);
        var exactWeighted = D.From(k1) + D.From(k2).Shifted(1) + D.From(k3).Shifted(1);
        Assert.That(exactWeighted, Is.EqualTo(D.Integer(2)));
        var loss6 = FinalLoss6(D.Zero, D.Zero, D.One, exactWeighted);
        Assert.That(Exact(loss6) / new Q(6, 1), Is.EqualTo(new Q(1, 3)));
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void StageAuditMatchesIndependentUnroundedClassicalReference(int stage)
    {
        foreach (var anchor in new[] { 0d, 1d, -Math.ScaleB(1, 41) })
            foreach (var h in new[] { .1, .5, 1.2345678901234 })
            {
                const double rate = 1.0000000000000002;
                var observed = anchor + (stage == 4 ? h : h / 2) * rate;
                var ideal = Q.From(anchor) + Q.From(h) * Q.From(rate) / new Q(stage == 4 ? 1 : 2, 1);
                var expectedLoss6 = new Q(6, 1) * (Q.From(observed) - ideal).Abs();
                Assert.That(Exact(StageLoss6(stage, D.From(anchor), D.From(observed), D.From(h), D.From(rate))), Is.EqualTo(expectedLoss6));
            }
    }

    [Test]
    public void FinalAuditIncludesRoundedSixthCoefficientRatherThanUsingItAsTheIdeal()
    {
        var actual = 1d / 6;
        var loss6 = FinalLoss6(D.Zero, D.From(actual), D.One, D.One);
        var independent = new Q(6, 1) * (Q.From(actual) - new Q(1, 6)).Abs();
        Assert.That(Exact(loss6), Is.EqualTo(independent));
        Assert.That(loss6, Is.EqualTo(D.Pow2(-54)));
    }

    [Test]
    public void WorkGuardsRejectBeforeLargeShiftsProductsOrRootIterations()
    {
        Assert.Throws<InvalidOperationException>(() => D.Pow2(32_769));
        Assert.Throws<InvalidOperationException>(() => D.Pow2(-32_769));
        Assert.Throws<InvalidOperationException>(() => _ = D.Pow2(32_768) * D.Pow2(1));
        Assert.Throws<InvalidOperationException>(() => _ = D.Pow2(32_768) + D.Pow2(-32_768));
        Assert.Throws<InvalidOperationException>(() => D.FromRaw(BigInteger.One << 65_536, 0));
        var max = D.FromRaw((BigInteger.One << 65_536) - 1, 0);
        Assert.Throws<InvalidOperationException>(() => _ = max + D.One);
        Assert.Throws<InvalidOperationException>(() => _ = max * D.Integer(3));
        Assert.Throws<InvalidOperationException>(() => IntegerSquareRoot(BigInteger.One << 256, out _));
        var half = D.FromRaw((BigInteger.One << 32_768) - 1, 0);
        Assert.That((half * half).N.GetBitLength(), Is.EqualTo(65_536));
        Assert.That(D.FromRaw(1, -32_768).E, Is.EqualTo(-32_768));
        Assert.That(D.FromRaw(1, 32_768).E, Is.EqualTo(32_768));
    }
}
