using System.Globalization;
using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Localization;
using MicroMobility.ParkingPhoto.Api.Services;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Api.Validation.Rules;

/// <summary>
/// For vehicles that carry a registration plate (mopeds), the plate has to be legible in the photo
/// and has to belong to the rented vehicle. Vehicles without a plate skip this rule entirely.
/// </summary>
public sealed class LicensePlateRule(IOptions<PhotoValidationOptions> options) : IParkingPhotoRule
{
    private readonly PhotoValidationOptions _options = options.Value;

    public int Order => RuleOrder.Plate;

    public ValueTask<IReadOnlyList<ValidationIssue>> EvaluateAsync(
        ParkingPhotoValidationContext context,
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();

        if (!context.Vehicle.RequiresPlate || context.Vision is null)
        {
            return ValueTask.FromResult<IReadOnlyList<ValidationIssue>>(issues);
        }

        var vision = context.Vision;
        var expected = context.Vehicle.PlateNumber;
        context.DetectedPlate = vision.PlateText;

        var details = new Dictionary<string, string>
        {
            ["expectedPlate"] = expected ?? string.Empty,
            ["detectedPlate"] = vision.PlateText ?? string.Empty,
            ["confidence"] = vision.PlateConfidence?.ToString("F2", CultureInfo.InvariantCulture) ?? "0.00",
            ["minConfidence"] = _options.MinPlateConfidence.ToString("F2", CultureInfo.InvariantCulture)
        };

        if (!vision.PlateDetected || string.IsNullOrWhiteSpace(vision.PlateText))
        {
            issues.Add(IssueCatalog.Create(IssueCode.PlateNotVisible, context.Language, details));
            return ValueTask.FromResult<IReadOnlyList<ValidationIssue>>(issues);
        }

        if ((vision.PlateConfidence ?? 0) < _options.MinPlateConfidence)
        {
            issues.Add(IssueCatalog.Create(IssueCode.PlateNotReadable, context.Language, details));
            return ValueTask.FromResult<IReadOnlyList<ValidationIssue>>(issues);
        }

        if (!PlateNormalizer.Matches(expected, vision.PlateText))
        {
            issues.Add(IssueCatalog.Create(IssueCode.PlateMismatch, context.Language, details));
        }

        return ValueTask.FromResult<IReadOnlyList<ValidationIssue>>(issues);
    }
}
