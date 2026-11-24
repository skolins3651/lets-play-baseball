using NUnit.Framework;
using D = Lpb.Physics.ModelANumericalArithmetic.Dyadic;
using B = Lpb.Physics.ModelANumericalArithmetic.Bounds;
using Q = Lpb.Physics.Tests.SpinReference.Rational;
using I = Lpb.Physics.Tests.SpinReference.Interval;
using static Lpb.Physics.ModelANumericalArithmetic;
using static Lpb.Physics.ModelANumericalPolicy;
using static Lpb.Physics.Tests.ModelANumericalArithmeticTests;

namespace Lpb.Physics.Tests;

internal static class ModelAI3Fixtures
{
    internal static SpinFlightState State(Vector3d? position = null, Vector3d? velocity = null, Vector3d? spin = null) =>
        new(new FlightState(position ?? default, velocity ?? default), spin ?? default);

    internal static ModelAParameters Baseball(bool damping = true, double density = 1.225, double drag = .35, double beta = .009) =>
        ModelAParameters.NathanAsrB(new(.145, .0366, density, drag),
            damping ? ModelARotationSettings.R1U(.4 * .145 * .0366 * .0366, beta) : ModelARotationSettings.R0());

    internal static ModelAFlightRequest Flight(double h = .01, double duration = 2, int spinCase = 0) =>
        new(State(new(0, 0, 1.0366), new(30, 20, 25), spinCase switch
        {
            0 => new(-100, 150, 0),
            1 => new(-50, 200, 80),
            _ => new(150, 100, 125)
        }), 9.80665, duration, h);

    internal static D[] FloorBudgets() => Enumerable.Repeat(D.Integer(100), 9).ToArray();

    internal static SpinFlightState WithComponent(SpinFlightState state, int component, double value)
    {
        var x = state.Translation.Position; var v = state.Translation.Velocity; var w = state.AngularVelocity;
        return State(new(component == 0 ? value : x.X, component == 1 ? value : x.Y, component == 2 ? value : x.Z),
            new(component == 3 ? value : v.X, component == 4 ? value : v.Y, component == 5 ? value : v.Z),
            new(component == 6 ? value : w.X, component == 7 ? value : w.Y, component == 8 ? value : w.Z));
    }
}

public class ModelANumericalPolicyTests
{
    [Test]
    public void CoefficientEnclosuresCoverScientificRationalsB1ConstantsAndIndependentMachinPi()
    {
        Contains(Rates.NathanA, new Q(42, 125));
        Contains(Rates.NathanA, Q.From(.336));
        Contains(Rates.NathanK, new Q(6041, 1000));
        Contains(Rates.NathanK, Q.From(6.041));
        Contains(Rates.Pi, Q.From(Math.PI));
        Assert.That(Exact(Rates.Pi.Lower).CompareTo(SpinReference.Pi.Lower), Is.Negative);
        Assert.That(Exact(Rates.Pi.Upper).CompareTo(SpinReference.Pi.Upper), Is.Positive);
        var parameters = ModelAI3Fixtures.Baseball();
        var a = parameters.Aerodynamics;
        var r = I.Point(Q.From(a.BallRadius));
        var common = I.Point(Q.From(a.AirDensity) / (new Q(2, 1) * Q.From(a.BallMass))) * SpinReference.Pi * r * r;
        var rates = new Rates(parameters);
        var drag = common * I.Point(Q.From(a.DragCoefficient));
        var gamma = I.Point(Q.From(a.AirDensity) * Q.From(parameters.Rotation.Beta!.Value) /
            Q.From(parameters.Rotation.RotationalInertia!.Value)) * SpinReference.Pi * r * r * r * r;
        foreach (var c in new[] { new Q(42, 125), Q.From(.336) })
            foreach (var k in new[] { new Q(6041, 1000), Q.From(6.041) })
            {
                var magnus = common * I.Point(c * k) * r;
                Contains(rates.Magnus, magnus.Lower); Contains(rates.Magnus, magnus.Upper);
            }
        Contains(rates.Drag, drag.Lower); Contains(rates.Drag, drag.Upper);
        Contains(rates.Gamma, gamma.Lower); Contains(rates.Gamma, gamma.Upper);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void EveryComponentHasItsOwnBudgetAndAgreement(int component)
    {
        var anchor = ModelAI3Fixtures.State(new(1, -2, 3), new(2, -3, 4), new(5, -6, 7));
        var endpoint = ModelAI3Fixtures.WithComponent(anchor, component, Component(anchor, component) + 1);
        var t = Budgets(anchor, endpoint, anchor, .5);
        for (var i = 0; i < 9; i++)
        {
            var a = Q.From(Component(anchor, i)); var f = Q.From(Component(endpoint, i));
            Q scale;
            if (i < 3)
            {
                var displacement = (f - a).Abs();
                var initialMotion = Q.From(.5) * Q.From(Component(anchor, i + 3)).Abs();
                scale = displacement.CompareTo(initialMotion) > 0 ? displacement : initialMotion;
            }
            else if (i < 6) scale = a.Abs().CompareTo(f.Abs()) > 0 ? a.Abs() : f.Abs();
            else scale = a.Abs();
            Assert.That(Exact(t[i]), Is.EqualTo(new Q(100, 1) + scale));
            Assert.That(Agreement(D.From(Component(endpoint, i)) - D.From(Component(anchor, i)), t[i]),
                Is.EqualTo(i != component), Name(i));
        }
        // A separate giant X budget never masks another component's discrepancy.
        Assert.That(Agreement(D.From(1e-7), D.Integer(100)), Is.False);
    }

    [Test]
    public void PositionBudgetsUseExactMotionRatherThanWorldOriginOrRoundedSubtraction()
    {
        var a = ModelAI3Fixtures.State(new(Math.ScaleB(1, 800), 0, 0), new(1, 0, 0));
        var f = ModelAI3Fixtures.WithComponent(a, 0, -Math.ScaleB(1, 800));
        Assert.That(Budgets(a, f, a, .5)[0], Is.EqualTo(D.Integer(100) + D.Pow2(801)));
        Assert.That(Budgets(a, a, a, .5)[0], Is.EqualTo(D.From(100.5)));
    }

    [TestCase(false, 1.225, .009)]
    [TestCase(true, 0, .009)]
    [TestCase(true, 1.225, 0)]
    public void ConstantSpinCatalogueIsReleaseAndPolicyBased(bool damping, double density, double beta)
    {
        var request = ModelAI3Fixtures.Flight();
        var invariant = new Invariants(request, ModelAI3Fixtures.Baseball(damping, density, beta: beta));
        Assert.That(invariant.ConstantSpin, Is.True);
        for (var i = 6; i < 9; i++) Assert.That(invariant[i], Is.True);
        Assert.Throws<ArithmeticException>(() => invariant.CheckState(ModelAI3Fixtures.WithComponent(request.InitialState, 6, -99)));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void StationaryCatalogueChecksValuesAndZeroDerivativesForEveryComponent(int component)
    {
        var state = ModelAI3Fixtures.State(new(double.MaxValue, -double.MaxValue, 0), spin: new(1e200, -1e200, 3));
        var invariants = new Invariants(new(state, 0, 1, 1), ModelAI3Fixtures.Baseball());
        Assert.That(invariants.Stationary && invariants[component], Is.True);
        var changed = Component(state, component) == 0 ? 1 : 0;
        Assert.Throws<ArithmeticException>(() => invariants.CheckState(ModelAI3Fixtures.WithComponent(state, component, changed)));
        var d = new double[9]; d[component] = 1;
        Assert.Throws<ArithmeticException>(() => invariants.CheckDerivative(new(d[0], d[1], d[2], d[3], d[4], d[5], d[6], d[7], d[8])));
    }

    [Test]
    public void ClosedCatalogueDoesNotInferGyroOrGravityInactivity()
    {
        var gyro = new Invariants(ModelAI3Fixtures.Flight(spinCase: 2), ModelAI3Fixtures.Baseball());
        for (var i = 0; i < 9; i++) Assert.That(gyro[i], Is.False, Name(i));
        var rest = new Invariants(new(ModelAI3Fixtures.State(spin: new(1, 2, 0)), 9.8, 1, .01), ModelAI3Fixtures.Baseball());
        Assert.That(rest.Stationary, Is.False);
        for (var i = 0; i < 6; i++) Assert.That(rest[i], Is.False, Name(i));
        Assert.That(rest[8], Is.True);
        Assert.Throws<ArithmeticException>(() => rest.CheckState(ModelAI3Fixtures.State(spin: new(1, 2, double.Epsilon))));
    }

    [Test]
    public void NoLiftReductionsClassifyOnlyProvenAxes()
    {
        var state = ModelAI3Fixtures.State(velocity: new(3, 0, 1));
        var request = new ModelAFlightRequest(state, 9.8, 1, .01);
        var drag = new Invariants(request, ModelAI3Fixtures.Baseball());
        Assert.That(Enumerable.Range(0, 6).Where(i => drag[i]), Is.EqualTo(new[] { 1, 4 }));
        var noDrag = new Invariants(request, ModelAI3Fixtures.Baseball(drag: 0));
        Assert.That(Enumerable.Range(0, 6).Where(i => noDrag[i]), Is.EqualTo(new[] { 1, 3, 4 }));
    }

    [TestCase(0d)]
    [TestCase(1e-15)]
    [TestCase(1d)]
    public void RatesAreRegularAtZeroSpeedWithoutDivisionBySpeed(double speed)
    {
        var rates = new Rates(ModelAI3Fixtures.Baseball());
        Assert.DoesNotThrow(() => rates.Check(.001, ModelAI3Fixtures.State(velocity: new(speed, 0, 0), spin: new(0, 100, 0))));
    }

    [Test]
    public void ZeroObservedLiftDoesNotRemovePerturbationCoupling()
    {
        var p = ModelAParameters.NathanAsrB(new(1, 1, 1, 0), ModelARotationSettings.R0());
        var state = ModelAI3Fixtures.State(velocity: new(1, 0, 0));
        Assert.Throws<ArithmeticException>(() => new Rates(p).Check(1, state));
        var noAir = ModelAParameters.NathanAsrB(new(1, 1, 0, 0), ModelARotationSettings.R0());
        Assert.DoesNotThrow(() => new Rates(noAir).Check(1, state));
    }

    [TestCase(.999999, true)]
    [TestCase(1.000001, false)]
    public void RateBoundaryIsConservativeOnBothSides(double target, bool accepted)
    {
        // gamma*pi^-1 input is binary64, so bracket 1 by a comfortably separated amount.
        var p = ModelAParameters.NathanAsrB(new(1e30, 1, 1 / Math.PI, 0), ModelARotationSettings.R1U(1, 1));
        var state = ModelAI3Fixtures.State(velocity: new(1, 0, 0));
        if (accepted) Assert.DoesNotThrow(() => new Rates(p).Check(target, state));
        else Assert.That(Assert.Throws<ArithmeticException>(() => new Rates(p).Check(target, state))!.Message, Does.Contain("damping"));
    }

    [Test]
    public void VariableRateDampingCornerCertificateIsThreeEighthsThroughOne()
    {
        var endpoints = new List<Q>();
        for (var mask = 0; mask < 16; mask++)
        {
            var a = Enumerable.Range(0, 4).Select(i => new Q((mask >> i) & 1, 1)).ToArray();
            var p2 = Q.One - a[0] / new Q(2, 1);
            var p3 = Q.One - a[1] * p2 / new Q(2, 1);
            var p4 = Q.One - a[2] * p3;
            var p = Q.One - (a[0] + new Q(2, 1) * a[1] * p2 + new Q(2, 1) * a[2] * p3 + a[3] * p4) / new Q(6, 1);
            endpoints.Add(p);
        }
        Assert.That(endpoints.Min(), Is.EqualTo(new Q(3, 8)));
        Assert.That(endpoints.Max(), Is.EqualTo(Q.One));
    }

    [TestCase(0d, "extinction")]
    [TestCase(-.5, "sign")]
    [TestCase(1.00000001, "expanded")]
    public void AngularFailuresAreStrictDespiteAnAbsoluteBudget(double endpoint, string criterion)
    {
        var a = ModelAI3Fixtures.State(spin: new(1, 0, 0));
        var b = ModelAI3Fixtures.State(spin: new(endpoint, 0, 0));
        Assert.That(Assert.Throws<ArithmeticException>(() => CheckAngularPath(a, b, false))!.Message, Does.Contain(criterion));
    }

    [Test]
    public void AxisUsesExactCrossProductsAndDominantXTieWithoutNormalizing()
    {
        var a = ModelAI3Fixtures.State(spin: new(2, -2, 1));
        var b = ModelAI3Fixtures.State(spin: new(1, -1, .5));
        var t = ModelAI3Fixtures.FloorBudgets();
        Assert.DoesNotThrow(() => CheckAxis(a, b, t));
        Assert.Throws<ArithmeticException>(() => CheckAxis(a, ModelAI3Fixtures.WithComponent(b, 8, .500001), t));
        Assert.Throws<ArithmeticException>(() => CheckAxis(default, b, t));
        Assert.DoesNotThrow(() => CheckAxis(default, default, t));
        Assert.DoesNotThrow(() => CheckAngularPath(ModelAI3Fixtures.State(spin: new(-0d, 0, 0)), default, true));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DivisionFreeAxisBoundaryAcceptsTheExactTieAndRejectsItsSuccessor(bool above)
    {
        var anchor = ModelAI3Fixtures.State(spin: new(1, 1, 0));
        var y = .5 + Math.ScaleB(1, -10);
        var endpoint = ModelAI3Fixtures.State(spin: new(.5, above ? Math.BitIncrement(y) : y, 0));
        var t = Enumerable.Repeat(D.Integer(9_765_625), 9).ToArray();
        if (above) Assert.Throws<ArithmeticException>(() => CheckAxis(anchor, endpoint, t));
        else Assert.DoesNotThrow(() => CheckAxis(anchor, endpoint, t));
    }

    [Test]
    public void SubnormalAngularExtinctionIsNotHiddenByTheAbsoluteFloor()
    {
        var anchor = ModelAI3Fixtures.State(spin: new(double.Epsilon, 0, 0));
        Assert.That(Assert.Throws<ArithmeticException>(() => CheckAngularPath(anchor, default, false))!.Message, Does.Contain("extinction"));
    }

    [TestCase(double.Epsilon, double.MaxValue)]
    [TestCase(double.MaxValue, double.Epsilon)]
    public void ExtremeValidPhysicalInputsRemainWithinTheFixedArithmeticEnvelope(double low, double high)
    {
        var parameters = ModelAParameters.NathanAsrB(new(low, high, high, high), ModelARotationSettings.R1U(low, high));
        var rates = new Rates(parameters);
        Assert.That(Math.Abs(rates.Gamma.Upper.E), Is.LessThan(12_000));
        Assert.That(Math.Abs(rates.Magnus.Upper.E), Is.LessThan(12_000));
        Assert.That(Math.Abs(rates.Drag.Upper.E), Is.LessThan(12_000));
        var state = ModelAI3Fixtures.State(velocity: new(double.MaxValue, double.Epsilon, 0), spin: new(high, low, 0));
        // Huge coefficients reject numerically; the reciprocal-scale case remains tiny and resolved.
        if (low == double.Epsilon) Assert.Throws<ArithmeticException>(() => rates.Check(double.MaxValue, state));
        else Assert.DoesNotThrow(() => rates.Check(double.MaxValue, state));
    }

    [Test]
    public void CompletedSpeedAndEnergyAreSeparateNecessaryCompatibilityDiagnostics()
    {
        var a = ModelAI3Fixtures.State(velocity: new(1, 0, 0));
        var t = ModelAI3Fixtures.FloorBudgets();
        var q = D.Integer(30_000); var root = SquareRoot(q);
        Assert.DoesNotThrow(() => CheckPhysicalPath(a, ModelAI3Fixtures.State(velocity: new(0, 1, 0)), 1, 0, t, root, q));
        var speed = Assert.Throws<ArithmeticException>(() => CheckPhysicalPath(a,
            ModelAI3Fixtures.State(velocity: new(2, 0, 0)), 1, 0, t, root, q));
        Assert.That(speed!.Message, Does.Contain("speed"));
        var height = ModelAI3Fixtures.State(position: new(0, 0, 1), velocity: new(1, 0, 0));
        Assert.That(Assert.Throws<ArithmeticException>(() => CheckPhysicalPath(a, height, .01, 1, t, root, q))!.Message, Does.Contain("energy"));
        // Lowering the height is allowed; neither ground contact nor sign restrictions are inferred.
        Assert.DoesNotThrow(() => CheckPhysicalPath(a, ModelAI3Fixtures.WithComponent(height, 2, -1e100), 1, 1, t, root, q));
    }
}
