using System.Globalization;
using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Geo;
using MicroMobility.ParkingPhoto.Api.Localization;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Api.Validation.Rules;

/// <summary>
/// Decides whether the spot the photo was taken at is legal to park at: sidewalks, accessibility
/// routes, crosswalks, transit stops and private property all have to stay clear, and the vehicle
/// has to be inside the service area.
/// </summary>
public sealed class ParkingLocationRule(
    IGeofenceService geofence,
    IOptions<PhotoValidationOptions> options) : IParkingPhotoRule
{
    private readonly PhotoValidationOptions _options = options.Value;

    public int Order => RuleOrder.Location;

    public ValueTask<IReadOnlyList<ValidationIssue>> EvaluateAsync(
        ParkingPhotoValidationContext context,
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        var fix = context.DeviceFix;

        if (fix is null || !fix.Value.IsValid || context.Metadata.Gps.IsMocked)
        {
            issues.Add(IssueCatalog.Create(IssueCode.GpsMissing, context.Language,
                new Dictionary<string, string>
                {
                    ["mocked"] = context.Metadata.Gps.IsMocked.ToString()
                }));

            return ValueTask.FromResult<IReadOnlyList<ValidationIssue>>(issues);
        }

        var accuracy = context.Metadata.Gps.AccuracyMeters;
        if (double.IsNaN(accuracy) || accuracy <= 0 || accuracy > _options.MaxGpsAccuracyMeters)
        {
            issues.Add(IssueCatalog.Create(IssueCode.GpsAccuracyTooLow, context.Language,
                new Dictionary<string, string>
                {
                    ["accuracyMeters"] = Format(accuracy),
                    ["maxAccuracyMeters"] = Format(_options.MaxGpsAccuracyMeters)
                }));

            accuracy = _options.MaxGpsAccuracyMeters;
        }

        // The EXIF GPS tag is an independent witness: if it is far from the reported fix, the photo
        // was not taken where the vehicle is standing.
        if (context.Exif.Location is { } exifLocation && exifLocation.IsValid)
        {
            var delta = GeoMath.DistanceMeters(exifLocation, fix.Value);
            if (delta > _options.MaxPhotoLocationDeltaMeters)
            {
                issues.Add(IssueCatalog.Create(IssueCode.PhotoLocationMismatch, context.Language,
                    new Dictionary<string, string>
                    {
                        ["deltaMeters"] = Format(delta),
                        ["photoLocation"] = exifLocation.ToString(),
                        ["deviceLocation"] = fix.Value.ToString()
                    }));
            }
        }

        var assessment = geofence.Assess(fix.Value, accuracy);
        context.LocationAssessment = assessment;

        foreach (var conflict in assessment.Conflicts)
        {
            if (conflict.Zone.IssueCode is not { } code)
            {
                continue;
            }

            issues.Add(IssueCatalog.Create(code, context.Language, new Dictionary<string, string>
            {
                ["zoneId"] = conflict.Zone.Id,
                ["zoneName"] = conflict.Zone.Name,
                ["distanceMeters"] = Format(conflict.DistanceMeters),
                ["inside"] = conflict.IsInside.ToString()
            }));
        }

        if (assessment.ServiceAreaConfigured && !assessment.InsideServiceArea)
        {
            issues.Add(IssueCatalog.Create(IssueCode.OutsideServiceArea, context.Language));
        }

        return ValueTask.FromResult<IReadOnlyList<ValidationIssue>>(issues);
    }

    private static string Format(double value) =>
        double.IsNaN(value) ? "unknown" : value.ToString("F1", CultureInfo.InvariantCulture);
}
