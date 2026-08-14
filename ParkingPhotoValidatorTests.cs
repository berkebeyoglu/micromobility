using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Geo;
using MicroMobility.ParkingPhoto.Api.Services;
using MicroMobility.ParkingPhoto.Api.Validation;
using MicroMobility.ParkingPhoto.Api.Validation.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Tests;

public class ParkingPhotoValidatorTests
{
    private readonly CaptureSessionOptions _sessionOptions = new();
    private readonly PhotoValidationOptions _photoOptions = TestData.PhotoOptions();
    private readonly ICaptureSessionService _sessions;

    public ParkingPhotoValidatorTests() =>
        _sessions = new CaptureSessionService(
            Options.Create(_sessionOptions), new FixedTimeProvider(TestData.Now));

    private ParkingPhotoValidator CreateValidator(VisionAnalysis vision) =>
        new(
            [
                new CameraSourceRule(_sessions, Options.Create(_photoOptions), Options.Create(_sessionOptions)),
                new ParkingLocationRule(
                    new GeofenceService(new InMemoryZoneRepository(SeedZones.Create()),
                        Options.Create(new GeofenceOptions())),
                    Options.Create(_photoOptions)),
                new UprightPostureRule(Options.Create(_photoOptions)),
                new LicensePlateRule(Options.Create(_photoOptions))
            ],
            new StubExifReader(PhotoExifFacts.Empty with { Make = "Apple", Model = "iPhone 15" }),
            new StubVisionAnalyzer(vision),
            _sessions,
            new NoopPhotoStore(),
            Options.Create(_photoOptions),
            new FixedTimeProvider(TestData.Now),
            NullLogger<ParkingPhotoValidator>.Instance);

    private static VisionAnalysis Vision(double tilt = 4, bool plateDetected = true, string? plate = "34 ABC 123") =>
        new(true, tilt, plateDetected, plate, plateDetected ? 0.95 : null, "test");

    private async Task<Api.Contracts.ParkingPhotoValidationResponse> ValidateAsync(
        Api.Contracts.CaptureMetadata metadata,
        VisionAnalysis vision,
        Ride ride,
        Vehicle? vehicle = null) =>
        await CreateValidator(vision).ValidateAsync(
            new ValidateParkingPhotoCommand(
                ride, vehicle ?? TestData.Scooter, [1, 2, 3, 4], "image/jpeg", metadata, "tr"),
            CancellationToken.None);

    [Fact]
    public async Task Clean_photo_is_accepted_and_unlocks_the_ride()
    {
        var ride = TestData.Ride();
        var session = _sessions.Create(ride.Id, "device-1");

        var result = await ValidateAsync(
            TestData.Metadata(sessionId: session.Id, token: session.Token), Vision(), ride);

        Assert.Equal(ParkingDecision.Accepted, result.Decision);
        Assert.True(result.CanEndRide);
        Assert.Empty(result.Issues);
        Assert.Null(result.Warning);
        Assert.NotNull(ride.ApprovedValidationId);
    }

    [Fact]
    public async Task Every_problem_is_reported_in_one_response()
    {
        var ride = TestData.Ride();
        var session = _sessions.Create(ride.Id, "device-1");

        // Parked on the sidewalk, moped plate not visible, vehicle lying on its side.
        var result = await ValidateAsync(
            TestData.Metadata(
                lat: TestData.SidewalkLat,
                lng: TestData.SidewalkLng,
                sessionId: session.Id,
                token: session.Token),
            Vision(tilt: 70, plateDetected: false, plate: null),
            ride,
            TestData.Moped);

        var codes = result.Issues.Select(i => i.Code).ToArray();
        Assert.Contains(nameof(IssueCode.SidewalkObstruction), codes);
        Assert.Contains(nameof(IssueCode.VehicleFallen), codes);
        Assert.Contains(nameof(IssueCode.PlateNotVisible), codes);

        Assert.Equal(ParkingDecision.RetakeRequired, result.Decision);
        Assert.False(result.CanEndRide);

        var warning = Assert.IsType<Api.Contracts.WarningDto>(result.Warning);
        Assert.Equal(3, warning.Lines.Count);
        Assert.All(warning.Lines, line => Assert.Matches(@"^\d\. ", line));
        Assert.Contains("hepsini düzeltip", warning.Headline);
        Assert.Contains("Ne yapmalısınız?", warning.CombinedMessage);
        Assert.Equal("Fotoğrafı yeniden çek", warning.PrimaryButton);
        Assert.Equal("Uygun park alanlarını göster", warning.SecondaryButton);
    }

    [Fact]
    public async Task Untrusted_photo_skips_the_vision_checks()
    {
        var ride = TestData.Ride();
        var session = _sessions.Create(ride.Id, "device-1");

        var result = await ValidateAsync(
            TestData.Metadata(PhotoSource.Gallery, sessionId: session.Id, token: session.Token),
            Vision(tilt: 80, plateDetected: false, plate: null),
            ride,
            TestData.Moped);

        var codes = result.Issues.Select(i => i.Code).ToArray();
        Assert.Contains(nameof(IssueCode.PhotoNotFromCamera), codes);
        Assert.DoesNotContain(nameof(IssueCode.VehicleFallen), codes);
        Assert.DoesNotContain(nameof(IssueCode.PlateNotVisible), codes);
    }

    [Fact]
    public async Task Location_is_still_checked_when_the_photo_is_rejected()
    {
        var ride = TestData.Ride();

        var result = await ValidateAsync(
            TestData.Metadata(PhotoSource.Gallery, lat: TestData.CrosswalkLat, lng: TestData.CrosswalkLng),
            Vision(),
            ride);

        var codes = result.Issues.Select(i => i.Code).ToArray();
        Assert.Contains(nameof(IssueCode.PhotoNotFromCamera), codes);
        Assert.Contains(nameof(IssueCode.CrosswalkArea), codes);
    }

    [Fact]
    public async Task Repeated_failures_escalate_to_manual_review()
    {
        var ride = TestData.Ride(failedAttempts: _photoOptions.MaxRetakeAttempts - 1);
        var session = _sessions.Create(ride.Id, "device-1");

        var result = await ValidateAsync(
            TestData.Metadata(
                lat: TestData.TransitStopLat,
                lng: TestData.TransitStopLng,
                sessionId: session.Id,
                token: session.Token),
            Vision(),
            ride);

        Assert.Equal(ParkingDecision.ManualReview, result.Decision);
        Assert.True(result.CanEndRide);
        Assert.Equal(RideStatus.UnderManualReview, ride.Status);
        Assert.Equal("Destek ekibine gönder", result.Warning!.SecondaryButton);
    }

    [Fact]
    public async Task Rejected_photo_offers_nearby_parking_spots()
    {
        var ride = TestData.Ride();
        var session = _sessions.Create(ride.Id, "device-1");

        var result = await ValidateAsync(
            TestData.Metadata(
                lat: TestData.SidewalkLat,
                lng: TestData.SidewalkLng,
                sessionId: session.Id,
                token: session.Token),
            Vision(),
            ride);

        Assert.NotEmpty(result.Location.Suggestions);
        Assert.False(result.Location.Suitable);
    }

    [Fact]
    public async Task English_client_gets_english_messages()
    {
        var ride = TestData.Ride();
        var session = _sessions.Create(ride.Id, "device-1");

        var result = await CreateValidator(Vision()).ValidateAsync(
            new ValidateParkingPhotoCommand(
                ride,
                TestData.Scooter,
                [1, 2, 3, 4],
                "image/jpeg",
                TestData.Metadata(
                    lat: TestData.SidewalkLat,
                    lng: TestData.SidewalkLng,
                    sessionId: session.Id,
                    token: session.Token),
                "en-GB"),
            CancellationToken.None);

        Assert.Contains("not suitable for parking", result.Warning!.CombinedMessage);
    }
}
