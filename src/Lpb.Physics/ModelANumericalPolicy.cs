using D = Lpb.Physics.ModelANumericalArithmetic.Dyadic;
using B = Lpb.Physics.ModelANumericalArithmetic.Bounds;
using static Lpb.Physics.ModelANumericalArithmetic;

namespace Lpb.Physics;

// Fixed B2-N policy. No user-selectable thresholds or physical-model alternatives.
internal static class ModelANumericalPolicy
{
    private static readonly string[] Names = ["position.X", "position.Y", "position.Z", "velocity.X", "velocity.Y", "velocity.Z",
        "angularVelocity.X", "angularVelocity.Y", "angularVelocity.Z"];
    internal static string Name(int component) => Names[component];

    internal static double Component(in SpinFlightState s, int i) => i switch
    {
        0 => s.Translation.Position.X,
        1 => s.Translation.Position.Y,
        2 => s.Translation.Position.Z,
        3 => s.Translation.Velocity.X,
        4 => s.Translation.Velocity.Y,
        5 => s.Translation.Velocity.Z,
        6 => s.AngularVelocity.X,
        7 => s.AngularVelocity.Y,
        8 => s.AngularVelocity.Z,
        _ => throw new ArgumentOutOfRangeException(nameof(i))
    };

    internal static double Component(in SpinFlightDerivative s, int i) => i switch
    {
        0 => s.PositionXRate,
        1 => s.PositionYRate,
        2 => s.PositionZRate,
        3 => s.VelocityXRate,
        4 => s.VelocityYRate,
        5 => s.VelocityZRate,
        6 => s.AngularVelocityXRate,
        7 => s.AngularVelocityYRate,
        8 => s.AngularVelocityZRate,
        _ => throw new ArgumentOutOfRangeException(nameof(i))
    };

    internal sealed class Invariants
    {
        private readonly bool[] constant = new bool[9];
        private readonly SpinFlightState release;
        internal bool Stationary { get; }
        internal bool ConstantSpin { get; }
        internal bool this[int i] => constant[i];

        internal Invariants(ModelAFlightRequest request, ModelAParameters parameters)
        {
            release = request.InitialState;
            var v = release.Translation.Velocity;
            var w = release.AngularVelocity;
            Stationary = request.GravityMagnitude == 0 && v == default(Vector3d);
            ConstantSpin = Stationary || parameters.Rotation.Kind == ModelARotationKind.R0 ||
                parameters.Aerodynamics.AirDensity == 0 || parameters.Rotation.Beta == 0;
            var liftAbsent = parameters.Aerodynamics.AirDensity == 0 || w == default(Vector3d);
            var dragAbsent = parameters.Aerodynamics.AirDensity == 0 || parameters.Aerodynamics.DragCoefficient == 0;
            for (var i = 0; i < 3; i++)
            {
                var noGravity = i != 2 || request.GravityMagnitude == 0;
                var zeroVelocity = Component(release, i + 3) == 0;
                constant[i] = Stationary || (liftAbsent && noGravity && zeroVelocity);
                constant[i + 3] = Stationary || (liftAbsent && noGravity && (zeroVelocity || dragAbsent));
                constant[i + 6] = ConstantSpin || Component(release, i + 6) == 0;
            }
        }

        internal void CheckState(SpinFlightState state)
        {
            for (var i = 0; i < 9; i++)
            {
                if (!double.IsFinite(Component(state, i))) throw new ArithmeticException($"{Name(i)} state is nonfinite.");
                if (constant[i] && Component(state, i) != Component(release, i))
                    throw new ArithmeticException($"{Name(i)} violates its release-established constant invariant.");
            }
        }

        internal void CheckDerivative(SpinFlightDerivative derivative)
        {
            for (var i = 0; i < 9; i++)
            {
                if (!double.IsFinite(Component(derivative, i))) throw new ArithmeticException($"{Name(i)} derivative is nonfinite.");
                if (constant[i] && Component(derivative, i) != 0)
                    throw new ArithmeticException($"{Name(i)} constant invariant has a nonzero derivative.");
            }
        }
    }

    internal sealed class Rates
    {
        // Neighbors enclose mathematical pi and the actual B1 Math.PI point.
        internal static readonly B Pi = B.Endpoints(D.From(Math.BitDecrement(Math.PI)), D.From(Math.BitIncrement(Math.PI)));
        internal static readonly B NathanA = B.Endpoints(
            D.Min(B.Quotient(D.Integer(42), D.Integer(125)).Lower, D.From(.336)),
            D.Max(B.Quotient(D.Integer(42), D.Integer(125)).Upper, D.From(.336)));
        internal static readonly B NathanK = B.Endpoints(
            D.Min(B.Quotient(D.Integer(6041), D.Integer(1000)).Lower, D.From(6.041)),
            D.Max(B.Quotient(D.Integer(6041), D.Integer(1000)).Upper, D.From(6.041)));
        internal B Drag { get; }
        internal B Magnus { get; }
        internal B Gamma { get; }
        private readonly B radius;

        internal Rates(ModelAParameters parameters)
        {
            var a = parameters.Aerodynamics;
            radius = B.Point(a.BallRadius);
            var common = B.Point(a.AirDensity) * Pi * radius * radius / (B.Point(2) * B.Point(a.BallMass));
            Drag = common * B.Point(a.DragCoefficient);
            Magnus = common * NathanA * NathanK * radius;
            Gamma = parameters.Rotation.Kind == ModelARotationKind.R0 ? B.Zero :
                B.Point(a.AirDensity) * Pi * radius * radius * radius * radius * B.Point(parameters.Rotation.Beta!.Value) /
                B.Point(parameters.Rotation.RotationalInertia!.Value);
        }

        internal void Check(double interval, SpinFlightState state)
        {
            var h = B.Point(interval);
            var speed = Norm(state.Translation.Velocity);
            var spin = Norm(state.AngularVelocity);
            if (!Within(h * Gamma * speed, B.One))
                throw new ArithmeticException("R1-U damping restriction h*lambda <= 1 is exceeded or unresolved.");
            // Norm Lipschitz bounds apply also at U=0; never form v/U.
            var translational = B.Point(2) * Drag * speed + B.Point(3) * Magnus * spin + Magnus * speed / radius;
            var rotational = Gamma * (radius * spin + speed);
            if (!Within(h * B.Max(translational, rotational), B.One))
                throw new ArithmeticException("Coupled sensitivity restriction h*L <= 1 is exceeded or unresolved.");
        }
    }

    internal static D[] Budgets(SpinFlightState a, SpinFlightState full, SpinFlightState diagnostic, double interval)
    {
        var t = new D[9]; var h = D.From(interval);
        for (var i = 0; i < 9; i++)
        {
            var anchor = D.From(Component(a, i));
            D scale;
            if (i < 3)
                scale = D.Max(D.Max((D.From(Component(full, i)) - anchor).Abs(),
                    (D.From(Component(diagnostic, i)) - anchor).Abs()), h * D.From(Component(a, i + 3)).Abs());
            else if (i < 6)
                scale = D.Max(anchor.Abs(), D.Max(D.From(Component(full, i)).Abs(), D.From(Component(diagnostic, i)).Abs()));
            else scale = anchor.Abs();
            t[i] = D.Integer(100) + scale;
        }
        return t;
    }

    internal static void CheckAngularPath(SpinFlightState anchor, SpinFlightState endpoint, bool constant)
    {
        for (var i = 6; i < 9; i++)
        {
            var a = Component(anchor, i); var b = Component(endpoint, i);
            if (constant || a == 0)
            {
                if (b != a) throw new ArithmeticException($"{Name(i)} angular preservation failed.");
            }
            else
            {
                if (b == 0) throw new ArithmeticException($"{Name(i)} numerical angular extinction.");
                if (Math.Sign(a) != Math.Sign(b)) throw new ArithmeticException($"{Name(i)} angular sign reversed.");
                if (Math.Abs(b) > Math.Abs(a)) throw new ArithmeticException($"{Name(i)} angular magnitude expanded.");
            }
        }
    }

    internal static void CheckAxis(SpinFlightState anchor, SpinFlightState endpoint, D[] t)
    {
        var dominant = 6;
        for (var i = 7; i < 9; i++)
            if (Math.Abs(Component(anchor, i)) > Math.Abs(Component(anchor, dominant))) dominant = i;
        var ad = D.From(Component(anchor, dominant));
        if (ad.Sign == 0)
        {
            for (var i = 6; i < 9; i++)
                if (Component(endpoint, i) != 0) throw new ArithmeticException($"{Name(i)} zero-axis preservation failed.");
            return;
        }
        var bd = D.From(Component(endpoint, dominant));
        for (var i = 6; i < 9; i++)
        {
            var residual = (D.From(Component(endpoint, i)) * ad - bd * D.From(Component(anchor, i))).Abs();
            if ((J * residual).CompareTo(t[i] * ad.Abs()) > 0)
                throw new ArithmeticException($"{Name(i)} common-axis residual exceeds its exact angular budget.");
        }
    }

    internal static void CheckPhysicalPath(SpinFlightState a, SpinFlightState b, double interval, double gravity,
        D[] t, B velocityBudgetNorm, D velocityBudgetSquare)
    {
        var ua = Norm(a.Translation.Velocity); var ub = Norm(b.Translation.Velocity);
        var g = D.From(gravity); var elapsed = D.From(interval); var j = B.Exact(J);
        var rise = B.Exact(g * elapsed);
        if (!Within(j * ub, j * ua + B.Exact(J * g * elapsed) + velocityBudgetNorm))
            throw new ArithmeticException("Completed-path speed compatibility is exceeded or unresolved.");
        var change = D.Zero;
        for (var i = 3; i < 6; i++)
        {
            var av = D.From(Component(a, i)); var bv = D.From(Component(b, i));
            change += (bv - av) * (bv + av);
        }
        change += D.Integer(2) * g * (D.From(b.Translation.Position.Z) - D.From(a.Translation.Position.Z));
        var left = J * J * change;
        var right = B.Point(2) * j * (ua + rise) * velocityBudgetNorm +
            B.Exact(velocityBudgetSquare) + B.Exact(D.Integer(2) * J * g * t[2]);
        if (left.CompareTo(right.Lower) > 0)
            throw new ArithmeticException("Completed-path energy compatibility is exceeded or unresolved.");
    }
}
