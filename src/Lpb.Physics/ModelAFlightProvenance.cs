namespace Lpb.Physics;

// Retained inputs are private to the result's implementation, not part of its public graph.
internal sealed class ModelAFlightProvenance
{
    // This identifies the approved future policy; declaring it does not attest execution.
    internal const string ApprovedNumericalPolicyIdentity = "classical-rk4/indexed-v1;model-a-full-state-v1";

    internal ModelAFlightProvenance(ModelAFlightRequest request, ModelAParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(parameters);
        Request = request;
        Parameters = parameters;
    }

    internal ModelAFlightRequest Request { get; }
    internal ModelAParameters Parameters { get; }
    internal string ModelIdentity => Parameters.PrescriptionIdentity;

    // I1 has no checked producer. Only a later authorized runtime can populate this.
    internal string? NumericalPolicyIdentity { get; }

    internal void ValidateRequest(ModelAFlightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Request.InitialState != request.InitialState || Request.GravityMagnitude != request.GravityMagnitude ||
            Request.Duration != request.Duration || Request.TimeStep != request.TimeStep)
            throw new ArgumentException("The request does not match the retained Model A inputs.", nameof(request));
    }

    internal void ValidateParameters(ModelAParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var retained = Parameters.Aerodynamics;
        var supplied = parameters.Aerodynamics;
        var rotation = Parameters.Rotation;
        var suppliedRotation = parameters.Rotation;
        if (Parameters.PrescriptionIdentity != parameters.PrescriptionIdentity ||
            retained.BallMass != supplied.BallMass || retained.BallRadius != supplied.BallRadius ||
            retained.AirDensity != supplied.AirDensity || retained.DragCoefficient != supplied.DragCoefficient ||
            rotation.Kind != suppliedRotation.Kind || rotation.RotationalInertia != suppliedRotation.RotationalInertia ||
            rotation.Beta != suppliedRotation.Beta)
            throw new ArgumentException("Parameters do not match the retained Model A inputs.", nameof(parameters));
    }
}
