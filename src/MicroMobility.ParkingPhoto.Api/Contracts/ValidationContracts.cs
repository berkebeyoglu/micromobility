using System.Text.Json.Serialization;
using MicroMobility.ParkingPhoto.Api.Domain;

namespace MicroMobility.ParkingPhoto.Api.Contracts;

public sealed class IssueDto
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public string? Action { get; init; }

    public IReadOnlyDictionary<string, string> Details { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Everything the app needs to render the warning sheet. All problems are delivered at once so the
/// user can fix them in a single pass instead of discovering them one photo at a time.
/// </summary>
public sealed class WarningDto
{
    public required string Title { get; init; }

    public required string Headline { get; init; }

    /// <summary>One line per problem, already numbered when there is more than one.</summary>
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>Concrete "do this" instructions, de-duplicated.</summary>
    public required IReadOnlyList<string> Actions { get; init; }

    /// <summary>Title + all lines + actions, ready to be dropped into a single text view.</summary>
    public required string CombinedMessage { get; init; }

    public required string PrimaryButton { get; init; }

    public string? SecondaryButton { get; init; }

    public required bool Blocking { get; init; }
}

public sealed class ZoneConflictDto
{
    public required string ZoneId { get; init; }

    public required string ZoneName { get; init; }

    public required string ZoneKind { get; init; }

    public required double DistanceMeters { get; init; }

    public required bool Inside { get; init; }
}

public sealed class ParkingSuggestionDto
{
    public required string ZoneId { get; init; }

    public required string Name { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public required double DistanceMeters { get; init; }
}

public sealed class LocationReportDto
{
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double? AccuracyMeters { get; init; }

    public required bool Suitable { get; init; }

    public required bool InsideServiceArea { get; init; }

    public required IReadOnlyList<ZoneConflictDto> Conflicts { get; init; }

    public required IReadOnlyList<ParkingSuggestionDto> Suggestions { get; init; }
}

public sealed class PlateReportDto
{
    public required bool Required { get; init; }

    public string? ExpectedPlate { get; init; }

    public string? DetectedPlate { get; init; }

    public double? Confidence { get; init; }

    public required bool Visible { get; init; }
}

public sealed class PostureReportDto
{
    public required bool VehicleDetected { get; init; }

    public double? TiltDegrees { get; init; }

    public required bool Upright { get; init; }

    public required string Source { get; init; }
}

public sealed class PhotoReportDto
{
    public required string Source { get; init; }

    public required bool FromLiveCamera { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public string? CameraMake { get; init; }

    public string? CameraModel { get; init; }

    public DateTimeOffset? TakenAt { get; init; }

    public double? AgeSeconds { get; init; }
}

public sealed class ParkingPhotoValidationResponse
{
    public required string ValidationId { get; init; }

    public required string RideId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ParkingDecision Decision { get; init; }

    public required bool CanEndRide { get; init; }

    public required int Attempt { get; init; }

    public required int RemainingAttempts { get; init; }

    public required IReadOnlyList<IssueDto> Issues { get; init; }

    /// <summary>Null when the photo is accepted.</summary>
    public WarningDto? Warning { get; init; }

    public required PhotoReportDto Photo { get; init; }

    public required LocationReportDto Location { get; init; }

    public PlateReportDto? Plate { get; init; }

    public PostureReportDto? Posture { get; init; }

    public required DateTimeOffset EvaluatedAt { get; init; }
}

public sealed class EndRideResponse
{
    public required string RideId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required RideStatus Status { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public required string Message { get; init; }
}

public sealed class StartRideRequest
{
    public string? UserId { get; set; }

    public string? VehicleId { get; set; }
}
