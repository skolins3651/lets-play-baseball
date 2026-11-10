namespace Lpb.Physics;

internal sealed class GravityDragDynamics
{
    private readonly double gravity;
    private readonly bool disabled;
    private readonly PositiveProduct coefficient;

    internal GravityDragDynamics(double gravityMagnitude, AerodynamicSettings aerodynamics)
    {
        ArgumentNullException.ThrowIfNull(aerodynamics);
        if (!double.IsFinite(gravityMagnitude) || gravityMagnitude < 0)
            throw new ArgumentOutOfRangeException(nameof(gravityMagnitude));
        gravity = gravityMagnitude;
        disabled = aerodynamics.Disabled;
        if (!disabled)
            coefficient = PositiveProduct.From(aerodynamics.AirDensity)
                .Times(aerodynamics.DragCoefficient).Times(Math.PI)
                .Times(aerodynamics.BallRadius).Times(aerodynamics.BallRadius)
                .DividedBy(aerodynamics.BallMass).Halved();
    }

    internal FlightDerivative Evaluate(double time, FlightState state)
    {
        var v = state.Velocity;
        var scale = Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z)));
        if (disabled || scale == 0)
            return new(v.X, v.Y, v.Z, 0, 0, -gravity);
        var x = v.X / scale;
        var y = v.Y / scale;
        var z = v.Z / scale;
        var normFactor = Math.Sqrt(x * x + y * y + z * z);
        var rate = coefficient.Times(scale).Times(normFactor);
        var az = Drag(rate, v.Z, "velocity.Z") - gravity;
        if (!double.IsFinite(az))
            throw new ArithmeticException("Drag plus gravity: velocity.Z acceleration is nonfinite.");
        return new(v.X, v.Y, v.Z, Drag(rate, v.X, "velocity.X"), Drag(rate, v.Y, "velocity.Y"), az);
    }

    private static double Drag(PositiveProduct rate, double velocity, string component)
    {
        if (velocity == 0) return 0;
        var value = rate.Times(Math.Abs(velocity)).ToAcceleration(component);
        return velocity > 0 ? -value : value;
    }

    // Only positive drag factors use this helper. No arbitrary precision or general arithmetic API.
    private readonly record struct PositiveProduct(double Mantissa, int Exponent)
    {
        internal static PositiveProduct From(double value)
        {
            var exponent = Math.ILogB(value);
            return new(Math.ScaleB(value, -exponent), exponent);
        }

        internal PositiveProduct Times(double value)
        {
            var other = From(value);
            return Normalize(Mantissa * other.Mantissa, Exponent + other.Exponent);
        }

        internal PositiveProduct DividedBy(double value)
        {
            var other = From(value);
            return Normalize(Mantissa / other.Mantissa, Exponent - other.Exponent);
        }

        internal PositiveProduct Halved() => new(Mantissa, Exponent - 1);

        private static PositiveProduct Normalize(double mantissa, int exponent)
        {
            var adjustment = Math.ILogB(mantissa);
            return new(Math.ScaleB(mantissa, -adjustment), exponent + adjustment);
        }

        internal double ToAcceleration(string component)
        {
            var value = Math.ScaleB(Mantissa, Exponent);
            if (!double.IsFinite(value) || value == 0)
                throw new ArithmeticException($"Drag {component} acceleration {(value == 0 ? "underflowed to zero" : "overflowed")}.");
            return value;
        }
    }
}
