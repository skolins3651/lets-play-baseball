using System.Collections;
using System.Reflection;
using NUnit.Framework;
using static Lpb.Physics.Tests.ModelAI3Fixtures;
using D = Lpb.Physics.ModelANumericalArithmetic.Dyadic;
using Q = Lpb.Physics.Tests.SpinReference.Rational;

namespace Lpb.Physics.Tests;

public class ModelASimulatorTests
{
    [Test]
    public void OnlyApprovedStaticSimulatorMethodIsPublic()
    {
        var type = typeof(ModelASimulator);
        Assert.That(type.IsAbstract && type.IsSealed, Is.True);
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Single();
        Assert.That(method.Name, Is.EqualTo("Simulate"));
        Assert.That(method.ReturnType, Is.EqualTo(typeof(ModelASimulationResult)));
        Assert.That(method.GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[] { typeof(ModelAFlightRequest), typeof(ModelAParameters) }));
    }

    [Test]
    public void NullInputsRetainArgumentExceptions()
    {
        Assert.Throws<ArgumentNullException>(() => ModelASimulator.Simulate(null!, Baseball()));
        Assert.Throws<ArgumentNullException>(() => ModelASimulator.Simulate(Flight(), null!));
    }

    [TestCase(.3, .1)]
    [TestCase(.35, .1)]
    [TestCase(.1, 1)]
    [TestCase(1, .03)]
    public void IndexedClockRetainsExactReleaseEveryFullEndpointAndRequestedTerminalTime(double duration, double h)
    {
        var request = new ModelAFlightRequest(State(new(1, 2, -3), new(1, -.5, 2), new(4, 5, 6)), 1, duration, h);
        var parameters = Baseball(density: 0);
        var result = ModelASimulator.Simulate(request, parameters);
        Assert.That(result.Samples[0], Is.EqualTo(new SpinTrajectorySample(0, request.InitialState)));
        Assert.That(result.FinalTime, Is.EqualTo(duration));
        Assert.That(result.CompletionReason, Is.EqualTo(CompletionReason.DurationReached));
        Assert.That(result.ModelIdentity, Is.EqualTo("model-a/nathan-asr-b-v1"));
        var direct = request.InitialState;
        var dynamics = new ModelADynamics(request.GravityMagnitude, parameters);
        for (var i = 1; i < result.Samples.Count; i++)
        {
            var expectedTime = Math.Min(i * h, duration);
            Assert.That(result.Samples[i].Time, Is.EqualTo(expectedTime));
            direct = SpinRk4Integrator.Step(result.Samples[i - 1].Time, expectedTime, direct, dynamics.Evaluate);
            Assert.That(result.Samples[i].State, Is.EqualTo(direct), "Only original full-step endpoints are retained.");
        }
    }

    [Test]
    public void DiagnosticsExecuteExactlyTwelvePhysicalEvaluationsAndDoNotReplaceFullEndpoint()
    {
        var request = Flight(duration: .02, h: .02);
        var p = Baseball(); var d = new ModelADynamics(request.GravityMagnitude, p);
        var full = SpinRk4Integrator.Step(0, .02, request.InitialState, d.Evaluate);
        var middle = SpinRk4Integrator.Step(0, .01, request.InitialState, d.Evaluate);
        var diagnostic = SpinRk4Integrator.Step(.01, .02, middle, d.Evaluate);
        Assert.That(full, Is.Not.EqualTo(diagnostic), "Fixture must distinguish recorded and diagnostic results.");
        var numerical = new ModelANumericalEvaluation(request, p);
        Assert.That(numerical.Step(1, new(0, request.InitialState), .02), Is.EqualTo(full));
        Assert.That(numerical.PhysicalEvaluationCount, Is.EqualTo(12));
        var result = ModelASimulator.Simulate(request, p);
        Assert.That(result.Samples.Count, Is.EqualTo(2));
        Assert.That(result.FinalState, Is.EqualTo(full));
    }

    [Test]
    public void PassiveInstrumentationObservesActualCoefficientsBeforeTheSameFourStages()
    {
        var calls = new List<string>();
        var parameters = Baseball(); var request = Flight();
        var d = new ModelADynamics(request.GravityMagnitude, parameters);
        const double start = .2, end = .23;
        var actual = SpinRk4Integrator.Step(start, end, request.InitialState, (time, state) =>
        {
            calls.Add(time.ToString("R")); return d.Evaluate(time, state);
        }, (h, c2, c6) =>
        {
            calls.Add("coefficients");
            Assert.That(h, Is.EqualTo(end - start));
            Assert.That(c2, Is.EqualTo(h / 2)); Assert.That(c6, Is.EqualTo(h / 6));
        });
        Assert.That(calls.Count, Is.EqualTo(5)); Assert.That(calls[0], Is.EqualTo("coefficients"));
        Assert.That(actual, Is.EqualTo(SpinRk4Integrator.Step(start, end, request.InitialState, d.Evaluate)));
    }

    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public void BothDiagnosticIntervalsAreValidatedBeforeAnyPhysicalEvaluation(int subnormalUnits)
    {
        var duration = subnormalUnits * double.Epsilon;
        var request = new ModelAFlightRequest(default, 0, duration, duration);
        var numerical = new ModelANumericalEvaluation(request, Baseball(density: 0));
        var error = Assert.Throws<ArithmeticException>(() => numerical.Step(1, new(0, default), duration));
        Assert.That(numerical.PhysicalEvaluationCount, Is.Zero);
        Assert.That(error!.Message, Does.Contain("ordinary step 1").And.Contain("half"));
    }

    [TestCase(8)]
    [TestCase(9)]
    public void RepresentableSubnormalSubdivisionsAreNotRejectedByAnInventedCutoff(int units)
    {
        var duration = units * double.Epsilon;
        var result = ModelASimulator.Simulate(new(default, 0, duration, duration), Baseball(density: 0));
        Assert.That(result.FinalTime, Is.EqualTo(duration));
        Assert.That(result.FinalState, Is.EqualTo(default(SpinFlightState)));
    }

    [TestCase(0d, 1d)]
    [TestCase(1d, 2d)]
    [TestCase(1e100, 1.0000000000000002e100)]
    public void IntervalPreflightSeparatesSafeAndUnsafeMidpoints(double start, double end)
    {
        if (start == 1e100)
            Assert.Throws<ArithmeticException>(() => ModelANumericalEvaluation.ValidateInterval(start, end, "probe"));
        else
            Assert.That(ModelANumericalEvaluation.ValidateInterval(start, end, "probe"), Is.EqualTo(start + (end - start) / 2));
    }

    [Test]
    public void WholeHorizonLimitRejectsBeforeSampleAllocationOrPhysicalWork()
    {
        var request = new ModelAFlightRequest(default, 0, 100_001, 1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<InvalidOperationException>(() => ModelASimulator.Simulate(request, Baseball(density: 0)));
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.LessThan(1_000_000), "No trajectory-sized allocation on rejected horizons.");
        var grid = new SimulationTimeGrid(new FlightRequest(default, 0, 100_000, 1));
        Assert.That(grid.StepCount, Is.EqualTo(100_000));
        Assert.That(grid.GetNextTime(100_000, 99_999), Is.EqualTo(100_000));
        // Existing I1 result tests independently exercise the 100001-sample ownership boundary.
    }

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    [TestCase(true, 1)]
    [TestCase(false, 2)]
    [TestCase(true, 2)]
    public void RepresentativeFlightsAreAcceptedForEachApprovedResolution(bool damping, int spinCase)
    {
        foreach (var h in new[] { .02, .01, .005 })
        {
            var result = ModelASimulator.Simulate(Flight(h, spinCase: spinCase), Baseball(damping));
            Assert.That(result.Samples.Count, Is.EqualTo((int)(2 / h) + 1));
            Assert.That(result.FinalTime, Is.EqualTo(2));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LargeStationaryStateIsVerifiedWithoutPerturbationRestrictions(bool damping)
    {
        var initial = State(new(double.MaxValue, -double.MaxValue, double.MaxValue), spin: new(1e200, -1e200, 1e200));
        var result = ModelASimulator.Simulate(new(initial, 0, 1, .25), Baseball(damping));
        Assert.That(result.Samples.All(s => s.State == initial), Is.True);
    }

    [Test]
    public void LargeConstantR0SpinAndInactiveZeroComponentsRemainValueExact()
    {
        var initial = State(velocity: new(1, 0, 0), spin: new(double.MaxValue, -double.MaxValue, -0d));
        var result = ModelASimulator.Simulate(new(initial, 0, 1, .125), Baseball(false, density: 0));
        foreach (var sample in result.Samples) Assert.That(sample.State.AngularVelocity, Is.EqualTo(initial.AngularVelocity));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ZeroDensityReductionMatchesIndependentGravityFormula(bool damping)
    {
        var initial = State(new(3, -1, -2), new(2, -3, 4), new(5, -6, 7));
        var result = ModelASimulator.Simulate(new(initial, 9.8, .5, .01), Baseball(damping, density: 0));
        foreach (var s in result.Samples)
        {
            var t = s.Time;
            Assert.That(s.State.Translation.Position.X, Is.EqualTo(3 + 2 * t).Within(1e-13));
            Assert.That(s.State.Translation.Position.Y, Is.EqualTo(-1 - 3 * t).Within(1e-13));
            Assert.That(s.State.Translation.Position.Z, Is.EqualTo(-2 + 4 * t - 4.9 * t * t).Within(1e-13));
            Assert.That(s.State.Translation.Velocity.Z, Is.EqualTo(4 - 9.8 * t).Within(1e-13));
            Assert.That(s.State.AngularVelocity, Is.EqualTo(initial.AngularVelocity));
        }
    }

    [TestCase(0d)]
    [TestCase(.35)]
    public void ZeroSpinReductionMatchesJointlyAcceptedExistingDragRuntime(double drag)
    {
        var request = new ModelAFlightRequest(State(new(0, 0, 2), new(20, 5, 15)), 9.8, .4, .01);
        var p = Baseball(drag: drag);
        var result = ModelASimulator.Simulate(request, p);
        var legacy = GravityDragSimulator.Simulate(request.TranslationRequest, p.Aerodynamics);
        Assert.That(result.Samples.Select(s => s.State.Translation), Is.EqualTo(legacy.Samples.Select(s => s.State)));
        Assert.That(result.Samples.All(s => s.State.AngularVelocity == default), Is.True);
    }

    [Test]
    public void ZeroBetaR1URetainsItsSelectionAndMatchesR0Dynamics()
    {
        var request = Flight(duration: .2);
        var r0 = ModelASimulator.Simulate(request, Baseball(false));
        var zeroBeta = ModelASimulator.Simulate(request, Baseball(beta: 0));
        Assert.That(zeroBeta.Samples, Is.EqualTo(r0.Samples));
        Assert.That(Source(zeroBeta).Parameters.Rotation.Kind, Is.EqualTo(ModelARotationKind.R1U));
    }

    [Test]
    public void CheckedProducerAttestsOnlySuccessfulDurationAndPreservesImmutableOriginalInputs()
    {
        var request = Flight(duration: .03); var p = Baseball();
        var result = ModelASimulator.Simulate(request, p);
        var source = Source(result);
        Assert.That(source.Request, Is.SameAs(request)); Assert.That(source.Parameters, Is.SameAs(p));
        Assert.That(source.NumericalPolicyIdentity, Is.EqualTo("classical-rk4/indexed-v1;model-a-full-state-v1"));
        Assert.That(new ModelAFlightProvenance(request, p).NumericalPolicyIdentity, Is.Null);
        Assert.That(((ICollection)result.Samples).SyncRoot, Is.SameAs(result.Samples));
        Assert.Throws<NotSupportedException>(() => ((IList<SpinTrajectorySample>)result.Samples)[0] = default);
        source.ValidateRequest(new(request.InitialState, request.GravityMagnitude, request.Duration, request.TimeStep));
        source.ValidateParameters(Baseball());
        Assert.Throws<ArgumentException>(() => source.ValidateRequest(Flight(duration: .04)));
        Assert.Throws<ArgumentException>(() => source.ValidateParameters(Baseball(beta: 0)));
    }

    [TestCase(30, -34)]
    [TestCase(40, -20)]
    public void AgreeingLostMotionCannotBypassRepresentationOrRecordedOperandLoss(int originExponent, int speedExponent)
    {
        var initial = State(new(Math.ScaleB(1, originExponent), 0, 0), new(Math.ScaleB(1, speedExponent), 0, 0));
        var request = new ModelAFlightRequest(initial, 0, 1, 1);
        var p = Baseball(density: 0); var d = new ModelADynamics(0, p);
        var full = SpinRk4Integrator.Step(0, 1, initial, d.Evaluate);
        var mid = SpinRk4Integrator.Step(0, .5, initial, d.Evaluate);
        var half = SpinRk4Integrator.Step(.5, 1, mid, d.Evaluate);
        Assert.That(full, Is.EqualTo(initial)); Assert.That(half, Is.EqualTo(initial));
        var loss = ModelANumericalArithmetic.FinalLoss6(D.From(initial.Translation.Position.X), D.From(full.Translation.Position.X), D.One,
            D.Integer(6) * D.From(initial.Translation.Velocity.X));
        Assert.That(ModelANumericalArithmeticTests.Exact(loss) / new Q(6, 1), Is.EqualTo(Q.Pow2(speedExponent)));
        var error = Assert.Throws<ArithmeticException>(() => ModelASimulator.Simulate(request, p));
        Assert.That(error!.Message, Does.Contain("ordinary step 1").And.Contain("position.X").And.Contain("spacing"));
    }

    [Test]
    public void B0DampingAmplificationIsRejectedBeforePhysicalEvaluation()
    {
        const double h = 10.9824254663;
        static double R(double z) => 1 - z + z * z / 2 - z * z * z / 6 + z * z * z * z / 24;
        Assert.That(R(h), Is.EqualTo(435.705).Within(.002));
        Assert.That(R(h / 2) * R(h / 2), Is.EqualTo(R(h)).Within(1e-7));
        Assert.That(Math.Exp(-h), Is.LessThan(.000018));
        var request = new ModelAFlightRequest(State(velocity: new(1, 0, 0), spin: new(1e-12, 0, 0)), 0, h, h);
        var p = ModelAParameters.NathanAsrB(new(1e30, 1, 1 / Math.PI, 0), ModelARotationSettings.R1U(1, 1));
        var numerical = new ModelANumericalEvaluation(request, p);
        var error = Assert.Throws<ArithmeticException>(() => numerical.Step(1, new(0, request.InitialState), h));
        Assert.That(error!.Message, Does.Contain("ordinary step 1").And.Contain("full k1").And.Contain("damping"));
        Assert.That(numerical.PhysicalEvaluationCount, Is.Zero);
    }

    [Test]
    public void CoarseMagnusAmplificationRejectsDespiteSubfloorFullHalfDiscrepancies()
    {
        var request = new ModelAFlightRequest(State(velocity: new(1e-12, 0, 0), spin: new(0, 1e-12, 0)), 0, 10, 10);
        var p = ModelAParameters.NathanAsrB(new(1, 1, 2e12 / Math.PI, 0), ModelARotationSettings.R0());
        var d = new ModelADynamics(0, p);
        var f = SpinRk4Integrator.Step(0, 10, request.InitialState, d.Evaluate);
        var m = SpinRk4Integrator.Step(0, 5, request.InitialState, d.Evaluate);
        var half = SpinRk4Integrator.Step(5, 10, m, d.Evaluate);
        for (var i = 0; i < 9; i++)
            Assert.That(Math.Abs(ModelANumericalPolicy.Component(f, i) - ModelANumericalPolicy.Component(half, i)), Is.LessThan(1e-8));
        var v = f.Translation.Velocity;
        Assert.That(Math.Sqrt(v.X * v.X + v.Z * v.Z) / 1e-12, Is.GreaterThan(200));
        Assert.That(Assert.Throws<ArithmeticException>(() => ModelASimulator.Simulate(request, p))!.Message, Does.Contain("full k1").And.Contain("sensitivity"));
    }

    [Test]
    public void FailedExecutionDoesNotReturnAPartialSuccess()
    {
        ModelASimulationResult? result = null;
        var initial = State(new(Math.ScaleB(1, 40), 0, 0), new(1e-6, 0, 0));
        Assert.Throws<ArithmeticException>(() => result = ModelASimulator.Simulate(new(initial, 0, 1, 1), Baseball(density: 0)));
        Assert.That(result, Is.Null);
    }

    [Test]
    public void CoarseOrdinaryFlightRejectsFullHalfDisagreementWithComponentContext()
    {
        var request = Flight(h: .2, duration: .2);
        var error = Assert.Throws<ArithmeticException>(() => ModelASimulator.Simulate(request, Baseball()));
        Assert.That(error!.Message, Does.Contain("ordinary step 1").And.Contain("Full/half agreement").And.Contain("position."));
    }

    [Test]
    public void B1NonzeroComponentResolutionFailureRetainsItsCauseAndCannotBecomeZeroLift()
    {
        var initial = State(velocity: new(1, 0, 0), spin: new(0, double.Epsilon, 0));
        var error = Assert.Throws<ArithmeticException>(() => ModelASimulator.Simulate(new(initial, 0, .01, .01), Baseball(false, drag: 0)));
        Assert.That(error!.Message, Does.Contain("ordinary step 1").And.Contain("full k1").And.Contain("Magnus").And.Contain("nonzero component rounds to zero"));
        Assert.That(error.InnerException, Is.Not.Null);
    }

    [Test]
    public void ActualRuntimeWeightedOverflowRetainsBranchPhaseAndComponentContext()
    {
        var error = Assert.Throws<ArithmeticException>(() => ModelASimulator.Simulate(new(default, double.MaxValue, .001, .001), Baseball(density: 0)));
        Assert.That(error!.Message, Does.Contain("ordinary step 1").And.Contain("full RK4 call").And.Contain("weighted combination").And.Contain("velocity.Z"));
        Assert.That(double.IsFinite(double.MaxValue * .001), Is.True, "The finite ideal endpoint cannot rescue required I2 intermediate overflow.");
    }

    [Test]
    public void ProductionIntermediateOverflowCannotBeRescuedByAnExactFiniteIdealEndpoint()
    {
        var calls = 0;
        var error = Assert.Throws<ArithmeticException>(() => SpinRk4Integrator.Step(0, 1, default, (_, _) =>
        {
            calls++;
            return calls switch
            {
                1 => new(0, 0, 0, 1e308, 0, 0, 0, 0, 0),
                2 => new(0, 0, 0, 1e308, 0, 0, 0, 0, 0),
                3 => new(0, 0, 0, -1e308, 0, 0, 0, 0, 0),
                _ => new(0, 0, 0, -1e308, 0, 0, 0, 0, 0)
            };
        }));
        Assert.That(calls, Is.EqualTo(4));
        Assert.That(error!.Message, Does.Contain("weighted combination"));
        Assert.That(D.From(1e308) + D.From(1e308).Shifted(1) - D.From(1e308).Shifted(1) - D.From(1e308), Is.EqualTo(D.Zero));
    }

    private static ModelAFlightProvenance Source(ModelASimulationResult result) =>
        (ModelAFlightProvenance)typeof(ModelASimulationResult).GetField("provenance", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(result)!;
}
