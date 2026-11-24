using D = Lpb.Physics.ModelANumericalArithmetic.Dyadic;
using static Lpb.Physics.ModelANumericalArithmetic;
using static Lpb.Physics.ModelANumericalPolicy;

namespace Lpb.Physics;

// One fixed B2-N trial: F, M, H in that order. No retries or diagnostic substitution.
internal sealed class ModelANumericalEvaluation
{
    private readonly ModelAFlightRequest request;
    private readonly ModelADynamics dynamics;
    private readonly Invariants invariants;
    private readonly Rates rates;
    internal long PhysicalEvaluationCount { get; private set; }

    internal ModelANumericalEvaluation(ModelAFlightRequest request, ModelAParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(parameters);
        this.request = request;
        dynamics = new(request.GravityMagnitude, parameters);
        invariants = new(request, parameters);
        rates = new(parameters);
    }

    internal SpinFlightState Step(int ordinaryStep, SpinTrajectorySample anchor, double endTime)
    {
        try
        {
            invariants.CheckState(anchor.State);
            var midpoint = ValidateInterval(anchor.Time, endTime, "full");
            ValidateInterval(anchor.Time, midpoint, "first-half");
            ValidateInterval(midpoint, endTime, "second-half");
            var trial = new Trial(this);
            trial.ObserveState(anchor.State, "ordinary anchor");
            var full = trial.Advance("full", anchor.Time, endTime, anchor.State);
            var middle = trial.Advance("first-half", anchor.Time, midpoint, anchor.State);
            var diagnostic = trial.Advance("second-half", midpoint, endTime, middle);
            var t = Budgets(anchor.State, full, diagnostic, endTime - anchor.Time);

            // Criterion-major order, and position/velocity/angular X/Y/Z within each.
            for (var i = 0; i < 9; i++)
                if (!Agreement(D.From(Component(full, i)) - D.From(Component(diagnostic, i)), t[i]))
                    throw new ArithmeticException($"Full/half agreement {Name(i)} exceeds its exact-decimal budget.");
            for (var i = 0; i < 9; i++)
                if (!invariants[i] && !Representation(trial.MaximumSpacing[i], t[i]))
                    throw new ArithmeticException($"{trial.SpacingContext[i]} {Name(i)} changing-state spacing exceeds B/8.");
            for (var i = 0; i < 9; i++)
                if (!invariants[i] && !UpdateLoss(trial.MaximumLoss6[i], t[i]))
                    throw new ArithmeticException($"{trial.LossContext[i]} {Name(i)} recorded-operand RK4 arithmetic loss exceeds B/8.");

            AngularPath(anchor.State, full, "full endpoint");
            AngularPath(anchor.State, middle, "first-half endpoint");
            AngularPath(middle, diagnostic, "second-half endpoint");
            if (trial.AngularStageFailure is not null) throw new ArithmeticException(trial.AngularStageFailure);
            Axis(anchor.State, full, t, "full endpoint");
            Axis(anchor.State, middle, t, "first-half endpoint");
            Axis(middle, diagnostic, t, "second-half endpoint");
            Axis(request.InitialState, full, t, "full endpoint against release axis");

            var q = t[3] * t[3] + t[4] * t[4] + t[5] * t[5];
            var root = SquareRoot(q);
            PhysicalPath(anchor.State, full, endTime - anchor.Time, t, root, q, "anchor -> full");
            PhysicalPath(anchor.State, middle, midpoint - anchor.Time, t, root, q, "anchor -> first-half");
            PhysicalPath(middle, diagnostic, endTime - midpoint, t, root, q, "first-half -> second-half");
            return full;
        }
        catch (ArithmeticException exception)
        {
            throw new ArithmeticException($"Model A ordinary step {ordinaryStep} [{anchor.Time:R}, {endTime:R}] s: {exception.Message}", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"Model A ordinary step {ordinaryStep} [{anchor.Time:R}, {endTime:R}] s resource failure: {exception.Message}", exception);
        }
    }

    private void AngularPath(SpinFlightState a, SpinFlightState b, string context)
    {
        try { CheckAngularPath(a, b, invariants.ConstantSpin); }
        catch (ArithmeticException e) { throw new ArithmeticException(context + ": " + e.Message, e); }
    }

    private static void Axis(SpinFlightState a, SpinFlightState b, D[] t, string context)
    {
        try { CheckAxis(a, b, t); }
        catch (ArithmeticException e) { throw new ArithmeticException(context + ": " + e.Message, e); }
    }

    private void PhysicalPath(SpinFlightState a, SpinFlightState b, double h, D[] t,
        ModelANumericalArithmetic.Bounds root, D q, string context)
    {
        try { CheckPhysicalPath(a, b, h, request.GravityMagnitude, t, root, q); }
        catch (ArithmeticException e) { throw new ArithmeticException(context + ": " + e.Message, e); }
    }

    internal static double ValidateInterval(double start, double end, string branch)
    {
        var h = end - start;
        if (!double.IsFinite(start) || start < 0 || !double.IsFinite(end) || end <= start || !double.IsFinite(h) || h <= 0)
            throw new ArithmeticException($"{branch} requires finite progressing times.");
        var half = h / 2;
        var sixth = h / 6;
        if (!double.IsFinite(half) || half <= 0 || !double.IsFinite(sixth) || sixth <= 0)
            throw new ArithmeticException($"{branch} has an unrepresentable half/sixth coefficient.");
        var middle = start + half;
        if (!double.IsFinite(middle) || middle <= start || middle >= end)
            throw new ArithmeticException($"{branch} has no representable interior midpoint.");
        return middle;
    }

    private sealed class Trial(ModelANumericalEvaluation owner)
    {
        private static readonly string[] Stages = ["anchor", "k1", "k2", "k3", "k4"];
        internal D[] MaximumSpacing { get; } = new D[9];
        internal D[] MaximumLoss6 { get; } = new D[9];
        internal string?[] SpacingContext { get; } = new string?[9];
        internal string?[] LossContext { get; } = new string?[9];
        internal string? AngularStageFailure { get; private set; }
        private readonly D[] callAnchor = new D[9];
        private readonly D[] previousRate = new D[9];
        private readonly D[] weightedRate = new D[9];
        private string branch = "";
        private int stage;
        private SpinFlightState anchorState;
        private (D H, D C2, D C6) coefficients;
        private D Interval => coefficients.H;

        internal SpinFlightState Advance(string label, double start, double end, SpinFlightState anchor)
        {
            branch = label; stage = 0; anchorState = anchor;
            for (var i = 0; i < 9; i++)
            {
                callAnchor[i] = D.From(Component(anchor, i));
                previousRate[i] = D.Zero; weightedRate[i] = D.Zero;
            }
            try
            {
                var endpoint = SpinRk4Integrator.Step(start, end, anchor, Evaluate, ObserveCoefficients);
                ObserveState(endpoint, branch + " endpoint");
                for (var i = 0; i < 9; i++)
                    RetainLoss(i, FinalLoss6(callAnchor[i], D.From(Component(endpoint, i)), Interval, weightedRate[i]), branch + " endpoint");
                return endpoint;
            }
            catch (ArithmeticException e) { throw new ArithmeticException(branch + " RK4 call: " + e.Message, e); }
            catch (InvalidOperationException e) { throw new InvalidOperationException(branch + " RK4 call: " + e.Message, e); }
        }

        private void ObserveCoefficients(double h, double half, double sixth)
        {
            // These are the actual values executed by I2, not rounded audit coefficients.
            executedInterval = h;
            coefficients = (D.From(h), D.From(half), D.From(sixth));
            if (coefficients.C2.Sign <= 0 || coefficients.C6.Sign <= 0)
                throw new ArithmeticException("Invalid executed RK4 coefficients.");
        }

        private SpinFlightDerivative Evaluate(double time, SpinFlightState state)
        {
            stage++;
            var context = branch + " " + Stages[stage] + $" at {time:R} s";
            try
            {
                owner.invariants.CheckState(state);
                if (!owner.invariants.Stationary) owner.rates.Check(executedInterval, state);
                owner.PhysicalEvaluationCount++;
                var derivative = owner.dynamics.Evaluate(time, state);
                owner.invariants.CheckDerivative(derivative);
                ObserveState(state, context);
                for (var i = 0; i < 9; i++)
                {
                    if (stage > 1)
                        RetainLoss(i, StageLoss6(stage, callAnchor[i], D.From(Component(state, i)), Interval, previousRate[i]), context);
                    var rate = D.From(Component(derivative, i));
                    weightedRate[i] += stage is 2 or 3 ? rate.Shifted(1) : rate;
                    previousRate[i] = rate;
                }
                if (AngularStageFailure is null)
                    for (var i = 6; i < 9; i++)
                    {
                        var a = Component(anchorState, i); var value = Component(state, i);
                        if (value < Math.Min(a, 0) || value > Math.Max(a, 0))
                        {
                            AngularStageFailure = context + $": {Name(i)} is outside the call anchor's signed angular range.";
                            break;
                        }
                    }
                return derivative;
            }
            catch (ArithmeticException e) { throw new ArithmeticException(context + ": " + e.Message, e); }
            catch (InvalidOperationException e) { throw new InvalidOperationException(context + ": " + e.Message, e); }
        }

        private double executedInterval;

        internal void ObserveState(SpinFlightState state, string context)
        {
            owner.invariants.CheckState(state);
            for (var i = 0; i < 9; i++)
            {
                var spacing = Spacing(Component(state, i));
                if (spacing.CompareTo(MaximumSpacing[i]) > 0)
                {
                    MaximumSpacing[i] = spacing; SpacingContext[i] = context;
                }
            }
        }

        private void RetainLoss(int i, D value, string context)
        {
            if (value.CompareTo(MaximumLoss6[i]) > 0)
            {
                MaximumLoss6[i] = value; LossContext[i] = context;
            }
        }
    }
}
