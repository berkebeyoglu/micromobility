using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Contracts;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Geo;
using MicroMobility.ParkingPhoto.Api.Services;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Api.Validation;

public sealed record ValidateParkingPhotoCommand(
    Ride Ride,
    Vehicle Vehicle,
    byte[] PhotoBytes,
    string ContentType,
    CaptureMetadata Metadata,
    string Language);

public interface IParkingPhotoValidator
{
    Task<ParkingPhotoValidationResponse> ValidateAsync(
        ValidateParkingPhotoCommand command,
        CancellationToken cancellationToken);
}

/// <summary>
/// Runs every parking rule against a single photo and merges the findings into one verdict.
/// Rules are never short-circuited on the first failure: the user gets the complete list of what is
/// wrong so a single retake can fix everything.
/// </summary>
public sealed class ParkingPhotoValidator(
    IEnumerable<IParkingPhotoRule> rules,
    IExifReader exifReader,
    IVisionAnalyzer visionAnalyzer,
    ICaptureSessionService captureSessions,
    IPhotoStore photoStore,
    IOptions<PhotoValidationOptions> options,
    TimeProvider timeProvider,
    ILogger<ParkingPhotoValidator> logger) : IParkingPhotoValidator
{
    private readonly IParkingPhotoRule[] _rules = rules.OrderBy(r => r.Order).ToArray();
    private readonly PhotoValidationOptions _options = options.Value;

    public async Task<ParkingPhotoValidationResponse> ValidateAsync(
        ValidateParkingPhotoCommand command,
        CancellationToken cancellationToken)
    {
        var validationId = Guid.NewGuid().ToString("N");
        var now = timeProvider.GetUtcNow();

        using var photoStream = new MemoryStream(command.PhotoBytes, writable: false);
        var exif = exifReader.Read(photoStream);

        var context = new ParkingPhotoValidationContext
        {
            Ride = command.Ride,
            Vehicle = command.Vehicle,
            Metadata = command.Metadata,
            PhotoBytes = command.PhotoBytes,
            ContentType = command.ContentType,
            Exif = exif,
            Language = command.Language,
            Now = now,
            Attempt = command.Ride.FailedPhotoAttempts + 1
        };

        var issues = new List<ValidationIssue>();

        foreach (var rule in _rules)
        {
            // Plate and posture verdicts are only meaningful on an image we trust, and running the
            // vision model on a rejected upload would just waste an inference call.
            if (rule.Order >= RuleOrder.Posture)
            {
                if (!context.PhotoTrusted)
                {
                    continue;
                }

                context.Vision ??= await AnalyzeAsync(context, cancellationToken);
            }

            issues.AddRange(await rule.EvaluateAsync(context, cancellationToken));
        }

        if (context.SessionState == CaptureSessionState.Valid &&
            !string.IsNullOrWhiteSpace(command.Metadata.CaptureSessionId))
        {
            // Burn the token either way: every retake opens a fresh camera session.
            captureSessions.Consume(command.Metadata.CaptureSessionId);
        }

        var blocking = issues.Where(i => i.Severity == IssueSeverity.Blocking).ToArray();
        var decision = blocking.Length == 0
            ? ParkingDecision.Accepted
            : context.Attempt >= _options.MaxRetakeAttempts
                ? ParkingDecision.ManualReview
                : ParkingDecision.RetakeRequired;

        ApplyToRide(command.Ride, decision, validationId);

        var storedPath = await photoStore.SaveAsync(
            command.Ride.Id, validationId, command.PhotoBytes, command.ContentType, cancellationToken);

        logger.LogInformation(
            "Parking photo {ValidationId} for ride {RideId}: {Decision} with {IssueCount} issue(s) [{Codes}], stored at {Path}",
            validationId, command.Ride.Id, decision, issues.Count,
            string.Join(",", issues.Select(i => i.Code)), storedPath ?? "-");

        var remainingAttempts = Math.Max(0, _options.MaxRetakeAttempts - context.Attempt);

        return new ParkingPhotoValidationResponse
        {
            ValidationId = validationId,
            RideId = command.Ride.Id,
            Decision = decision,
            CanEndRide = decision != ParkingDecision.RetakeRequired,
            Attempt = context.Attempt,
            RemainingAttempts = remainingAttempts,
            Issues = issues
                .OrderBy(i => i.DisplayOrder)
                .Select(ToDto)
                .ToArray(),
            Warning = WarningBuilder.Build(issues, decision, remainingAttempts, command.Language),
            Photo = BuildPhotoReport(context),
            Location = BuildLocationReport(context),
            Plate = BuildPlateReport(context),
            Posture = BuildPostureReport(context),
            EvaluatedAt = now
        };
    }

    private async Task<VisionAnalysis?> AnalyzeAsync(
        ParkingPhotoValidationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await visionAnalyzer.AnalyzeAsync(
                new VisionRequest(
                    context.PhotoBytes,
                    context.ContentType,
                    context.Vehicle.RequiresPlate,
                    context.Vehicle.PlateNumber,
                    context.Metadata.VisionHints),
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // An unavailable model must not strand the user mid-ride: skip the vision based rules
            // and let the remaining checks decide.
            logger.LogError(ex, "Vision analysis failed for ride {RideId}", context.Ride.Id);
            return null;
        }
    }

    private static void ApplyToRide(Ride ride, ParkingDecision decision, string validationId)
    {
        switch (decision)
        {
            case ParkingDecision.Accepted:
                ride.ApprovedValidationId = validationId;
                ride.Status = RideStatus.PendingParkingCheck;
                ride.FailedPhotoAttempts = 0;
                break;

            case ParkingDecision.ManualReview:
                ride.FailedPhotoAttempts++;
                ride.ApprovedValidationId = validationId;
                ride.Status = RideStatus.UnderManualReview;
                break;

            default:
                ride.FailedPhotoAttempts++;
                ride.Status = RideStatus.PendingParkingCheck;
                break;
        }
    }

    private static IssueDto ToDto(ValidationIssue issue) => new()
    {
        Code = issue.Code.ToString(),
        Severity = issue.Severity.ToString(),
        Title = issue.Title,
        Message = issue.Message,
        Action = issue.Action,
        Details = issue.Details
    };

    private static PhotoReportDto BuildPhotoReport(ParkingPhotoValidationContext context)
    {
        var takenAt = context.Metadata.CapturedAt;

        return new PhotoReportDto
        {
            Source = context.Metadata.Source.ToString(),
            FromLiveCamera = context.Metadata.Device.LiveCameraCapture && context.Exif.HasCameraSignature,
            Width = context.Exif.Width,
            Height = context.Exif.Height,
            CameraMake = context.Exif.Make,
            CameraModel = context.Exif.Model,
            TakenAt = takenAt,
            AgeSeconds = takenAt is null ? null : Math.Round((context.Now - takenAt.Value).TotalSeconds, 1)
        };
    }

    private static LocationReportDto BuildLocationReport(ParkingPhotoValidationContext context)
    {
        var assessment = context.LocationAssessment;
        var fix = context.DeviceFix;

        return new LocationReportDto
        {
            Latitude = fix?.Latitude,
            Longitude = fix?.Longitude,
            AccuracyMeters = double.IsNaN(context.Metadata.Gps.AccuracyMeters)
                ? null
                : context.Metadata.Gps.AccuracyMeters,
            Suitable = assessment?.IsSuitable ?? false,
            InsideServiceArea = assessment?.InsideServiceArea ?? false,
            Conflicts = assessment?.Conflicts.Select(c => new ZoneConflictDto
            {
                ZoneId = c.Zone.Id,
                ZoneName = c.Zone.Name,
                ZoneKind = c.Zone.Kind.ToString(),
                DistanceMeters = Math.Round(c.DistanceMeters, 1),
                Inside = c.IsInside
            }).ToArray() ?? [],
            Suggestions = assessment?.Suggestions.Select(s => new ParkingSuggestionDto
            {
                ZoneId = s.ZoneId,
                Name = s.Name,
                Latitude = s.Center.Latitude,
                Longitude = s.Center.Longitude,
                DistanceMeters = Math.Round(s.DistanceMeters, 1)
            }).ToArray() ?? []
        };
    }

    private static PlateReportDto? BuildPlateReport(ParkingPhotoValidationContext context)
    {
        if (!context.Vehicle.RequiresPlate)
        {
            return null;
        }

        return new PlateReportDto
        {
            Required = true,
            ExpectedPlate = context.Vehicle.PlateNumber,
            DetectedPlate = context.DetectedPlate,
            Confidence = context.Vision?.PlateConfidence,
            Visible = context.Vision?.PlateDetected ?? false
        };
    }

    private static PostureReportDto? BuildPostureReport(ParkingPhotoValidationContext context)
    {
        if (context.Vision is null && context.EffectiveTiltDegrees is null)
        {
            return null;
        }

        return new PostureReportDto
        {
            VehicleDetected = context.Vision?.VehicleDetected ?? true,
            TiltDegrees = context.EffectiveTiltDegrees,
            Upright = context.PostureUpright,
            Source = context.PostureSource
        };
    }
}
