namespace Lpb.Physics;

// Scratch rates may be nonfinite until checked by the integrator, like FlightDerivative.
internal readonly record struct SpinFlightDerivative(
    double PositionXRate,
    double PositionYRate,
    double PositionZRate,
    double VelocityXRate,
    double VelocityYRate,
    double VelocityZRate,
    double AngularVelocityXRate,
    double AngularVelocityYRate,
    double AngularVelocityZRate);
