using System.Text.Json.Serialization;
using MicroMobility.ParkingPhoto.Api.Domain;

namespace MicroMobility.ParkingPhoto.Api.Contracts;

public sealed class CaptureSessionRequest
{
    public string? DeviceId { get; set; }

    public string? Platform { get; set; }

    public string? AppVersion { get; set; }
}

public sealed class CaptureSessionResponse
{
    public required string SessionId { get; init; }

    /// <summary>Single use token the camera layer has to send back with the photo.</summary>
    public required string CaptureToken { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required int MaxPhotoAgeSeconds { get; init; }

    public required int MinPhotoWidth { get; init; }

    public required int MinPhotoHeight { get; init; }

    /// <summary>Whether the plate has to be visible for this vehicle.</summary>
    public required bool PlateRequired { get; init; }

    public string? PlateNumber { get; init; }

    public required IReadOnlyList<string> Instructions { get; init; }
}

public sealed class DeviceInfo
{
    public string? DeviceId { get; set; }

    public string? Platform { get; set; }

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? AppVersion { get; set; }

    /// <summary>
    /// Set by the in-app camera layer. False means the image came from a picker / share sheet.
    /// </summary>
    public bool LiveCameraCapture { get; set; }
}

public sealed class GpsFix
{
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Horizontal accuracy radius in metres (68% confidence).</summary>
    public double AccuracyMeters { get; set; } = double.NaN;

    public DateTimeOffset? CapturedAt { get; set; }

    /// <summary>Android/iOS mock location flag.</summary>
    public bool IsMocked { get; set; }
}

/// <summary>Optional telemetry coming from the vehicle's own IMU over IoT.</summary>
public sealed class VehicleTelemetry
{
    public double? TiltDegrees { get; set; }

    public bool? IsUpright { get; set; }

    public DateTimeOffset? MeasuredAt { get; set; }
}

/// <summary>
/// Deterministic overrides for the vision analysers. Only honoured when
/// <c>Vision:AllowClientHints</c> is enabled (local development and automated tests).
/// </summary>
public sealed class VisionHints
{
    public bool? VehicleDetected { get; set; }

    public double? VehicleTiltDegrees { get; set; }

    public bool? PlateDetected { get; set; }

    public string? PlateText { get; set; }

    public double? PlateConfidence { get; set; }
}

public sealed class CaptureMetadata
{
    public string? CaptureSessionId { get; set; }

    public string? CaptureToken { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PhotoSource Source { get; set; } = PhotoSource.Unknown;

    public DateTimeOffset? CapturedAt { get; set; }

    /// <summary>SHA-256 of the bytes produced by the camera, hex encoded.</summary>
    public string? PhotoSha256 { get; set; }

    public DeviceInfo Device { get; set; } = new();

    public GpsFix Gps { get; set; } = new();

    public VehicleTelemetry? Telemetry { get; set; }

    public VisionHints? VisionHints { get; set; }
}
