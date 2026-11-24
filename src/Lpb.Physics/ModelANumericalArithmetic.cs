using System.Numerics;

namespace Lpb.Physics;

// B2-N arithmetic addendum §§1–2, 4, 6. This is not B1's nearest-rounded arithmetic.
internal static class ModelANumericalArithmetic
{
    internal const int MaximumBits = 65_536;
    internal const int MaximumExponent = 32_768;
    internal const int Precision = 128;

    private static int Bits(BigInteger n) => checked((int)BigInteger.Abs(n).GetBitLength());

    private static void RequireBits(long bits, string operation)
    {
        if (bits > MaximumBits)
            throw new InvalidOperationException($"Model A bounded arithmetic {operation} exceeds the {MaximumBits}-bit work limit.");
    }

    private static int Exponent(long value)
    {
        if (value < -MaximumExponent || value > MaximumExponent)
            throw new InvalidOperationException($"Model A bounded arithmetic exponent exceeds +/-{MaximumExponent}.");
        return (int)value;
    }

    private static BigInteger Shift(BigInteger value, int count)
    {
        if (count < 0) throw new InvalidOperationException("Model A bounded arithmetic has a negative left shift.");
        RequireBits((long)Bits(value) + count, "shift");
        return value << count;
    }

    // Constructed only for rare guard-boundary decisions, without a 65,537-bit intermediate.
    private static class Limit
    {
        internal static readonly BigInteger Magnitude =
            (BigInteger.One << (MaximumBits - 1)) | ((BigInteger.One << (MaximumBits - 1)) - 1);
    }

    internal readonly struct Dyadic : IComparable<Dyadic>, IEquatable<Dyadic>
    {
        internal BigInteger N { get; }
        internal int E { get; }
        internal int Sign => N.Sign;
        internal static Dyadic Zero => default;
        internal static Dyadic One => new(BigInteger.One, 0);

        private Dyadic(BigInteger n, int e) { N = n; E = e; }

        internal static Dyadic FromRaw(BigInteger n, int e)
        {
            RequireBits(Bits(n), "normalization input");
            Exponent(e);
            if (n.IsZero) return Zero;
            var zeros = checked((int)BigInteger.TrailingZeroCount(BigInteger.Abs(n)));
            var normalizedExponent = Exponent((long)e + zeros);
            return new(n >> zeros, normalizedExponent);
        }

        internal static Dyadic From(double value)
        {
            if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            var bits = BitConverter.DoubleToUInt64Bits(value);
            var encoded = (int)((bits >> 52) & 2047);
            var fraction = bits & 0x000f_ffff_ffff_ffffUL;
            BigInteger n = encoded == 0 ? fraction : fraction | (1UL << 52);
            if ((bits >> 63) != 0) n = -n;
            return FromRaw(n, encoded == 0 ? -1074 : encoded - 1075);
        }

        internal static Dyadic Integer(long value) => FromRaw(value, 0);
        internal static Dyadic Pow2(int e) => new(BigInteger.One, Exponent(e));
        internal Dyadic Abs() => Sign < 0 ? new(-N, E) : this;
        internal Dyadic Shifted(int e) => Sign == 0 ? Zero : new(N, Exponent((long)E + e));

        public static Dyadic operator -(Dyadic a) => new(-a.N, a.E);
        public static Dyadic operator -(Dyadic a, Dyadic b) => a + -b;

        public static Dyadic operator +(Dyadic a, Dyadic b)
        {
            if (a.Sign == 0) return b;
            if (b.Sign == 0) return a;
            var e = Math.Min(a.E, b.E);
            var x = Shift(a.N, checked(a.E - e));
            var y = Shift(b.N, checked(b.E - e));
            if (x.Sign == y.Sign && Math.Max(Bits(x), Bits(y)) == MaximumBits &&
                BigInteger.Abs(y) > Limit.Magnitude - BigInteger.Abs(x))
                throw new InvalidOperationException("Model A bounded arithmetic addition exceeds its significand limit.");
            return FromRaw(x + y, e);
        }

        public static Dyadic operator *(Dyadic a, Dyadic b)
        {
            if (a.Sign == 0 || b.Sign == 0) return Zero;
            var e = Exponent((long)a.E + b.E);
            var bits = (long)Bits(a.N) + Bits(b.N);
            if (bits > MaximumBits && (bits - 1 > MaximumBits ||
                BigInteger.Abs(a.N) > Limit.Magnitude / BigInteger.Abs(b.N)))
                throw new InvalidOperationException("Model A bounded arithmetic multiplication exceeds its significand limit.");
            return FromRaw(a.N * b.N, e);
        }

        public int CompareTo(Dyadic other) => (this - other).Sign;
        public bool Equals(Dyadic other) => N == other.N && E == other.E;
        public override bool Equals(object? other) => other is Dyadic value && Equals(value);
        public override int GetHashCode() => HashCode.Combine(N, E);
        internal static Dyadic Max(Dyadic a, Dyadic b) => a.CompareTo(b) >= 0 ? a : b;
        internal static Dyadic Min(Dyadic a, Dyadic b) => a.CompareTo(b) <= 0 ? a : b;
    }

    internal readonly struct Bounds
    {
        internal Dyadic Lower { get; }
        internal Dyadic Upper { get; }
        internal static Bounds Zero => default;
        internal static Bounds One => new(Dyadic.One, Dyadic.One);

        private Bounds(Dyadic lower, Dyadic upper) { Lower = lower; Upper = upper; }

        internal static Bounds Endpoints(Dyadic lower, Dyadic upper)
        {
            if (lower.CompareTo(upper) > 0) throw new ArgumentException("Interval endpoints are reversed.");
            return new(Round(lower).Lower, Round(upper).Upper);
        }

        internal static Bounds Point(double value) => Round(Dyadic.From(value));
        internal static Bounds Exact(Dyadic value) => Round(value);

        internal static Bounds Round(Dyadic value)
        {
            if (value.Sign == 0) return Zero;
            if (value.Sign < 0) return -Round(-value);
            var discard = Math.Max(Bits(value.N) - Precision, 0);
            if (discard == 0) return new(value, value);
            var units = value.N >> discard;
            var remainder = value.N - Shift(units, discard);
            var e = Exponent((long)value.E + discard);
            return new(Dyadic.FromRaw(units, e), Dyadic.FromRaw(units + (remainder.IsZero ? 0 : 1), e));
        }

        public static Bounds operator -(Bounds a) => new(-a.Upper, -a.Lower);
        public static Bounds operator +(Bounds a, Bounds b) =>
            new(Round(a.Lower + b.Lower).Lower, Round(a.Upper + b.Upper).Upper);
        public static Bounds operator -(Bounds a, Bounds b) => a + -b;
        public static Bounds operator *(Bounds a, Bounds b)
        {
            var p = a.Lower * b.Lower;
            var lower = p; var upper = p;
            p = a.Lower * b.Upper; lower = Dyadic.Min(lower, p); upper = Dyadic.Max(upper, p);
            p = a.Upper * b.Lower; lower = Dyadic.Min(lower, p); upper = Dyadic.Max(upper, p);
            p = a.Upper * b.Upper; lower = Dyadic.Min(lower, p); upper = Dyadic.Max(upper, p);
            return new(Round(lower).Lower, Round(upper).Upper);
        }

        public static Bounds operator /(Bounds a, Bounds b)
        {
            if (b.Lower.Sign <= 0 && b.Upper.Sign >= 0)
                throw new ArithmeticException("Model A interval denominator contains zero.");
            var q = Quotient(a.Lower, b.Lower);
            var lower = q.Lower; var upper = q.Upper;
            q = Quotient(a.Lower, b.Upper); lower = Dyadic.Min(lower, q.Lower); upper = Dyadic.Max(upper, q.Upper);
            q = Quotient(a.Upper, b.Lower); lower = Dyadic.Min(lower, q.Lower); upper = Dyadic.Max(upper, q.Upper);
            q = Quotient(a.Upper, b.Upper); lower = Dyadic.Min(lower, q.Lower); upper = Dyadic.Max(upper, q.Upper);
            return new(lower, upper);
        }

        internal static Bounds Quotient(Dyadic a, Dyadic b)
        {
            if (b.Sign == 0) throw new ArithmeticException("Model A exact quotient denominator is zero.");
            if (a.Sign == 0) return Zero;
            var x = BigInteger.Abs(a.N); var y = BigInteger.Abs(b.N);
            var j = Bits(x) - Bits(y);
            if (j >= 0 ? x < Shift(y, j) : Shift(x, -j) < y) j--;
            var e = Exponent((long)a.E - b.E);
            var grid = Exponent((long)j + e - (Precision - 1));
            var shift = checked(e - grid);
            var numerator = shift >= 0 ? Shift(x, shift) : x;
            var denominator = shift < 0 ? Shift(y, -shift) : y;
            var units = BigInteger.DivRem(numerator, denominator, out var remainder);
            var result = new Bounds(Dyadic.FromRaw(units, grid),
                Dyadic.FromRaw(units + (remainder.IsZero ? 0 : 1), grid));
            return a.Sign == b.Sign ? result : -result;
        }

        internal Bounds Abs()
        {
            var upper = Dyadic.Max(Lower.Abs(), Upper.Abs());
            var lower = Lower.Sign <= 0 && Upper.Sign >= 0 ? Dyadic.Zero : Dyadic.Min(Lower.Abs(), Upper.Abs());
            return new(lower, upper);
        }

        internal static Bounds Max(Bounds a, Bounds b) =>
            new(Dyadic.Max(a.Lower, b.Lower), Dyadic.Max(a.Upper, b.Upper));
    }

    internal static Bounds SquareRoot(Dyadic value)
    {
        if (value.Sign < 0) throw new ArithmeticException("Model A norm square is negative.");
        if (value.Sign == 0) return Bounds.Zero;
        var log = (long)Bits(value.N) - 1 + value.E;
        var j = log >= 0 ? log / 2 : (log - 1) / 2;
        var grid = Exponent(j - (Precision - 1));
        var shift = checked(value.E - 2 * grid);
        var m = shift >= 0 ? Shift(value.N, shift) : value.N >> -shift;
        var root = IntegerSquareRoot(m, out _);
        var lower = Dyadic.FromRaw(root, grid);
        var upper = (lower * lower).Equals(value) ? lower : Dyadic.FromRaw(root + 1, grid);
        return Bounds.Endpoints(lower, upper);
    }

    internal static BigInteger IntegerSquareRoot(BigInteger value, out int iterations)
    {
        if (value.Sign < 0) throw new ArithmeticException("Negative integer norm square.");
        var bits = Bits(value);
        if (bits > 256) throw new InvalidOperationException("Model A square root exceeds its 256-bit/128-iteration work limit.");
        iterations = (bits + 1) / 2;
        var root = BigInteger.Zero; var remainder = BigInteger.Zero;
        for (var digit = iterations - 1; digit >= 0; digit--)
        {
            var group = (value >> (2 * digit)) & 3;
            remainder = (remainder << 2) + group;
            var trial = (root << 2) + 1;
            root <<= 1;
            if (remainder >= trial) { remainder -= trial; root++; }
        }
        return root;
    }

    internal static Bounds Norm(Dyadic x, Dyadic y, Dyadic z) => SquareRoot(x * x + y * y + z * z);
    internal static Bounds Norm(Vector3d value) => Norm(Dyadic.From(value.X), Dyadic.From(value.Y), Dyadic.From(value.Z));

    internal static Bounds Norm(Bounds x, Bounds y, Bounds z)
    {
        static (Dyadic Lower, Dyadic Upper) Square(Bounds a)
        {
            var x = a.Lower * a.Lower; var y = a.Upper * a.Upper;
            return (a.Lower.Sign <= 0 && a.Upper.Sign >= 0 ? Dyadic.Zero : Dyadic.Min(x, y), Dyadic.Max(x, y));
        }
        var a = Square(x); var b = Square(y); var c = Square(z);
        return Bounds.Endpoints(SquareRoot(a.Lower + b.Lower + c.Lower).Lower,
            SquareRoot(a.Upper + b.Upper + c.Upper).Upper);
    }

    internal static Dyadic Spacing(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        var encoded = (int)((BitConverter.DoubleToUInt64Bits(value) >> 52) & 2047);
        return Dyadic.Pow2(encoded == 0 ? -1074 : encoded - 1075);
    }

    internal static readonly Dyadic J = Dyadic.Integer(10_000_000_000);
    internal static readonly Dyadic EightJ = J.Shifted(3);
    internal static bool Within(Bounds discrepancy, Bounds allowance) => discrepancy.Upper.CompareTo(allowance.Lower) <= 0;
    internal static bool Agreement(Dyadic gap, Dyadic t) => (J * gap.Abs()).CompareTo(t) <= 0;
    internal static bool Representation(Dyadic value, Dyadic t) => (EightJ * value).CompareTo(t) <= 0;
    internal static bool UpdateLoss(Dyadic loss6, Dyadic t) => (EightJ * loss6).CompareTo(Dyadic.Integer(6) * t) <= 0;

    internal static Dyadic StageLoss6(int stage, Dyadic anchor, Dyadic observed, Dyadic h, Dyadic previousRate) => stage switch
    {
        2 or 3 => Dyadic.Integer(3) * ((observed - anchor).Shifted(1) - h * previousRate).Abs(),
        4 => Dyadic.Integer(6) * ((observed - anchor) - h * previousRate).Abs(),
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    internal static Dyadic FinalLoss6(Dyadic anchor, Dyadic endpoint, Dyadic h, Dyadic weightedRate) =>
        (Dyadic.Integer(6) * (endpoint - anchor) - h * weightedRate).Abs();
}
