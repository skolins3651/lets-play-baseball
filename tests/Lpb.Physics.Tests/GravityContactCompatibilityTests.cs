using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class GravityContactCompatibilityTests
{
    private static FlightRequest Request(double z, double v, double g, double duration, double dt,
        double vx = 0, double vy = 0, double x = 0, double y = 0) =>
        new(FlightFixtures.State(z, v, x, y, vx, vy), g, duration, dt);

    private static IEnumerable<TestCaseData> Controls()
    {
        var zero = FlightFixtures.ZeroPlane;
        var controls = new List<(string Name, FlightRequest Request, GroundContactSettings Contact)> {
            ("constant-before", Request(2,-2,0,1,1), new(0,1)),
            ("constant-horizon", Request(2,-1,0,1,2), new(0,1)),
            ("drop-horizon", Request(20,0,10,2,2), zero),
            ("return-horizon", Request(0,10,10,2,2), zero),
            ("return-within-step", Request(0,10,10,3,3), zero),
            ("nonexact-projection", Request(1,0,10,2,1,6,8,3,-2), zero),
            ("high-horizontal", Request(1,0,10,2,1,1e6), zero),
            ("large-zero-gravity-velocity", Request(1,-1e9,0,2e-9,2e-9,1e9,-1e9), zero),
            ("constant-multistep", Request(2,-1,0,3,.25), zero),
            ("last-budget-step", Request(99999.5,-1,0,1e6,1), zero),
            ("tiny-exact-return", Request(1,1e-300,2e-300,1,1), new(0,1))
        };
        foreach (var (release, v, vx, vy, g, dt) in new (double, double, double, double, double, double)[] {
            (20,0,0,0,10,.25),(20,0,10,0,10,.3),(0,10,10,0,10,.3),
            (0,10,6,8,10,3),(20,-3,-6,8,9.80665,.7),(1,0,2,-3,10,1) })
            controls.Add(($"analytical-{release}-{v}-{dt}", Request(2.125 + release, v, g, 10, dt, vx, vy, 3, -4), new(2,.125)));
        foreach (var v in new[] { -40d, 0, 30 })
            controls.Add(($"baseball-{v}", Request(20,v,9.80665,10,.3,40,-35), zero));
        foreach (var dt in new[] { 3d, .3, .001 })
            controls.Add(($"apex-return-{dt}", Request(0,10,10,3,dt,6,8), zero));
        foreach (var exponent in new[] { 2,5,10,16,20,26,30,40,50,51,52 })
        {
            var a = Math.ScaleB(1d, -exponent);
            controls.Add(($"cancellation-horizon-{exponent}", Request(1-a,-a*a,2*(1+a),1-a,1), zero));
        }
        foreach (var item in controls)
        foreach (var mode in new[] { "gravity", "zero-density", "zero-coefficient" })
            yield return new TestCaseData(item.Request, item.Contact, mode)
                .SetName($"CertifiedContact_{item.Name}_{mode}");
    }

    [TestCaseSource(nameof(Controls))]
    public void AcceptedContactPreservesStateProvenanceAndIndependentFirstContactProof(
        FlightRequest request, GroundContactSettings contact, string mode)
    {
        var a = Aerodynamics(contact, mode);
        var result = a is null ? GravityOnlySimulator.SimulateUntilGroundContact(request, contact)
            : GravityDragSimulator.SimulateUntilGroundContact(request, a, contact.GroundElevation);
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.GroundContact));
        Assert.That(result.FinalTime, Is.GreaterThan(0).And.LessThanOrEqualTo(request.Duration));
        Assert.That(result.FinalState.Position.Z, Is.EqualTo(contact.ContactHeight));
        Assert.That(result.Samples[0].State, Is.EqualTo(request.InitialState));
        Assert.That(result.ModelKind, Is.EqualTo(a is null ? FlightModelKind.GravityOnly : FlightModelKind.GravityAndDrag));

        AssertPhysicalTiming(result.Samples[0], result.FinalTime, contact.ContactHeight, request.GravityMagnitude);
        AssertPhysicalTiming(result.Samples[^2], result.FinalTime, contact.ContactHeight, request.GravityMagnitude);
        Assert.That(TrajectoryMeasurements.GetGroundContact(result), Is.EqualTo(result.Samples[^1]));
        if (a is null)
            Assert.That(GravityOnlyTrajectory.Evaluate(result, request, result.FinalTime), Is.EqualTo(result.FinalState));
        else
        {
            var equivalent = new AerodynamicSettings(a.BallMass, a.BallRadius, a.AirDensity, a.DragCoefficient);
            Assert.That(GravityDragTrajectory.Evaluate(result, request, equivalent, result.FinalTime), Is.EqualTo(result.FinalState));
            Assert.Throws<ArgumentException>(() => GravityOnlyTrajectory.Evaluate(result, request, result.FinalTime));
            Assert.Throws<ArgumentException>(() => GravityDragTrajectory.Evaluate(result, request,
                new(.146, a.BallRadius, a.AirDensity, a.DragCoefficient), result.FinalTime));
        }
    }

    [TestCase("gravity")] [TestCase("zero-density")] [TestCase("zero-coefficient")]
    public void RepresentablyAirborneHorizonKeepsValidDurationCompletion(string mode)
    {
        var contact = new GroundContactSettings(0, 1);
        var u = Math.BitIncrement(1d) - 1;
        var request = Request(1 + u, -.25 * u, 0, 1, 1);
        var a = Aerodynamics(contact, mode);
        var result = a is null ? GravityOnlySimulator.SimulateUntilGroundContact(request, contact)
            : GravityDragSimulator.SimulateUntilGroundContact(request, a, 0);
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
        Assert.That(result.FinalTime, Is.EqualTo(1));
        Assert.That(result.FinalState.Position.Z, Is.GreaterThan(1));
        Assert.That(TrajectoryMeasurements.GetGroundContact(result), Is.Null);
        Assert.That(result.ModelKind, Is.EqualTo(a is null ? FlightModelKind.GravityOnly : FlightModelKind.GravityAndDrag));
    }

    [TestCase("gravity", 0)] [TestCase("gravity", -1)]
    [TestCase("zero-density", 0)] [TestCase("zero-density", -1)]
    [TestCase("zero-coefficient", 0)] [TestCase("zero-coefficient", -1)]
    public void ImmediateContactStillBypassesUnrepresentableIntegration(string mode, double velocity)
    {
        var contact = FlightFixtures.ZeroPlane;
        var request = Request(0, velocity, double.MaxValue, double.Epsilon, double.Epsilon);
        var a = Aerodynamics(contact, mode);
        var result = a is null ? GravityOnlySimulator.SimulateUntilGroundContact(request, contact)
            : GravityDragSimulator.SimulateUntilGroundContact(request, a, contact.GroundElevation);
        Assert.That(result.Samples, Has.Count.EqualTo(1));
        Assert.That(result.FinalTime, Is.Zero);
        Assert.That(result.FinalState, Is.EqualTo(request.InitialState));
        Assert.That(result.ModelKind, Is.EqualTo(a is null ? FlightModelKind.GravityOnly : FlightModelKind.GravityAndDrag));
    }

    [TestCase("gravity")] [TestCase("zero-density")] [TestCase("zero-coefficient")]
    public void FiniteUnresolvableReleaseRangeFailsWithStepAndOriginContext(string mode)
    {
        var request = Request(Math.ScaleB(1,1023), -Math.ScaleB(1,1021), 0, 8, 1);
        var contact = new GroundContactSettings(-3 * Math.ScaleB(1,1022), Math.ScaleB(1,1022));
        var a = Aerodynamics(contact, mode);
        SimulationResult? result = null;
        var error = Assert.Throws<ArithmeticException>(() => result = a is null
            ? GravityOnlySimulator.SimulateUntilGroundContact(request, contact)
            : GravityDragSimulator.SimulateUntilGroundContact(request, a, contact.GroundElevation));
        Assert.That(result, Is.Null);
        Assert.That(error!.Message, Does.Contain("Integration step 8").And.Contain("release-origin").And.Contain("TwoSum sum"));
        TestContext.Out.WriteLine(error.ToString());
    }

    [TestCase("gravity")] [TestCase("zero-density")] [TestCase("zero-coefficient")]
    public void BothDisabledPathsUseGravityPolicyV2(string mode)
    {
        var contact = FlightFixtures.ZeroPlane;
        var request = Request(1, -1, 0, 1, 1);
        var source = new FlightProvenance(request, contact, Aerodynamics(contact, mode));
        Assert.That(source.NumericalPolicy, Is.EqualTo("classical-rk4/indexed-v1;gravity-events-v2"));
        Assert.That(source.ModelKind, Is.EqualTo(mode == "gravity" ? FlightModelKind.GravityOnly : FlightModelKind.GravityAndDrag));
        var enabled = new FlightProvenance(request, contact, new(.145, contact.BallRadius, 1.225, .35));
        Assert.That(enabled.NumericalPolicy, Is.EqualTo("classical-rk4/indexed-v1;drag-full-state-v1;drag-events-v1"));
    }

    private static AerodynamicSettings? Aerodynamics(GroundContactSettings contact, string mode) => mode switch
    {
        "gravity" => null,
        "zero-density" => new(.145, contact.BallRadius, 0, .35),
        "zero-coefficient" => new(.145, contact.BallRadius, 1.225, 0),
        _ => throw new AssertionException(mode)
    };

    private static void AssertPhysicalTiming(TrajectorySample origin, double time, double height, double gravity)
    {
        var elapsed = ExactBinary.From(time) - ExactBinary.From(origin.Time);
        var z = ExactBinary.From(origin.State.Position.Z) - ExactBinary.From(height);
        var v = ExactBinary.From(origin.State.Velocity.Z);
        var g = ExactBinary.From(gravity);
        var q = z + v * elapsed - (g * elapsed * elapsed).Half();
        Assert.That(q.Sign, Is.LessThanOrEqualTo(0), "The contact cannot precede the exact physical crossing.");
        Assert.That((v - g * elapsed).Sign, Is.LessThan(0));
        var earlier = ExactBinary.From(time) - ExactBinary.From(GravityOnlyRefinement.TimeTolerance);
        var originTime = ExactBinary.From(origin.Time);
        if (earlier.CompareTo(originTime) < 0)
            earlier = originTime;
        var earlierElapsed = earlier - originTime;
        var earlierQ = z + v * earlierElapsed - (g * earlierElapsed * earlierElapsed).Half();
        Assert.That(earlierQ.Sign, Is.GreaterThanOrEqualTo(0), "The unique first contact must lie inside the exact allowed window.");
    }
}
