using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class GravityContactEvidenceRegressionTests
{
    private static IEnumerable<TestCaseData> PrematureCases()
    {
        foreach (var height in new[] { 1d, Math.ScaleB(1d, 20) })
        foreach (var model in new[] { "gravity", "zero-density", "zero-coefficient" })
        foreach (var scenario in new[] { "original", "adjacent-speed", "iterative", "multistep", "positive-gravity", "upward-return" })
            yield return new TestCaseData(height, model, scenario)
                .SetName($"PrematureRoundedContact_{scenario}_{model}_H{height:R}");
    }

    [TestCaseSource(nameof(PrematureCases))]
    public void PrematureRoundedContactMustFailNumerically(double height, string model, string scenario)
    {
        var unit = Math.BitIncrement(height) - height;
        var request = scenario switch
        {
            "original" => Request(height + unit, -.75 * unit),
            "adjacent-speed" => Request(height + unit, -Math.BitDecrement(unit)),
            "iterative" => Request(height + unit, -1.5 * unit),
            "multistep" => Request(height + 14 * unit, -3.6 * unit, duration: 3 + 2.2 / 3.6),
            "positive-gravity" => Request(height + unit, 0, 1.5 * unit),
            "upward-return" => Request(height, unit, Math.BitDecrement(2 * unit)),
            _ => throw new AssertionException($"Unknown scenario {scenario}.")
        };
        SimulationResult? result = null;
        var error = Assert.Throws<ArithmeticException>(() =>
        {
            result = Run(request, height, model);
            ReportUnexpectedSuccess(result, height);
        });
        Assert.That(result, Is.Null);
        Assert.That(error!.Message, Does.Contain("Integration step").And.Contain("contact"));
        TestContext.Out.WriteLine(error.ToString());
    }

    [TestCase(1, "gravity")]
    [TestCase(1, "zero-density")]
    [TestCase(1, "zero-coefficient")]
    [TestCase(1048576, "gravity")]
    [TestCase(1048576, "zero-density")]
    [TestCase(1048576, "zero-coefficient")]
    public void CrossingByCandidateTimeCannotAuthorizeSubstantiallyLateRoundedContact(double height, string model)
    {
        var unit = Math.BitIncrement(height) - height;
        // Clearance u and constant downward speed 5u/4 give exact contact at 4/5 s.
        // At t=1, Q=-u/4 and W=-5u/4: crossing-by-time evidence passes exactly,
        // but the existing rounded-root shortcut reports contact 0.2 s late.
        var request = Request(height + unit, -1.25 * unit);
        SimulationResult? result = null;
        var error = Assert.Throws<ArithmeticException>(() =>
        {
            result = Run(request, height, model);
            ReportUnexpectedSuccess(result, height);
            TestContext.Out.WriteLine($"Exact reference: 4/5 s; reported delay: {result.FinalTime - .8:R} s.");
        });
        Assert.That(result, Is.Null);
        Assert.That(error!.Message, Does.Contain("Integration step").And.Contain("contact"));
        TestContext.Out.WriteLine(error.ToString());
    }

    private static FlightRequest Request(double height, double velocity, double gravity = 0,
        double duration = 1) => new(new(new(0, 0, height), new(0, 0, velocity)), gravity, duration, 1);

    private static SimulationResult Run(FlightRequest request, double height, string model) => model switch
    {
        "gravity" => GravityOnlySimulator.SimulateUntilGroundContact(request, new(height - 1, 1)),
        "zero-density" => GravityDragSimulator.SimulateUntilGroundContact(request, new(.145, 1, 0, .35), height - 1),
        "zero-coefficient" => GravityDragSimulator.SimulateUntilGroundContact(request, new(.145, 1, 1.225, 0), height - 1),
        _ => throw new AssertionException($"Unknown model {model}.")
    };

    private static void ReportUnexpectedSuccess(SimulationResult result, double height) =>
        TestContext.Out.WriteLine($"Unexpected {result.CompletionReason}: time={result.FinalTime:R}, " +
            $"Z={result.FinalState.Position.Z:R}, H={height:R}, vz={result.FinalState.Velocity.Z:R}, model={result.ModelKind}.");
}
