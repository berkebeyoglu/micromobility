using System.Globalization;
using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Localization;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Api.Validation.Rules;

/// <summary>
/// Checks that the parked vehicle stands upright. The vehicle's own IMU is preferred when its
/// reading is fresh, otherwise the tilt estimated from the photo is used.
/// </summary>
public sealed class UprightPostureRule(IOptions<PhotoValidationOptions> options) : IParkingPhotoRule
{
    private static readonly TimeSpan TelemetryFreshness = TimeSpan.FromMinutes(2);
    private readonly PhotoValidationOptions _options = options.Value;

    public int Order => RuleOrder.Posture;

    public ValueTask<IReadOnlyList<ValidationIssue>> EvaluateAsync(
        ParkingPhotoValidationContext context,
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();

        var telemetry = context.Metadata.Telemetry;
        var telemetryIsFresh = telemetry?.MeasuredAt is { } measuredAt &&
                               context.Now - measuredAt <= TelemetryFreshness;

        double? tilt = null;
        var source = "none";

        if (telemetryIsFresh && telemetry?.TiltDegrees is { } telemetryTilt)
        {
            tilt = Math.Abs(telemetryTilt);
            source = "vehicle-imu";
        }
        else if (context.Vision is { VehicleDetected: true, TiltDegrees: { } visionTilt })
        {
            tilt = Math.Abs(visionTilt);
            source = context.Vision.Source;
        }
        else if (telemetry?.IsUpright is false)
        {
            tilt = _options.FallenTiltDegrees;
            source = "vehicle-imu";
        }

        context.EffectiveTiltDegrees = tilt;
        context.PostureSource = source;

        if (context.Vision is { VehicleDetected: false })
        {
            context.PostureUpright = false;
            issues.Add(IssueCatalog.Create(IssueCode.VehicleNotDetected, context.Language));
            return ValueTask.FromResult<IReadOnlyList<ValidationIssue>>(issues);
        }

        if (tilt is null)
        {
            context.PostureUpright = true;
            return ValueTask.FromResult<IReadOnlyList<ValidationIssue>>(issues);
        }

        var details = new Dictionary<string, string>
        {
            ["tiltDegrees"] = tilt.Value.ToString("F1", CultureInfo.InvariantCulture),
            ["maxTiltDegrees"] = _options.MaxTiltDegrees.ToString("F1", CultureInfo.InvariantCulture),
            ["source"] = source
        };

        if (tilt >= _options.FallenTiltDegrees)
        {
            context.PostureUpright = false;
            issues.Add(IssueCatalog.Create(IssueCode.VehicleFallen, context.Language, details));
        }
        else if (tilt > _options.MaxTiltDegrees)
        {
            context.PostureUpright = false;
            issues.Add(IssueCatalog.Create(IssueCode.VehicleNotUpright, context.Language, details));
        }
        else
        {
            context.PostureUpright = true;
        }

        return ValueTask.FromResult<IReadOnlyList<ValidationIssue>>(issues);
    }
}
