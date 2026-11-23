using System.Numerics;
using System.Reflection;
using NUnit.Framework;
using Scaled = Lpb.Physics.SpinKernelArithmetic.ScaledNumber;
using Q = Lpb.Physics.Tests.SpinReference.Rational;
using I = Lpb.Physics.Tests.SpinReference.Interval;

namespace Lpb.Physics.Tests;

public class SpinKernelArithmeticTests
{
    [Test]
    public void DecompositionAndConversionPreserveEveryFiniteBoundaryIncludingSignedZero()
    {
        var values = new[] { 0d, -0d, double.Epsilon, -double.Epsilon, 3 * double.Epsilon,
            Math.BitDecrement(Math.ScaleB(1, -1022)), Math.ScaleB(1, -1022), Math.BitIncrement(Math.ScaleB(1, -1022)),
            .5, -1, Math.BitIncrement(1d), double.MaxValue, -double.MaxValue };
        foreach (var value in values)
        {
            var scaled = Scaled.From(value);
            Assert.That(SpinReference.Exact(scaled), Is.EqualTo(Q.From(value)));
            Assert.That(BitConverter.DoubleToUInt64Bits(scaled.ToDouble("round trip")),
                Is.EqualTo(value == 0 ? 0UL : BitConverter.DoubleToUInt64Bits(value)));
            if (value != 0)
                Assert.That(scaled.Mantissa, Is.GreaterThanOrEqualTo(1).And.LessThan(2));
        }
    }

    [TestCase(1)] [TestCase(-1)]
    public void SubnormalRoundingIsSingleTiesToEvenAndNeverSilentlyZero(int sign)
    {
        var signBits = sign < 0 ? 1UL << 63 : 0;
        Assert.That(Bits(sign, 1, -1074), Is.EqualTo(signBits | 1));
        Assert.That(Bits(sign, 1.5, -1074), Is.EqualTo(signBits | 2));
        Assert.That(Bits(sign, 1.25, -1073), Is.EqualTo(signBits | 2)); // 2.5 eta -> even 2
        Assert.That(Bits(sign, Math.BitIncrement(1d), -1075), Is.EqualTo(signBits | 1));
        Assert.Throws<ArithmeticException>(() => Bits(sign, 1, -1075)); // half eta -> zero
        Assert.Throws<ArithmeticException>(() => Bits(sign, Math.BitDecrement(1d), -1075));
        Assert.Throws<ArithmeticException>(() => Bits(sign, 1, -1600));
        Assert.That(Bits(sign, Math.BitDecrement(2d), -1023), Is.EqualTo(signBits | 0x0010_0000_0000_0000UL));
        Assert.That(Bits(sign, Math.BitDecrement(Math.BitDecrement(2d)), -1023),
            Is.EqualTo(signBits | 0x000f_ffff_ffff_ffffUL));
    }

    private static ulong Bits(int sign, double word, int exponent) =>
        BitConverter.DoubleToUInt64Bits(Scaled.From(sign * word).Shifted(exponent).ToDouble("conversion fixture"));

    [Test]
    public void NormalRangeEdgesAndUnsafeConversionsFailAsArithmetic()
    {
        Assert.That(Scaled.From(double.MaxValue).ToDouble("maximum"), Is.EqualTo(double.MaxValue));
        var overflow = Assert.Throws<ArithmeticException>(() => Scaled.From(1).Shifted(1024).ToDouble("component Z"));
        Assert.That(overflow!.Message, Does.Contain("component Z").And.Contain("range"));
        Assert.Throws<ArithmeticException>(() => Scaled.From(1).DividedBy(default));
        Assert.Throws<ArithmeticException>(() => SpinKernelArithmetic.Phi(Scaled.From(-1)));
    }

    [Test]
    public void ScaledOperationsAvoidIntermediateRangeLoss()
    {
        var large = Scaled.From(Math.ScaleB(1, 800));
        var small = Scaled.From(Math.ScaleB(1, -800));
        Assert.That(large.Times(large).Times(small).ToDouble("product"), Is.EqualTo(Math.ScaleB(1, 800)));
        Assert.That(small.Times(small).DividedBy(small).ToDouble("quotient"), Is.EqualTo(Math.ScaleB(1, -800)));
        Assert.That(Scaled.From(-0d).Times(large).Sign, Is.Zero);
    }

    [Test]
    public void ExactCancellationAndNearCancellationRetainTheExactSign()
    {
        var delta = Math.ScaleB(1, -52);
        Assert.That(1 - (1 - delta) * (1 + delta), Is.Zero);
        var dyadic = ExactBinary.From(1) - ExactBinary.From(1 - delta) * ExactBinary.From(1 + delta);
        Assert.That(dyadic.CompareTo(ExactBinary.From(Math.ScaleB(1, -104))), Is.Zero);
        var actual = SpinKernelArithmetic.Determinant(1, 1, 1 - delta, 1 + delta);
        Assert.That(SpinReference.Exact(actual), Is.EqualTo(Q.Pow2(-104)));
        foreach (var exponent in new[] { -800, 0, 800 })
        {
            var value = Math.ScaleB(1, exponent);
            Assert.That(SpinKernelArithmetic.Determinant(value, value, value, value).IsZero, Is.True);
        }
    }

    [TestCase(1)] [TestCase(3)] [TestCase(-1)] [TestCase(-3)]
    public void SubnormalInputsAreDecomposedBeforeTheirProductsCanUnderflow(int units)
    {
        CheckDeterminant(units * double.Epsilon, .5, 0, 1);
        CheckDeterminant(0, 1, units * double.Epsilon, .5);
    }

    [TestCase(126)] [TestCase(127)] [TestCase(128)] [TestCase(129)]
    public void ExponentGapBoundaryAndMantissaNeighborsMeetGamma8(int gap)
    {
        foreach (var a in new[] { Math.BitDecrement(1d), 1, Math.BitIncrement(1d), Math.BitDecrement(2d) })
        foreach (var sign in new[] { -1, 1 })
        {
            var b = Math.ScaleB(1, -gap);
            CheckDeterminant(a, sign, b, Math.BitDecrement(2d));
            CheckDeterminant(b, Math.BitDecrement(2d), a, sign);
            CheckDeterminant(a, sign, -b, Math.BitDecrement(2d));
        }
    }

    [Test]
    public void SeededDeterminantsAreCheckedAgainstIndependentExactDyadics()
    {
        var rng = new Random(7001);
        var counts = new int[3];
        for (var i = 0; i < 4000; i++)
        {
            double Next() => Math.ScaleB((rng.Next(2) == 0 ? -1 : 1) * (1 + rng.NextDouble()), rng.Next(-1000, 1001));
            var a = Next(); var b = Next(); var c = Next(); var d = Next();
            if (i % 3 == 0) { c = a; d = Math.BitIncrement(b); }
            if (i % 3 == 1) { c = a; d = b; }
            var result = CheckDeterminant(a, b, c, d);
            counts[result.Sign + 1]++;
        }
        TestContext.Out.WriteLine($"Random(7001): 4000 exact determinant references; negative/zero/positive = {string.Join("/", counts)}.");
        Assert.That(counts.All(c => c > 0), Is.True);
    }

    private static Scaled CheckDeterminant(double a, double b, double c, double d)
    {
        var expected = Q.From(a) * Q.From(b) - Q.From(c) * Q.From(d);
        var actual = SpinKernelArithmetic.Determinant(a, b, c, d);
        Assert.That(actual.Sign, Is.EqualTo(expected.Sign));
        SpinReference.AssertRelative(actual, I.Point(expected), 8);
        return actual;
    }

    [TestCase(63)] [TestCase(64)] [TestCase(65)]
    public void NormOmissionBoundaryAndNeighborsRespectTheProvedFactorBound(int gap)
    {
        foreach (var word in new[] { Math.BitDecrement(1d), 1, Math.BitIncrement(1d), Math.BitDecrement(2d) })
        {
            var v = new Vector3d(1, Math.ScaleB(word, -gap), -Math.ScaleB(word, -gap));
            SpinReference.AssertRelative(SpinKernelArithmetic.Norm(v), SpinReference.Norm(v), 4);
        }
    }

    [Test]
    public void NormRemainsScaledWhenItsMaterializedMagnitudeWouldOverflow()
    {
        var v = new Vector3d(1.5 * Math.ScaleB(1, 1023), 1.5 * Math.ScaleB(1, 1023), double.Epsilon);
        var actual = SpinKernelArithmetic.Norm(v);
        SpinReference.AssertRelative(actual, SpinReference.Norm(v), 4);
        Assert.Throws<ArithmeticException>(() => actual.ToDouble("norm"));
        Assert.That(SpinKernelArithmetic.Norm(default(Vector3d)).IsZero, Is.True);
    }

    [Test]
    public void PolynomialCoefficientsHaveCertifiedRationalRoundingCells()
    {
        var coefficients = (double[])typeof(SpinKernelArithmetic)
            .GetField("PhiCoefficients", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Assert.That(coefficients, Has.Length.EqualTo(16));
        var factorial = BigInteger.One;
        for (var j = 0; j < coefficients.Length; j++)
        {
            factorial *= j + 1;
            var exact = new Q(j % 2 == 0 ? 1 : -1, factorial);
            Assert.That(BitConverter.DoubleToUInt64Bits(coefficients[j]),
                Is.EqualTo(BitConverter.DoubleToUInt64Bits(exact.ToNearestDouble())), $"coefficient {j}");
        }
        Assert.That(SpinReference.Rounded(SpinReference.Pi), Is.EqualTo(Math.PI), "Independent Machin enclosure certifies pi.");
    }

    [Test]
    public void PhiBoundariesNeighborsAndEveryHalvingRangeMeetGamma64()
    {
        var inputs = new HashSet<double> { 0, double.Epsilon, .125, .3, 1, 3, 12, 31, 63 };
        foreach (var boundary in new[] { Math.ScaleB(1, -53), .5, 1, 2, 4, 8, 16, 32, 64 })
        {
            inputs.Add(Math.BitDecrement(boundary)); inputs.Add(boundary); inputs.Add(Math.BitIncrement(boundary));
        }
        foreach (var z in inputs)
            SpinReference.AssertRelative(SpinKernelArithmetic.Phi(Scaled.From(z)), SpinReference.Phi(Q.From(z)), 64, $"phi({z:R})");
    }

    [TestCase(-1600)] [TestCase(-54)] [TestCase(6)] [TestCase(1600)]
    public void PhiDoesNotMaterializeUnrepresentableArgumentsOrReciprocals(int exponent)
    {
        var z = Scaled.From(1).Shifted(exponent);
        SpinReference.AssertRelative(SpinKernelArithmetic.Phi(z), SpinReference.Phi(Q.Pow2(exponent)), 64);
    }

    [Test]
    public void KernelsAndArithmeticDoNotExpandThePublicSurface()
    {
        foreach (var type in new[] { typeof(ModelAMagnusKernel), typeof(ModelARotationKernel), typeof(SpinKernelArithmetic) })
            Assert.That(type.IsNotPublic, Is.True);
        Assert.That(typeof(ModelAMagnusKernel).Assembly.GetExportedTypes().Any(t =>
            t.Name is "ModelAMagnusKernel" or "ModelARotationKernel" or "SpinKernelArithmetic"), Is.False);
    }
}

// Verification-only arithmetic, independent of the production scaled/EFT path.
// Rational endpoints are rounded OUTWARD to 320 significant binary digits.
// Machin's identity, Taylor remainder bounds, and integer square-root brackets
// give certified enclosures, not precision-stability observations or Math.Exp.
internal static class SpinReference
{
    internal readonly struct Rational : IComparable<Rational>, IEquatable<Rational>
    {
        internal BigInteger Numerator { get; }
        internal BigInteger Denominator { get; }
        internal int Sign => Numerator.Sign;
        internal static Rational Zero => new(0, 1);
        internal static Rational One => new(1, 1);

        internal Rational(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero) throw new DivideByZeroException("Reference denominator.");
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            Numerator = numerator / divisor;
            Denominator = denominator / divisor;
        }

        internal static Rational From(double value)
        {
            if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            var bits = BitConverter.DoubleToUInt64Bits(value);
            var encoded = (int)((bits >> 52) & 2047);
            BigInteger integer = bits & 0x000f_ffff_ffff_ffffUL;
            if (encoded != 0) integer += BigInteger.One << 52;
            if ((bits >> 63) != 0) integer = -integer;
            return new Rational(integer, 1) * Pow2(encoded == 0 ? -1074 : encoded - 1075);
        }

        internal static Rational Pow2(int exponent) => exponent >= 0
            ? new(BigInteger.One << exponent, 1) : new(1, BigInteger.One << -exponent);
        internal Rational Abs() => new(BigInteger.Abs(Numerator), Denominator);
        internal int BinaryExponent()
        {
            var integer = BigInteger.Abs(Numerator);
            if (integer.IsZero) throw new InvalidOperationException("Zero has no reference exponent.");
            var exponent = checked((int)(integer.GetBitLength() - Denominator.GetBitLength()));
            if (new Rational(integer, Denominator).CompareTo(Pow2(exponent)) < 0) exponent--;
            return exponent;
        }

        internal Rational Outward(bool upper)
        {
            if (Sign == 0) return Zero;
            var step = Pow2(BinaryExponent() - 319);
            var units = this / step;
            var whole = BigInteger.DivRem(units.Numerator, units.Denominator, out var remainder);
            if (!remainder.IsZero && upper && Sign > 0) whole++;
            if (!remainder.IsZero && !upper && Sign < 0) whole--;
            return new Rational(whole, 1) * step;
        }

        internal double ToNearestDouble()
        {
            if (Sign == 0) return 0;
            var magnitude = Abs();
            var unitExponent = Math.Max(-1074, magnitude.BinaryExponent() - 52);
            var units = magnitude / Pow2(unitExponent);
            var whole = BigInteger.DivRem(units.Numerator, units.Denominator, out var remainder);
            var comparison = (remainder * 2).CompareTo(units.Denominator);
            if (comparison > 0 || (comparison == 0 && !whole.IsEven)) whole++;
            if (whole.IsZero) return BitConverter.UInt64BitsToDouble(Sign < 0 ? 1UL << 63 : 0);
            return Sign * Math.ScaleB((double)whole, unitExponent);
        }

        public int CompareTo(Rational other) => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
        public bool Equals(Rational other) => Numerator == other.Numerator && Denominator == other.Denominator;
        public override bool Equals(object? other) => other is Rational q && Equals(q);
        public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);
        public override string ToString() => $"{Numerator}/{Denominator}";
        public static Rational operator +(Rational a, Rational b) => new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
        public static Rational operator -(Rational a) => new(-a.Numerator, a.Denominator);
        public static Rational operator -(Rational a, Rational b) => a + -b;
        public static Rational operator *(Rational a, Rational b) => new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
        public static Rational operator /(Rational a, Rational b) => new(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
    }

    internal readonly record struct Interval(Rational Lower, Rational Upper)
    {
        internal static Interval Point(Rational q) => new(q, q);
        private static Interval Round(Rational lo, Rational hi) => new(lo.Outward(false), hi.Outward(true));
        public static Interval operator +(Interval a, Interval b) => Round(a.Lower + b.Lower, a.Upper + b.Upper);
        public static Interval operator -(Interval a) => new(-a.Upper, -a.Lower);
        public static Interval operator -(Interval a, Interval b) => a + -b;
        public static Interval operator *(Interval a, Interval b)
        {
            var values = new[] { a.Lower * b.Lower, a.Lower * b.Upper, a.Upper * b.Lower, a.Upper * b.Upper };
            return Round(values.Min(), values.Max());
        }
        public static Interval operator /(Interval a, Interval b)
        {
            if (b.Lower.Sign <= 0 && b.Upper.Sign >= 0) throw new ArithmeticException("Reference interval division crosses zero.");
            var values = new[] { a.Lower / b.Lower, a.Lower / b.Upper, a.Upper / b.Lower, a.Upper / b.Upper };
            return Round(values.Min(), values.Max());
        }
    }

    internal static Rational Integer(int value) => new(value, 1);
    internal static Interval Point(int value) => Interval.Point(Integer(value));
    internal static Rational Gamma(int count) => new(count, (BigInteger.One << 53) - count);
    internal static Rational Exact(Scaled value) => value.IsZero ? Rational.Zero :
        Rational.From(value.Sign * value.Mantissa) * Rational.Pow2(value.Exponent);
    internal static readonly Interval Pi = AtanReciprocal(5, 128) * Point(16) - AtanReciprocal(239, 48) * Point(4);

    private static Interval AtanReciprocal(int denominator, int count)
    {
        var x = Interval.Point(new Rational(1, denominator));
        var square = x * x;
        var power = x;
        var sum = Point(0);
        for (var j = 0; j < count; j++)
        {
            var term = power / Point(2 * j + 1);
            sum = j % 2 == 0 ? sum + term : sum - term;
            power *= square;
        }
        var remainder = (power / Point(2 * count + 1)).Upper;
        return new(sum.Lower - remainder, sum.Upper + remainder);
    }

    internal static Interval Sqrt(Interval value) => new(SqrtEndpoint(value.Lower, false), SqrtEndpoint(value.Upper, true));
    private static Rational SqrtEndpoint(Rational value, bool upper)
    {
        if (value.Sign == 0) return Rational.Zero;
        if (value.Sign < 0) throw new ArithmeticException("Negative reference square root.");
        var exponent = value.BinaryExponent();
        var rootExponent = exponent >= 0 ? exponent / 2 : (exponent - 1) / 2;
        var stepExponent = rootExponent - 319;
        var scaled = value / Rational.Pow2(2 * stepExponent);
        var floor = scaled.Numerator / scaled.Denominator;
        var root = BigInteger.One << checked((int)((floor.GetBitLength() + 1) / 2));
        while (true)
        {
            var next = (root + floor / root) / 2;
            if (next >= root) break;
            root = next;
        }
        if (upper && root * root * scaled.Denominator != scaled.Numerator) root++;
        return new Rational(root, 1) * Rational.Pow2(stepExponent);
    }

    internal static Interval OneMinusExp(Interval argument)
    {
        if (argument.Lower.Sign < 0) throw new ArithmeticException("Negative reference exponential argument.");
        if (argument.Lower.CompareTo(Integer(64)) >= 0)
        {
            // e > 1+1+1/2+1/6 = 8/3. No neglected exponential is set to zero.
            var count = argument.Lower.CompareTo(Integer(512)) >= 0 ? 512 :
                (int)(argument.Lower.Numerator / argument.Lower.Denominator);
            var tail = new Rational(BigInteger.Pow(3, count), BigInteger.Pow(8, count));
            return new(Rational.One - tail, Rational.One);
        }
        Assert.That(argument.Upper.CompareTo(Integer(65)), Is.LessThanOrEqualTo(0), "Reference Taylor domain.");
        var term = argument;
        var sum = term;
        for (var j = 2; j <= 512; j++)
        {
            var nextBound = (term * argument / Point(j)).Upper;
            if (nextBound.CompareTo(argument.Lower * Rational.Pow2(-280)) <= 0)
                return new(sum.Lower - nextBound, sum.Upper + nextBound);
            term = term * argument / Point(j);
            sum = j % 2 == 0 ? sum - term : sum + term;
        }
        // Taylor's Lagrange remainder <= z^513/513!, since exp(-xi)<=1.
        var remainder = (term * argument / Point(513)).Upper;
        return new(sum.Lower - remainder, sum.Upper + remainder);
    }

    internal static Interval Phi(Rational argument) => argument.Sign == 0 ? Point(1) :
        OneMinusExp(Interval.Point(argument)) / Interval.Point(argument);
    internal static Rational NormSquared(Vector3d v) =>
        Rational.From(v.X) * Rational.From(v.X) + Rational.From(v.Y) * Rational.From(v.Y) + Rational.From(v.Z) * Rational.From(v.Z);
    internal static Interval Norm(Vector3d v) => Sqrt(Interval.Point(NormSquared(v)));

    internal static Interval[] Magnus(AerodynamicSettings a, Vector3d v, Vector3d w)
    {
        var c = new[] { Rational.From(w.Y) * Rational.From(v.Z) - Rational.From(w.Z) * Rational.From(v.Y),
            Rational.From(w.Z) * Rational.From(v.X) - Rational.From(w.X) * Rational.From(v.Z),
            Rational.From(w.X) * Rational.From(v.Y) - Rational.From(w.Y) * Rational.From(v.X) };
        var speedSquared = NormSquared(v);
        var crossSquared = c.Aggregate(Rational.Zero, (sum, x) => sum + x * x);
        if (a.AirDensity == 0 || speedSquared.Sign == 0 || crossSquared.Sign == 0)
            return [Point(0), Point(0), Point(0)];
        var crossMagnitude = Sqrt(Interval.Point(crossSquared));
        var radius = Interval.Point(Rational.From(a.BallRadius));
        var z = Interval.Point(new Rational(6041, 1000)) * radius * crossMagnitude / Interval.Point(speedSquared);
        // Independent normalized-force formula, not the production regularized phi path.
        var factor = Interval.Point(Rational.From(a.AirDensity) * Rational.From(a.BallRadius) *
            Rational.From(a.BallRadius) * speedSquared / (Integer(2) * Rational.From(a.BallMass))) *
            Pi * Interval.Point(new Rational(42, 125)) * OneMinusExp(z) / crossMagnitude;
        return c.Select(x => factor * Interval.Point(x)).ToArray();
    }

    internal static Interval[] Rotation(AerodynamicSettings a, double inertia, double beta, Vector3d v, Vector3d w)
    {
        var radius = Rational.From(a.BallRadius);
        var factor = -Interval.Point(Rational.From(a.AirDensity) * radius * radius * radius * radius *
            Rational.From(beta) / Rational.From(inertia)) * Pi * Norm(v);
        return new[] { w.X, w.Y, w.Z }.Select(x => factor * Interval.Point(Rational.From(x))).ToArray();
    }

    internal static double Rounded(Interval reference)
    {
        var lo = reference.Lower.ToNearestDouble();
        var hi = reference.Upper.ToNearestDouble();
        Assert.That(BitConverter.DoubleToUInt64Bits(lo), Is.EqualTo(BitConverter.DoubleToUInt64Bits(hi)),
            "Reference enclosure does not certify a unique rounded binary64 result.");
        return lo;
    }

    internal static void AssertRelative(Scaled actual, Interval reference, int count, string context = "")
    {
        var value = Exact(actual);
        var error = new[] { (value - reference.Lower).Abs(), (value - reference.Upper).Abs() }.Max();
        var scale = new[] { reference.Lower.Abs(), reference.Upper.Abs() }.Min();
        Assert.That(error.CompareTo(Gamma(count) * scale), Is.LessThanOrEqualTo(0), context);
    }

    internal static void AssertComponent(double actual, Interval reference, int count, string context = "")
    {
        var rounded = Rounded(reference);
        var error = (Rational.From(actual) - Rational.From(rounded)).Abs();
        var allowance = Gamma(count) * Rational.From(rounded).Abs() + Integer(2) * Rational.Pow2(-1074);
        Assert.That(error.CompareTo(allowance), Is.LessThanOrEqualTo(0), context);
        if (reference.Lower.Sign > 0 || reference.Upper.Sign < 0)
            Assert.That(Math.Sign(actual), Is.EqualTo(reference.Lower.Sign), context + " nonzero sign");
        else if (reference.Lower.Sign == 0 && reference.Upper.Sign == 0)
            Assert.That(BitConverter.DoubleToUInt64Bits(actual), Is.Zero, context + " canonical zero");
    }

    internal static void AssertVector(Vector3d actual, Interval[] reference, int count)
    {
        var values = new[] { actual.X, actual.Y, actual.Z };
        for (var i = 0; i < 3; i++) AssertComponent(values[i], reference[i], count, $"component {i}");
    }
}
