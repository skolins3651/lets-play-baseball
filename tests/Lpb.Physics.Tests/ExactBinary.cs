using System.Numerics;

namespace Lpb.Physics.Tests;

// Verification-only dyadics. No production arithmetic, rounding, or root solver is reused.
internal readonly struct ExactBinary : IComparable<ExactBinary>
{
    private readonly BigInteger significand;
    private readonly int exponent;

    private ExactBinary(BigInteger significand, int exponent)
    {
        this.significand = significand;
        this.exponent = exponent;
    }

    internal static ExactBinary From(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        var bits = BitConverter.DoubleToUInt64Bits(value);
        var encodedExponent = (int)((bits >> 52) & 0x7ff);
        var fraction = bits & 0x000f_ffff_ffff_ffff;
        var integer = new BigInteger(encodedExponent == 0 ? fraction : fraction | (1UL << 52));
        if ((bits >> 63) != 0)
            integer = -integer;
        return new ExactBinary(integer, encodedExponent == 0 ? -1074 : encodedExponent - 1023 - 52);
    }

    internal int Sign => significand.Sign;
    internal ExactBinary Half() => new(significand, exponent - 1);

    public int CompareTo(ExactBinary other) => (this - other).Sign;
    public override string ToString() => $"{significand} * 2^{exponent}";

    public static ExactBinary operator +(ExactBinary first, ExactBinary second)
    {
        var common = Math.Min(first.exponent, second.exponent);
        return new ExactBinary((first.significand << (first.exponent - common)) +
            (second.significand << (second.exponent - common)), common);
    }

    public static ExactBinary operator -(ExactBinary value) => new(-value.significand, value.exponent);
    public static ExactBinary operator -(ExactBinary first, ExactBinary second) => first + -second;
    public static ExactBinary operator *(ExactBinary first, ExactBinary second) =>
        new(first.significand * second.significand, first.exponent + second.exponent);
}
