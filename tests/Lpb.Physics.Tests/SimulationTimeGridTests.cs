using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class SimulationTimeGridTests
{
    [Test]
    public void EarlyTerminationBudgetDoesNotSubstituteDurationForLastIndexedBoundary()
    {
        var grid = new SimulationTimeGrid(new FlightRequest(default, 0, 1_000_000, 1), requireWholeHorizon: false);
        Assert.That(grid.StepCount, Is.EqualTo(100000));
        Assert.That(grid.GetNextTime(100000, 99999), Is.EqualTo(100000));
    }

    [TestCase(double.MaxValue, double.MaxValue)]
    [TestCase(1, 0.3)]
    public void EarlyTerminationGridStillUsesExactRequestedEndWhenReached(double duration, double dt)
    {
        var grid = new SimulationTimeGrid(new FlightRequest(default, 0, duration, dt), requireWholeHorizon: false);
        var time = 0d;
        for (var step = 1; step <= grid.StepCount; step++)
            time = grid.GetNextTime(step, time);
        Assert.That(time, Is.EqualTo(duration));
    }

    [TestCase(2, 0.25, 8)]
    [TestCase(1, 0.3, 4)]
    [TestCase(1, 2, 1)]
    [TestCase(0.3, 0.1, 3)]
    [TestCase(0.6, 0.1, 6)]
    [TestCase(100000, 1, 100000)]
    [TestCase(double.MaxValue, double.MaxValue, 1)]
    [TestCase(double.Epsilon, double.Epsilon, 1)]
    public void PreflightFindsMinimumIndexedStepCount(double duration, double step, int expectedCount)
    {
        var grid = Create(duration, step);

        Assert.That(grid.StepCount, Is.EqualTo(expectedCount));
        Assert.That((double)grid.StepCount * step, Is.GreaterThanOrEqualTo(duration));
        Assert.That((double)(grid.StepCount - 1) * step, Is.LessThan(duration));
    }

    [TestCase(100001, 1)]
    [TestCase(double.MaxValue, double.Epsilon)]
    [TestCase(double.MaxValue, 1e-300)]
    public void PreflightRejectsExcessiveRequestsIncludingOverflowingRatios(double duration, double step)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Create(duration, step));
        Assert.That(exception!.Message, Does.Contain("100000").And.Contain("duration").And.Contain("timestep"));
    }

    [TestCase(1)]
    [TestCase(1e-5)]
    [TestCase(1e300)]
    public void ResourceBoundaryUsesIndexedMultiplicationAndAdjacentRepresentableDurations(double step)
    {
        var boundary = 100000d * step;

        Assert.That(Create(boundary, step).StepCount, Is.EqualTo(100000));
        Assert.That(Create(Math.BitDecrement(boundary), step).StepCount, Is.EqualTo(100000));
        Assert.Throws<InvalidOperationException>(() => Create(Math.BitIncrement(boundary), step));
    }

    [Test]
    public void RoundedDivisionIsNotUsedToRejectPermittedGrid()
    {
        var step = 1e300;
        var duration = 100000d * step;
        Assert.That(Math.Ceiling(duration / step), Is.GreaterThan(100000));
        Assert.That(Create(duration, step).StepCount, Is.EqualTo(100000));
    }

    [Test]
    public void AdjacentTimestepsUseTheirOwnRoundedBoundaries()
    {
        foreach (var step in new[] { Math.BitDecrement(1e-5), 1e-5, Math.BitIncrement(1e-5) })
        {
            var boundary = 100000d * step;
            Assert.That(Create(boundary, step).StepCount, Is.EqualTo(100000));
            Assert.Throws<InvalidOperationException>(() => Create(Math.BitIncrement(boundary), step));
        }
    }

    [TestCase(2, 0.25)]
    [TestCase(1, 0.3)]
    [TestCase(0.3, 0.1)]
    [TestCase(double.MaxValue, double.MaxValue)]
    [TestCase(double.Epsilon, double.Epsilon)]
    public void ExecutionTimesAgreeWithPreflightAndEndExactlyWithoutOvershoot(double duration, double step)
    {
        var grid = Create(duration, step);
        var current = 0d;
        for (var i = 1; i <= grid.StepCount; i++)
        {
            var next = grid.GetNextTime(i, current);
            Assert.That(double.IsFinite(next), Is.True);
            Assert.That(next, Is.GreaterThan(current).And.LessThanOrEqualTo(duration));
            if (i < grid.StepCount)
            {
                Assert.That(next, Is.EqualTo((double)i * step));
                Assert.That(next, Is.LessThan(duration));
            }

            current = next;
        }

        Assert.That(current, Is.EqualTo(duration));
    }

    [TestCase(0.25)]
    [TestCase(0.5)]
    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void ExecutionRejectsDuplicateBackwardOrInvalidCurrentTime(double current)
    {
        var grid = Create(1, 0.25);
        var exception = Assert.Throws<ArithmeticException>(() => grid.GetNextTime(1, current));
        Assert.That(exception!.Message, Does.Contain("Step 1"));
    }

    [TestCase(0)]
    [TestCase(5)]
    public void ExecutionRejectsIndexOutsideApprovedSteps(int index)
    {
        var grid = Create(1, 0.25);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetNextTime(index, 0));
    }

    private static SimulationTimeGrid Create(double duration, double step) => new(new FlightRequest(default, 0, duration, step));
}
