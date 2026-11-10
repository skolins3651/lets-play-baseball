namespace Lpb.Physics;

// Only the stationary-air, nonnegative quadratic-drag model has these event policies.
// All trial evaluations use the ORIGINAL ordinary anchor, never a narrowed endpoint.
internal static class GravityDragRefinement
{
    internal const int MaximumEvaluations = 128;
    internal const double TimeTolerance = 1e-10;
    internal const double HeightTolerance = 1e-8;
    internal const double VelocityTolerance = 1e-8;

    internal static TrajectorySample FindContact(TrajectorySample anchor, TrajectorySample upper,
        FlightState upperDiagnostic, double height, DragNumericalEvaluation numerical)
    {
        ValidateInterval(anchor, anchor, upper, numerical);
        DragNumericalEvaluation.CheckContactHeight(height);
        numerical.CheckState(upperDiagnostic);
        CheckContactGeometry(upper.Time, upper.State, upperDiagnostic, height);
        if (upper.State.Position.Z > height || upper.State.Velocity.Z >= 0 || upperDiagnostic.Velocity.Z >= 0)
            throw new ArithmeticException("Drag contact requires a descending on/below-plane upper endpoint on both numerical paths.");
        numerical.CheckContactReachability(anchor, upper.Time, height);
        var lower = anchor;
        if (lower.State.Position.Z == height && lower.State.Velocity.Z > 0)
            lower = FindApex(anchor, lower, upper, numerical, height);
        if (lower.State.Position.Z <= height)
            throw new ArithmeticException("Drag contact requires a resolvable airborne lower endpoint; the launch root is excluded.");

        for (var evaluations = 0; ; evaluations++)
        {
            // Primary equality alone is not a paired exact root. If the diagnostic
            // is below the plane, retain the bracket and require normal localization.
            if (upper.State.Position.Z == height && upperDiagnostic.Position.Z == height)
                return upper;
            if (DragNumericalEvaluation.UpperDifference(upper.Time, lower.Time) <= TimeTolerance &&
                DragNumericalEvaluation.UpperDifference(upper.State.Position.Z, height) <= HeightTolerance &&
                DragNumericalEvaluation.UpperDifference(upperDiagnostic.Position.Z, height) <= HeightTolerance)
                return new(upper.Time, new(new(upper.State.Position.X, upper.State.Position.Y, height), upper.State.Velocity));
            RequireBudget(evaluations, "contact");
            var time = NumericalEventInterval.InteriorTime(lower.Time, upper.Time);
            var trial = numerical.EvaluateStep(anchor, time);
            CheckContactGeometry(time, trial.Full, trial.Diagnostic, height);
            var sample = new TrajectorySample(time, trial.Full);
            if (sample.State.Position.Z > height)
                lower = sample;
            else if (sample.State.Velocity.Z < 0 && trial.Diagnostic.Velocity.Z < 0)
            {
                numerical.CheckContactReachability(anchor, time, height);
                upper = sample;
                upperDiagnostic = trial.Diagnostic;
            }
            else
                throw new ArithmeticException("Drag contact lost its descending-event bracket.");
        }
    }

    internal static TrajectorySample FindApex(TrajectorySample anchor, TrajectorySample lower,
        TrajectorySample upper, DragNumericalEvaluation numerical, double? contactHeight = null)
    {
        ValidateInterval(anchor, lower, upper, numerical);
        if (lower.State.Velocity.Z <= 0 || upper.State.Velocity.Z > 0)
            throw new ArithmeticException("Drag apex requires positive/nonpositive vertical-velocity endpoints.");
        for (var evaluations = 0; ; evaluations++)
        {
            if (upper.State.Velocity.Z == 0)
                return upper;
            // Endpoint agreement is insufficient. The final interior witness consumes one of
            // the same 128 trials and participates in numerical maximum/tie selection.
            var narrow = DragNumericalEvaluation.UpperDifference(upper.Time, lower.Time) <= TimeTolerance &&
                Math.Abs(lower.State.Velocity.Z) <= VelocityTolerance && Math.Abs(upper.State.Velocity.Z) <= VelocityTolerance;
            RequireBudget(evaluations, "apex (including interior witness)");
            var time = NumericalEventInterval.InteriorTime(lower.Time, upper.Time);
            var evaluated = numerical.EvaluateStep(anchor, time);
            if (contactHeight is not null)
                CheckContactGeometry(time, evaluated.Full, evaluated.Diagnostic, contactHeight.Value);
            var trial = new TrajectorySample(time, evaluated.Full);
            if (narrow && Math.Abs(trial.State.Velocity.Z) <= VelocityTolerance)
            {
                var minimum = Math.Min(lower.State.Position.Z, Math.Min(upper.State.Position.Z, trial.State.Position.Z));
                var maximum = Math.Max(lower.State.Position.Z, Math.Max(upper.State.Position.Z, trial.State.Position.Z));
                if (DragNumericalEvaluation.UpperDifference(maximum, minimum) <= HeightTolerance)
                    return Prefer(Prefer(lower, upper), trial);
            }
            if (trial.State.Velocity.Z > 0)
                lower = trial;
            else
                upper = trial;
        }
    }

    internal static void CheckContactGeometry(double time, FlightState full, FlightState diagnostic, double height)
    {
        if (!double.IsFinite(full.Position.Z) || !double.IsFinite(diagnostic.Position.Z) || !double.IsFinite(height))
            throw new ArithmeticException("Drag contact geometry requires finite endpoint heights and plane.");
        if ((full.Position.Z > height) != (diagnostic.Position.Z > height))
            throw new ArithmeticException($"Drag contact geometry disagreement at {time:R} s: full Z {full.Position.Z:R}, " +
                $"diagnostic Z {diagnostic.Position.Z:R}, contact height {height:R}; reduce timestep or resolve coordinate precision.");
    }

    private static TrajectorySample Prefer(TrajectorySample first, TrajectorySample second) =>
        second.State.Position.Z > first.State.Position.Z ||
        (second.State.Position.Z == first.State.Position.Z && second.Time < first.Time) ? second : first;

    private static void RequireBudget(int evaluations, string operation)
    {
        if (evaluations == MaximumEvaluations)
            throw new ArithmeticException($"Drag {operation} exceeded {MaximumEvaluations} checked trial evaluations.");
    }

    private static void ValidateInterval(TrajectorySample anchor, TrajectorySample lower,
        TrajectorySample upper, DragNumericalEvaluation numerical)
    {
        if (!double.IsFinite(anchor.Time) || anchor.Time < 0 || !double.IsFinite(lower.Time) || lower.Time < anchor.Time ||
            !double.IsFinite(upper.Time) || upper.Time <= lower.Time)
            throw new ArithmeticException("Drag refinement requires a finite progressing interval and original numerical anchor.");
        numerical.CheckState(anchor.State);
        numerical.CheckState(lower.State);
        numerical.CheckState(upper.State);
        numerical.CheckTransition(anchor.State, lower.State);
        numerical.CheckTransition(lower.State, upper.State);
    }
}
