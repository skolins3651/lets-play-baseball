namespace Lpb.Physics;

// Immutable retained inputs, including geometry on duration-limited contact-aware flights.
// Policy identifiers describe fixed implementation rules; they are not a solver registry.
internal sealed class FlightProvenance
{
    internal FlightProvenance(FlightRequest request, GroundContactSettings? contact, AerodynamicSettings? aerodynamics = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (aerodynamics is not null && contact is not null && contact.BallRadius != aerodynamics.BallRadius)
            throw new ArgumentException("Contact must use the aerodynamic ball radius.", nameof(contact));
        Request = request;
        Contact = contact;
        Aerodynamics = aerodynamics;
    }

    internal FlightRequest Request { get; }
    internal GroundContactSettings? Contact { get; }
    internal AerodynamicSettings? Aerodynamics { get; }
    internal FlightModelKind ModelKind => Aerodynamics is null ? FlightModelKind.GravityOnly : FlightModelKind.GravityAndDrag;
    internal string NumericalPolicy => Aerodynamics is null || Aerodynamics.Disabled
        ? "classical-rk4/indexed-v1;gravity-events-v2"
        : "classical-rk4/indexed-v1;drag-full-state-v1;drag-events-v1";

    internal void ValidateRequest(FlightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Request.InitialState != request.InitialState || Request.GravityMagnitude != request.GravityMagnitude ||
            Request.Duration != request.Duration || Request.TimeStep != request.TimeStep)
            throw new ArgumentException("The request does not match the result's model inputs.", nameof(request));
    }

    internal void ValidateAerodynamics(AerodynamicSettings aerodynamics)
    {
        ArgumentNullException.ThrowIfNull(aerodynamics);
        if (Aerodynamics is null || Aerodynamics.BallMass != aerodynamics.BallMass ||
            Aerodynamics.BallRadius != aerodynamics.BallRadius || Aerodynamics.AirDensity != aerodynamics.AirDensity ||
            Aerodynamics.DragCoefficient != aerodynamics.DragCoefficient)
            throw new ArgumentException("Aerodynamic settings do not match the result's inputs.", nameof(aerodynamics));
    }
}
