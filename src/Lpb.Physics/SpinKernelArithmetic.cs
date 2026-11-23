using System.Numerics;

namespace Lpb.Physics;

// B1-D arithmetic only: normalized words, a four-word determinant expansion, and
// the specified Nathan series/doubling sequence. Never advances a flight state.
internal static class SpinKernelArithmetic
{
    internal readonly struct ScaledNumber
    {
        internal int Sign { get; }
        internal double Mantissa { get; }
        internal int Exponent { get; }
        internal bool IsZero => Sign == 0;

        private ScaledNumber(int sign, double mantissa, int exponent)
        {
            Sign = sign;
            Mantissa = mantissa;
            Exponent = exponent;
        }

        internal static ScaledNumber From(double value)
        {
            if (!double.IsFinite(value))
                throw new ArithmeticException("Model A scalar decomposition requires a finite value.");
            var bits = BitConverter.DoubleToUInt64Bits(value);
            var fraction = bits & 0x000f_ffff_ffff_ffffUL;
            var encoded = (int)((bits >> 52) & 0x7ff);
            if (encoded == 0 && fraction == 0)
                return default;
            ulong significand;
            int exponent;
            if (encoded == 0)
            {
                var leading = BitOperations.Log2(fraction);
                significand = fraction << (52 - leading);
                exponent = leading - 1074;
            }
            else
            {
                significand = fraction | (1UL << 52);
                exponent = encoded - 1023;
            }
            return new((bits >> 63) == 0 ? 1 : -1, Math.ScaleB(significand, -52), exponent);
        }

        private static ScaledNumber Normalize(double word, int exponent)
        {
            var normalized = From(word);
            if (normalized.IsZero)
                throw new ArithmeticException("Model A normalization lost a proven nonzero value.");
            return new(normalized.Sign, normalized.Mantissa, checked(exponent + normalized.Exponent));
        }

        internal ScaledNumber Times(ScaledNumber other) =>
            IsZero || other.IsZero ? default :
            Normalize(Sign * other.Sign * (Mantissa * other.Mantissa), checked(Exponent + other.Exponent));

        internal ScaledNumber DividedBy(ScaledNumber other)
        {
            if (other.IsZero)
                throw new ArithmeticException("Model A scaled division has a zero denominator.");
            return IsZero ? default :
                Normalize(Sign * other.Sign * (Mantissa / other.Mantissa), checked(Exponent - other.Exponent));
        }

        internal ScaledNumber Shifted(int exponent) =>
            IsZero ? default : new(Sign, Mantissa, checked(Exponent + exponent));

        internal ScaledNumber Negated() => IsZero ? default : new(-Sign, Mantissa, Exponent);

        internal double ToDouble(string context)
        {
            if (IsZero)
                return 0;
            if (Exponent > 1023)
                throw new ArithmeticException($"{context}: final conversion exceeds finite binary64 range.");
            var sign = Sign < 0 ? 1UL << 63 : 0;
            var significand = (BitConverter.DoubleToUInt64Bits(Mantissa) & 0x000f_ffff_ffff_ffffUL) | (1UL << 52);
            if (Exponent >= -1022)
                return BitConverter.UInt64BitsToDouble(sign | ((ulong)(Exponent + 1023) << 52) |
                    (significand - (1UL << 52)));

            // One rounding in minimum-subnormal units; no intermediate tiny double.
            var shift = -(long)Exponent - 1022;
            if (shift >= 54)
                throw new ArithmeticException($"{context}: nonzero component rounds to zero in final conversion.");
            var count = (int)shift;
            var units = significand >> count;
            var remainder = significand & ((1UL << count) - 1);
            var half = 1UL << (count - 1);
            if (remainder > half || (remainder == half && (units & 1) != 0))
                units++;
            if (units == 0)
                throw new ArithmeticException($"{context}: nonzero component rounds to zero in final conversion.");
            // units == 2^52 naturally encodes the minimum normal value.
            return BitConverter.UInt64BitsToDouble(sign | units);
        }

        internal static ScaledNumber FromWord(double word, int exponent) => Normalize(word, exponent);
    }

    // Each literal is the correctly rounded exact (-1)^j/(j+1)!; independently
    // certified by the test-only rational oracle, including its rounding cell.
    private static readonly double[] PhiCoefficients =
    [
        1, -.5, .16666666666666666, -.041666666666666664,
        .008333333333333333, -.001388888888888889, .0001984126984126984,
        -2.48015873015873e-5, 2.7557319223985893e-6, -2.755731922398589e-7,
        2.505210838544172e-8, -2.08767569878681e-9, 1.6059043836821613e-10,
        -1.1470745597729725e-11, 7.647163731819816e-13, -4.779477332387385e-14
    ];

    internal static ScaledNumber Norm(Vector3d vector) =>
        Norm(ScaledNumber.From(vector.X), ScaledNumber.From(vector.Y), ScaledNumber.From(vector.Z));

    internal static ScaledNumber Norm(ScaledNumber x, ScaledNumber y, ScaledNumber z)
    {
        if (x.IsZero && y.IsZero && z.IsZero)
            return default;
        Span<ScaledNumber> components = [x, y, z];
        var exponent = int.MinValue;
        foreach (var value in components)
            if (!value.IsZero)
                exponent = Math.Max(exponent, value.Exponent);
        var sum = 0d;
        foreach (var value in components)
        {
            if (value.IsZero)
                continue;
            var gap = (long)exponent - value.Exponent;
            if (gap >= 64)
                continue; // Only the norm omits it; the original component is retained.
            var relative = Math.ScaleB(value.Mantissa, -(int)gap);
            sum = Math.FusedMultiplyAdd(relative, relative, sum);
        }
        return ScaledNumber.FromWord(Math.Sqrt(sum), exponent);
    }

    internal static (ScaledNumber X, ScaledNumber Y, ScaledNumber Z) Cross(Vector3d spin, Vector3d velocity) =>
        (Determinant(spin.Y, velocity.Z, spin.Z, velocity.Y),
         Determinant(spin.Z, velocity.X, spin.X, velocity.Z),
         Determinant(spin.X, velocity.Y, spin.Y, velocity.X));

    internal static ScaledNumber Determinant(double a, double b, double c, double d)
    {
        var p = ProductParts(a, b);
        var q = ProductParts(c, d);
        if (p.High == 0 && q.High == 0)
            return default;
        if (p.High != 0 && q.High != 0 && Math.Abs((long)p.Exponent - q.Exponent) >= 128)
            return p.Exponent > q.Exponent
                ? ScaledNumber.FromWord(p.High, p.Exponent)
                : ScaledNumber.FromWord(-q.High, q.Exponent);

        var exponent = p.High == 0 ? q.Exponent : q.High == 0 ? p.Exponent : Math.Max(p.Exponent, q.Exponent);
        Span<double> terms = stackalloc double[4];
        terms.Clear();
        if (p.High != 0)
        {
            terms[0] = Math.ScaleB(p.Low, p.Exponent - exponent);
            terms[1] = Math.ScaleB(p.High, p.Exponent - exponent);
        }
        if (q.High != 0)
        {
            terms[2] = -Math.ScaleB(q.Low, q.Exponent - exponent);
            terms[3] = -Math.ScaleB(q.High, q.Exponent - exponent);
        }
        Span<double> expansion = stackalloc double[4];
        Span<double> work = stackalloc double[4];
        var length = 0;
        foreach (var term in terms)
        {
            length = Grow(expansion[..length], term, work);
            work[..length].CopyTo(expansion);
        }
        length = Compress(expansion[..length], work);
        if (length == 0)
            return default;
        // Compression's largest word has the exact determinant sign and <1 ulp
        // absolute error. It is an estimate, not a correctly rounded determinant.
        return ScaledNumber.FromWord(expansion[length - 1], exponent);
    }

    private static (double High, double Low, int Exponent) ProductParts(double a, double b)
    {
        var first = ScaledNumber.From(a);
        var second = ScaledNumber.From(b);
        if (first.IsZero || second.IsZero)
            return default;
        var x = first.Sign * first.Mantissa;
        var y = second.Sign * second.Mantissa;
        var high = x * y;
        return (high, Math.FusedMultiplyAdd(x, y, -high), checked(first.Exponent + second.Exponent));
    }

    private static (double Sum, double Error) TwoSum(double first, double second)
    {
        var sum = first + second;
        var virtualSecond = sum - first;
        var virtualFirst = sum - virtualSecond;
        var secondError = second - virtualSecond;
        var firstError = first - virtualFirst;
        return (sum, firstError + secondError);
    }

    private static int Grow(ReadOnlySpan<double> expansion, double term, Span<double> output)
    {
        var accumulator = term;
        var count = 0;
        foreach (var word in expansion)
        {
            var (sum, error) = TwoSum(accumulator, word);
            if (error != 0)
                output[count++] = error;
            accumulator = sum;
        }
        if (accumulator != 0)
            output[count++] = accumulator;
        return count;
    }

    private static int Compress(Span<double> expansion, Span<double> work)
    {
        if (expansion.IsEmpty)
            return 0;
        var accumulator = expansion[^1];
        var bottom = expansion.Length;
        for (var i = expansion.Length - 2; i >= 0; i--)
        {
            var (sum, error) = TwoSum(accumulator, expansion[i]);
            if (error != 0)
            {
                work[--bottom] = sum;
                accumulator = error;
            }
            else
                accumulator = sum;
        }
        work[--bottom] = accumulator;
        accumulator = work[bottom];
        var count = 0;
        for (var i = bottom + 1; i < expansion.Length; i++)
        {
            var (sum, error) = TwoSum(work[i], accumulator);
            if (error != 0)
                expansion[count++] = error;
            accumulator = sum;
        }
        if (accumulator != 0)
            expansion[count++] = accumulator;
        return count;
    }

    internal static ScaledNumber Phi(ScaledNumber argument)
    {
        if (argument.Sign < 0)
            throw new ArithmeticException("Nathan phi requires a nonnegative argument.");
        if (argument.IsZero || argument.Exponent < -53 ||
            (argument.Exponent == -53 && argument.Mantissa == 1))
            return ScaledNumber.From(1);
        if (argument.Exponent >= 6)
            return ScaledNumber.From(1).DividedBy(argument);

        var reduced = argument.ToDouble("Nathan phi bounded argument");
        var halvings = 0;
        while (reduced > .5)
        {
            reduced /= 2;
            halvings++;
        }
        var value = PhiCoefficients[^1];
        for (var j = PhiCoefficients.Length - 2; j >= 0; j--)
            value = Math.FusedMultiplyAdd(reduced, value, PhiCoefficients[j]);
        for (var j = 0; j < halvings; j++)
        {
            value *= Math.FusedMultiplyAdd(-reduced / 2, value, 1);
            reduced *= 2;
        }
        if (!double.IsFinite(value) || value <= 0)
            throw new ArithmeticException("Nathan phi series/doubling lost a finite positive value.");
        return ScaledNumber.From(value);
    }
}
