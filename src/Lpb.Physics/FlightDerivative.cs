namespace Lpb.Physics;

// Scratch rates may be nonfinite until checked by the integrator. They are not physical output states.
internal readonly record struct FlightDerivative(
    double PositionXRate,
    double PositionYRate,
    double PositionZRate,
    double VelocityXRate,
    double VelocityYRate,
    double VelocityZRate);
