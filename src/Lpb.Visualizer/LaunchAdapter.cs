using Lpb.Physics;

namespace Lpb.Visualizer;

// Private application input, deliberately unrelated to the future public export schema.
internal sealed record LaunchInput
{
    public required double SpeedMph { get; init; }
    public required double ElevationDegrees { get; init; }
    public required double FieldAzimuthDegrees { get; init; }
    public required double CenterHeightFeet { get; init; }
    public required double GravityMagnitude { get; init; }
    public required double TimeStepSeconds { get; init; }
    public required double DurationSeconds { get; init; }
    public required double GroundElevationFeet { get; init; }
    public required double BallRadiusInches { get; init; }
    public required string Mode { get; init; }
}

internal sealed record RunInput(LaunchInput Launch, int DisplayPointCount);
internal sealed record DisplayInput(int DisplayPointCount);
internal sealed record Direction(double X, double Y);
internal sealed record ExecutedScenario(LaunchInput Launch, string Model, FlightState InitialState,
    double GroundElevationMetres, double BallRadiusMetres, Direction Direction);
internal sealed record AdaptedLaunch(FlightRequest Request, GroundContactSettings Contact, ExecutedScenario Scenario);

internal sealed class InputException(Dictionary<string, string> errors) : Exception("Check the highlighted inputs.")
{
    public Dictionary<string, string> Errors { get; } = errors;
}

internal static class LaunchAdapter
{
    internal const double MetresPerFoot = 0.3048;
    internal const double MetresPerInch = 0.0254;
    internal const double MetresPerSecondPerMph = 0.44704;

    public static AdaptedLaunch Convert(LaunchInput? input)
    {
        if (input is null)
            throw new InputException(new() { ["launch"] = "Launch settings are required." });
        var errors = new Dictionary<string, string>();
        void Check(string field, double value, bool valid, string message)
        {
            if (!double.IsFinite(value) || !valid) errors[field] = message;
        }
        Check("speedMph", input.SpeedMph, input.SpeedMph >= 0, "Speed must be finite and nonnegative (mph).");
        Check("elevationDegrees", input.ElevationDegrees, input.ElevationDegrees is >= -90 and <= 90, "Elevation must be between -90 and 90 degrees.");
        Check("fieldAzimuthDegrees", input.FieldAzimuthDegrees, input.FieldAzimuthDegrees is >= -180 and <= 180, "Field azimuth must be between -180 and 180 degrees.");
        Check("centerHeightFeet", input.CenterHeightFeet, true, "Center height must be finite (ft).");
        Check("gravityMagnitude", input.GravityMagnitude, input.GravityMagnitude >= 0, "Gravity must be finite and nonnegative (m/s²).");
        Check("timeStepSeconds", input.TimeStepSeconds, input.TimeStepSeconds > 0, "Timestep must be finite and positive (s).");
        Check("durationSeconds", input.DurationSeconds, input.DurationSeconds > 0, "Duration must be finite and positive (s).");
        Check("groundElevationFeet", input.GroundElevationFeet, true, "Ground elevation must be finite (ft).");
        Check("ballRadiusInches", input.BallRadiusInches, input.BallRadiusInches > 0, "Radius must be finite and positive (in).");
        if (input.Mode is not ("contact" or "unconstrained")) errors["mode"] = "Choose contact or unconstrained flight.";
        if (errors.Count > 0) throw new InputException(errors);

        var speed = input.SpeedMph * MetresPerSecondPerMph;
        var ground = input.GroundElevationFeet * MetresPerFoot;
        var radius = input.BallRadiusInches * MetresPerInch;
        GroundContactSettings contact;
        try { contact = new GroundContactSettings(ground, radius); }
        catch (ArgumentException exception)
        {
            throw new InputException(new() { ["ballRadiusInches"] = exception.Message });
        }
        var z = ground + input.CenterHeightFeet * MetresPerFoot;
        if (!double.IsFinite(z))
            throw new InputException(new() { ["centerHeightFeet"] = "Ground elevation plus center height is not representable." });
        var (sinElevation, cosElevation) = SinCosDegrees(input.ElevationDegrees);
        var (sinAzimuth, cosAzimuth) = SinCosDegrees(input.FieldAzimuthDegrees);
        var velocity = speed == 0 ? new Vector3d(0, 0, 0)
            : cosElevation == 0 ? new Vector3d(0, 0, input.ElevationDegrees > 0 ? speed : -speed)
            : new Vector3d(cosAzimuth == 0 ? 0 : speed * cosElevation * cosAzimuth,
                sinAzimuth == 0 ? 0 : speed * cosElevation * sinAzimuth,
                sinElevation == 0 ? 0 : speed * sinElevation);
        var state = new FlightState(new Vector3d(0, 0, z), velocity);
        var request = new FlightRequest(state, input.GravityMagnitude, input.DurationSeconds, input.TimeStepSeconds);
        return new(request, contact, new(input, "Gravity-only · RK4", state, ground, radius, new(cosAzimuth, sinAzimuth)));
    }

    // Only exact degree values receive exact mathematical pairs. Never threshold nearby inputs.
    internal static (double Sin, double Cos) SinCosDegrees(double degrees) => degrees switch
    {
        -180 => (0, -1), -90 => (-1, 0), 0 => (0, 1), 90 => (1, 0), 180 => (0, -1),
        _ => Math.SinCos(degrees * (Math.PI / 180))
    };
}
