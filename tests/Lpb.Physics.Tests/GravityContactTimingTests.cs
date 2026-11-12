using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class GravityContactTimingTests
{
    private static TrajectorySample Origin(double time, double z, double velocity) =>
        new(time, FlightFixtures.State(z, velocity));

    private static void ValidateSameOrigin(double z, double velocity, double time, double gravity = 0, double height = 0)
    {
        var origin = Origin(0, z, velocity);
        GravityContactEvidence.Validate(origin, origin.State, time, height, gravity);
    }

    [Test]
    public void ExactMathematicalRootNeedsNoRepresentableEarlierTimestamp()
    {
        var time = Math.ScaleB(1, 20);
        Assert.Throws<ArithmeticException>(() => GravityContactEvidence.CreateWindow(0, time));
        Assert.DoesNotThrow(() => ValidateSameOrigin(time, -1, time));
    }

    [Test]
    public void CutoffCeilingAndIndependentWidthRespectTheExactTolerance()
    {
        var tolerance = GravityOnlyRefinement.TimeTolerance;
        var naive = 1 - tolerance;
        Assert.That((ExactBinary.From(1) - ExactBinary.From(naive)).CompareTo(ExactBinary.From(tolerance)), Is.GreaterThan(0));
        var window = GravityContactEvidence.CreateWindow(0, 1);
        Assert.That(window.Earlier, Is.GreaterThan(naive));
        var exactWidth = ExactBinary.From(1) - ExactBinary.From(window.Earlier);
        Assert.That(exactWidth.Sign, Is.GreaterThan(0));
        Assert.That(exactWidth.CompareTo(ExactBinary.From(tolerance)), Is.LessThanOrEqualTo(0));
        GravityContactArithmeticTests.Contains(window.Width, exactWidth, "actual timing-window width");
    }

    [Test]
    public void WidthMayEqualToleranceWithoutBlindOutwardInflation()
    {
        var t = GravityOnlyRefinement.TimeTolerance;
        var window = GravityContactEvidence.CreateWindow(0, t);
        Assert.That(window.Earlier, Is.Zero);
        Assert.That(window.Width.Lower, Is.EqualTo(t));
        Assert.That(window.Width.Upper, Is.EqualTo(t));
        Assert.DoesNotThrow(() => ValidateSameOrigin(t, -2, t));
    }

    [Test]
    public void SpacingBoundaryUsesActualGapAndRejectsTheNextTimestamp()
    {
        var t = Math.ScaleB(1, 19);
        Assert.DoesNotThrow(() => ValidateSameOrigin(Math.BitDecrement(t), -1, t));
        var next = Math.BitIncrement(t);
        var error = Assert.Throws<ArithmeticException>(() => ValidateSameOrigin(t, -1, next));
        Assert.That(error!.Message, Does.Contain("no representable earlier timestamp"));
        Assert.That((ExactBinary.From(next) - ExactBinary.From(t)).CompareTo(ExactBinary.From(GravityOnlyRefinement.TimeTolerance)), Is.GreaterThan(0));
    }

    [Test]
    public void AValidDelayCanLackAContainingRepresentableWindow()
    {
        const double z = 16.9999999983;
        // Exact delay is (17-z)/17. Compare without rounded division.
        Assert.That((ExactBinary.From(17) - ExactBinary.From(z)).CompareTo(
            ExactBinary.From(GravityOnlyRefinement.TimeTolerance) * ExactBinary.From(17)), Is.LessThanOrEqualTo(0));
        var error = Assert.Throws<ArithmeticException>(() => ValidateSameOrigin(z, -17, 1));
        Assert.That(error!.Message, Does.Contain("before the selected window"));
        Assert.That(error.Message, Does.Not.Contain("too late"));
    }

    [Test]
    public void LocalExactRootCannotHideAnAirborneReleaseTrajectory()
    {
        var anchor = Origin(1, .5, -1);
        var release = FlightFixtures.State(2, -1);
        var error = Assert.Throws<ArithmeticException>(() => GravityContactEvidence.Validate(anchor, release, 1.5, 0, 0));
        Assert.That(error!.Message, Does.Contain("release-origin").And.Contain("still airborne"));
    }

    [Test]
    public void ReleaseExactRootCannotHideAnEarlierLocalCrossing()
    {
        var anchor = Origin(1, .5, -1);
        var error = Assert.Throws<ArithmeticException>(() => GravityContactEvidence.Validate(anchor, FlightFixtures.State(2, -1), 2, 0, 0));
        Assert.That(error!.Message, Does.Contain("ordinary-anchor").And.Contain("selected window"));
    }

    [Test]
    public void DifferentOriginsCanCertifyRootsInTheSameWindow()
    {
        var anchor = Origin(1, 1 - .25 * GravityOnlyRefinement.TimeTolerance, -1);
        Assert.DoesNotThrow(() => GravityContactEvidence.Validate(anchor, FlightFixtures.State(2, -1), 2, 0, 0));
    }

    [Test]
    public void UpwardLaunchExcludesItsInitialRootAndCertifiesTheLaterReturn()
    {
        Assert.Throws<ArithmeticException>(() => ValidateSameOrigin(0, 10, 0, 10));
        var early = Assert.Throws<ArithmeticException>(() => ValidateSameOrigin(0, 10, Math.BitDecrement(2d), 10));
        Assert.That(early!.Message, Does.Contain("still airborne"));
        Assert.DoesNotThrow(() => ValidateSameOrigin(0, 10, 2, 10));
        Assert.DoesNotThrow(() => ValidateSameOrigin(0, 10, 2 + .5 * GravityOnlyRefinement.TimeTolerance, 10));
        Assert.Throws<ArithmeticException>(() => ValidateSameOrigin(0, 10, 2 + 2 * GravityOnlyRefinement.TimeTolerance, 10));
    }

    [Test]
    public void SubnormalAmbiguityFailsWithoutClaimingAnExactRoundedZero()
    {
        var error = Assert.Throws<ArithmeticException>(() => ValidateSameOrigin(1, double.Epsilon,
            Math.BitIncrement(2d / 3), 3 * double.Epsilon, 1));
        Assert.That(error!.Message, Does.Contain("occurrence").And.Contain("unresolved"));
    }

    [Test]
    public void UnsafeReleaseBoundRetainsItsOriginAndOperationContext()
    {
        var unit = Math.ScaleB(1, 1021);
        var anchor = Origin(7, -3 * unit, -unit);
        var error = Assert.Throws<ArithmeticException>(() => GravityContactEvidence.Validate(anchor,
            FlightFixtures.State(4 * unit, -unit), 8, -4 * unit, 0));
        Assert.That(error!.Message, Does.Contain("release-origin candidate").And.Contain("TwoSum sum"));
    }
}
