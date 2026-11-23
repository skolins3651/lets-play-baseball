using NUnit.Framework;

namespace Lpb.Physics.Tests;

public class SpinFlightContractTests
{
    [Test]
    public void StateAndSamplePreserveAllNineComponentsAndDefaultValues()
    {
        var translation = new FlightState(new(-1, 2, -3), new(4, -5, 6));
        var spin = new Vector3d(-7, 8, -9);
        var state = new SpinFlightState(translation, spin);
        var sample = new SpinTrajectorySample(.25, state);
        Assert.That(state.Translation, Is.EqualTo(translation));
        Assert.That(state.AngularVelocity, Is.EqualTo(spin));
        Assert.That(sample.State, Is.EqualTo(state));
        Assert.That(sample.Time, Is.EqualTo(.25));
        Assert.That(default(SpinFlightState), Is.EqualTo(new SpinFlightState(default, default)));
        Assert.That(new SpinFlightState(), Is.EqualTo(default(SpinFlightState)));
        Assert.That(default(SpinTrajectorySample), Is.EqualTo(new SpinTrajectorySample(0, default)));
        Assert.That(new SpinTrajectorySample(), Is.EqualTo(default(SpinTrajectorySample)));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)]
    public void EveryStateComponentParticipatesInExactEquality(int component)
    {
        var values = Enumerable.Range(1, 9).Select(i => (double)i).ToArray();
        var original = FromComponents(values);
        var equal = FromComponents((double[])values.Clone());
        Assert.That(original == equal, Is.True);
        Assert.That(original.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
        values[component] = Math.BitIncrement(values[component]);
        var different = FromComponents(values);
        Assert.That(original == different, Is.False);
        Assert.That(new SpinTrajectorySample(1, original), Is.Not.EqualTo(new SpinTrajectorySample(1, different)));
    }

    [Test]
    public void EqualityUsesOrdinarySignedZeroSemanticsWithoutChangingStoredBits()
    {
        var negativeZero = BitConverter.UInt64BitsToDouble(1UL << 63);
        var state = new SpinFlightState(new(new(negativeZero, 0, 0), default), new(negativeZero, 0, 0));
        Assert.That(state, Is.EqualTo(default(SpinFlightState)));
        Assert.That(BitConverter.DoubleToUInt64Bits(state.AngularVelocity.X), Is.EqualTo(1UL << 63));
        Assert.That(new SpinTrajectorySample(negativeZero, state), Is.EqualTo(default(SpinTrajectorySample)));
        Assert.That(new SpinTrajectorySample(1, state), Is.Not.EqualTo(default(SpinTrajectorySample)));
    }

    [Test]
    public void RequestComposesLegacyInputsAndPreservesSpinAndExplicitValues()
    {
        var state = new SpinFlightState(new(new(-1, -2, -3), new(-4, -5, -6)), new(-7, -8, -9));
        var request = new ModelAFlightRequest(state, 9.80665, 1, .3);
        Assert.That(request.InitialState, Is.EqualTo(state));
        Assert.That(request.GravityMagnitude, Is.EqualTo(9.80665));
        Assert.That(request.Duration, Is.EqualTo(1));
        Assert.That(request.TimeStep, Is.EqualTo(.3));
        Assert.That(request.TranslationRequest.InitialState, Is.EqualTo(state.Translation));
        Assert.That(request.TranslationRequest.GravityMagnitude, Is.EqualTo(request.GravityMagnitude));
        Assert.That(request.TranslationRequest.Duration, Is.EqualTo(request.Duration));
        Assert.That(request.TranslationRequest.TimeStep, Is.EqualTo(request.TimeStep));
        Assert.That(request.Equals(new ModelAFlightRequest(state, 9.80665, 1, .3)), Is.False);
    }

    [TestCase(0, 1, 2)]
    [TestCase(double.Epsilon, double.Epsilon, double.Epsilon)]
    [TestCase(double.MaxValue, double.MaxValue, double.MaxValue)]
    [TestCase(0, double.MaxValue, double.Epsilon)]
    public void RequestAcceptsValidExtremesWithoutRunningPreflightOrNumericalPolicy(double gravity, double duration, double step)
    {
        var request = new ModelAFlightRequest(default, gravity, duration, step);
        Assert.That(request.GravityMagnitude, Is.EqualTo(gravity));
        Assert.That(request.Duration, Is.EqualTo(duration));
        Assert.That(request.TimeStep, Is.EqualTo(step));
    }

    [TestCase(-1)] [TestCase(-double.Epsilon)] [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
    public void RequestRejectsInvalidGravityWithEstablishedParameterName(double value) =>
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new ModelAFlightRequest(default, value, 1, .25))!.ParamName,
            Is.EqualTo("gravityMagnitude"));

    [TestCase(0)] [TestCase(-1)] [TestCase(-double.Epsilon)] [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
    public void RequestRejectsInvalidDuration(double value) =>
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new ModelAFlightRequest(default, 0, value, .25))!.ParamName,
            Is.EqualTo("duration"));

    [TestCase(0)] [TestCase(-1)] [TestCase(-double.Epsilon)] [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
    public void RequestRejectsInvalidTimeStep(double value) =>
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new ModelAFlightRequest(default, 0, 1, value))!.ParamName,
            Is.EqualTo("timeStep"));

    [TestCase(0)] [TestCase(double.Epsilon)] [TestCase(double.MaxValue)]
    public void SampleAcceptsFiniteNonnegativeTime(double value) =>
        Assert.That(new SpinTrajectorySample(value, default).Time, Is.EqualTo(value));

    [TestCase(-1)] [TestCase(-double.Epsilon)] [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
    public void SampleRejectsInvalidTime(double value) =>
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new SpinTrajectorySample(value, default))!.ParamName,
            Is.EqualTo("time"));

    internal static SpinFlightState FromComponents(double[] c) =>
        new(new(new(c[0], c[1], c[2]), new(c[3], c[4], c[5])), new(c[6], c[7], c[8]));
}
