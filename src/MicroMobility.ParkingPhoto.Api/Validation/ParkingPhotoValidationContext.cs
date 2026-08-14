using MicroMobility.ParkingPhoto.Api.Contracts;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Geo;
using MicroMobility.ParkingPhoto.Api.Services;

namespace MicroMobility.ParkingPhoto.Api.Validation;

/// <summary>
/// Shared state for one photo evaluation. Rules read the inputs and publish their findings here so
/// the orchestrator can build a single response that lists every problem at once.
/// </summary>
public sealed class ParkingPhotoValidationContext
{
    public required Ride Ride { get; init; }

    public required Vehicle Vehicle { get; init; }

    public required CaptureMetadata Metadata { get; init; }

    public required byte[] PhotoBytes { get; init; }

    public required string ContentType { get; init; }

    public required PhotoExifFacts Exif { get; init; }

    public required string Language { get; init; }

    public required DateTimeOffset Now { get; init; }

    public required int Attempt { get; init; }

    public GeoPoint? DeviceFix =>
        Metadata.Gps is { Latitude: not null, Longitude: not null }
            ? new GeoPoint(Metadata.Gps.Latitude.Value, Metadata.Gps.Longitude.Value)
            : null;

    public CaptureSessionState SessionState { get; set; } = CaptureSessionState.NotFound;

    /// <summary>False when the image itself cannot be trusted, which makes vision results pointless.</summary>
    public bool PhotoTrusted { get; set; }

    public ParkingLocationAssessment? LocationAssessment { get; set; }

    public VisionAnalysis? Vision { get; set; }

    /// <summary>Tilt actually used for the posture verdict, and where it came from.</summary>
    public double? EffectiveTiltDegrees { get; set; }

    public string PostureSource { get; set; } = "none";

    public bool PostureUpright { get; set; } = true;

    public string? DetectedPlate { get; set; }
}
