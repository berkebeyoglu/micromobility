using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Contracts;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Services;
using MicroMobility.ParkingPhoto.Api.Validation;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Tests;

/// <summary>Shared builders so each test only states the input it actually cares about.</summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 8, 13, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Coordinates from the seeded Kadıköy demo data.</summary>
    public const double SuitableLat = 40.9903;

    public const double SuitableLng = 29.0281;
    public const double SidewalkLat = 40.9905;
    public const double SidewalkLng = 29.0270;
    public const double CrosswalkLat = 40.9908;
    public const double CrosswalkLng = 29.0262;
    public const double TransitStopLat = 40.9890;
    public const double TransitStopLng = 29.0300;

    public static Vehicle Scooter => new() { Id = "SCOOTER-1001", Kind = VehicleKind.Scooter };

    public static Vehicle Moped => new()
    {
        Id = "MOPED-3001",
        Kind = VehicleKind.Moped,
        PlateNumber = "34 ABC 123"
    };

    public static Ride Ride(int failedAttempts = 0) => new()
    {
        Id = "ride-test",
        UserId = "user-1",
        VehicleId = "SCOOTER-1001",
        StartedAt = Now.AddMinutes(-10),
        FailedPhotoAttempts = failedAttempts
    };

    public static PhotoValidationOptions PhotoOptions(Action<PhotoValidationOptions>? configure = null)
    {
        var options = new PhotoValidationOptions { RequireCameraExif = false };
        configure?.Invoke(options);
        return options;
    }

    public static CaptureMetadata Metadata(
        PhotoSource source = PhotoSource.Camera,
        double lat = SuitableLat,
        double lng = SuitableLng,
        double accuracy = 5,
        string? sessionId = null,
        string? token = null,
        double? tilt = null,
        bool vehicleDetected = true,
        bool plateDetected = true,
        string? plateText = null,
        double plateConfidence = 0.95) => new()
    {
        CaptureSessionId = sessionId,
        CaptureToken = token,
        Source = source,
        CapturedAt = Now.AddSeconds(-5),
        Device = new DeviceInfo
        {
            DeviceId = "device-1",
            Platform = "ios",
            Manufacturer = "Apple",
            Model = "iPhone 15",
            AppVersion = "1.0.0",
            LiveCameraCapture = source == PhotoSource.Camera
        },
        Gps = new GpsFix
        {
            Latitude = lat,
            Longitude = lng,
            AccuracyMeters = accuracy,
            CapturedAt = Now
        },
        VisionHints = new VisionHints
        {
            VehicleDetected = vehicleDetected,
            VehicleTiltDegrees = tilt,
            PlateDetected = plateDetected,
            PlateText = plateText,
            PlateConfidence = plateConfidence
        }
    };

    public static ParkingPhotoValidationContext Context(
        CaptureMetadata metadata,
        Vehicle? vehicle = null,
        Ride? ride = null,
        PhotoExifFacts? exif = null,
        VisionAnalysis? vision = null) => new()
    {
        Ride = ride ?? Ride(),
        Vehicle = vehicle ?? Scooter,
        Metadata = metadata,
        PhotoBytes = [1, 2, 3, 4],
        ContentType = "image/jpeg",
        Exif = exif ?? PhotoExifFacts.Empty with { Make = "Apple", Model = "iPhone 15" },
        Language = "tr",
        Now = Now,
        Attempt = 1,
        Vision = vision
    };

    public static IOptions<T> Wrap<T>(T value) where T : class => Options.Create(value);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class StubVisionAnalyzer(VisionAnalysis analysis) : IVisionAnalyzer
{
    public Task<VisionAnalysis> AnalyzeAsync(VisionRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(analysis);
}

internal sealed class StubExifReader(PhotoExifFacts facts) : IExifReader
{
    public PhotoExifFacts Read(Stream photoStream) => facts;
}

internal sealed class NoopPhotoStore : IPhotoStore
{
    public Task<string?> SaveAsync(
        string rideId, string validationId, byte[] photo, string contentType, CancellationToken ct) =>
        Task.FromResult<string?>(null);
}
