using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Geo;
using MicroMobility.ParkingPhoto.Api.Services;
using MicroMobility.ParkingPhoto.Api.Validation.Rules;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Tests;

public class ParkingLocationRuleTests
{
    private static ParkingLocationRule CreateRule() =>
        new(new GeofenceService(new InMemoryZoneRepository(SeedZones.Create()),
                Options.Create(new GeofenceOptions())),
            Options.Create(TestData.PhotoOptions()));

    [Fact]
    public async Task Suitable_spot_produces_no_issue()
    {
        var context = TestData.Context(TestData.Metadata());

        var issues = await CreateRule().EvaluateAsync(context, CancellationToken.None);

        Assert.Empty(issues);
        Assert.True(context.LocationAssessment!.IsSuitable);
    }

    [Fact]
    public async Task Sidewalk_produces_obstruction_issue_with_turkish_message()
    {
        var context = TestData.Context(
            TestData.Metadata(lat: TestData.SidewalkLat, lng: TestData.SidewalkLng));

        var issues = await CreateRule().EvaluateAsync(context, CancellationToken.None);

        var issue = Assert.Single(issues, i => i.Code == IssueCode.SidewalkObstruction);
        Assert.Contains("park etmeye uygun değil", issue.Message);
    }

    [Fact]
    public async Task Missing_gps_stops_the_location_check()
    {
        var metadata = TestData.Metadata();
        metadata.Gps.Latitude = null;
        metadata.Gps.Longitude = null;

        var issues = await CreateRule().EvaluateAsync(TestData.Context(metadata), CancellationToken.None);

        Assert.Contains(issues, i => i.Code == IssueCode.GpsMissing);
    }

    [Fact]
    public async Task Mocked_gps_is_treated_as_missing()
    {
        var metadata = TestData.Metadata();
        metadata.Gps.IsMocked = true;

        var issues = await CreateRule().EvaluateAsync(TestData.Context(metadata), CancellationToken.None);

        Assert.Contains(issues, i => i.Code == IssueCode.GpsMissing);
    }

    [Fact]
    public async Task Inaccurate_gps_is_reported()
    {
        var issues = await CreateRule()
            .EvaluateAsync(TestData.Context(TestData.Metadata(accuracy: 120)), CancellationToken.None);

        Assert.Contains(issues, i => i.Code == IssueCode.GpsAccuracyTooLow);
    }

    [Fact]
    public async Task Photo_taken_far_from_the_device_fix_is_reported()
    {
        var context = TestData.Context(
            TestData.Metadata(),
            exif: PhotoExifFacts.Empty with
            {
                Make = "Apple",
                Location = new GeoPoint(TestData.SuitableLat + 0.01, TestData.SuitableLng)
            });

        var issues = await CreateRule().EvaluateAsync(context, CancellationToken.None);

        Assert.Contains(issues, i => i.Code == IssueCode.PhotoLocationMismatch);
    }
}

public class UprightPostureRuleTests
{
    private static UprightPostureRule CreateRule() => new(Options.Create(TestData.PhotoOptions()));

    private static async Task<IReadOnlyList<ValidationIssue>> RunAsync(VisionAnalysis vision)
    {
        var context = TestData.Context(TestData.Metadata(), vision: vision);
        return await CreateRule().EvaluateAsync(context, CancellationToken.None);
    }

    private static VisionAnalysis Vision(double? tilt, bool detected = true) =>
        new(detected, tilt, false, null, null, "test");

    [Fact]
    public async Task Upright_vehicle_passes() => Assert.Empty(await RunAsync(Vision(6)));

    [Fact]
    public async Task Tilted_vehicle_is_reported()
    {
        var issues = await RunAsync(Vision(35));

        var issue = Assert.Single(issues);
        Assert.Equal(IssueCode.VehicleNotUpright, issue.Code);
        Assert.Contains("dik konuma getirin", issue.Message);
    }

    [Fact]
    public async Task Fallen_vehicle_is_reported()
    {
        var issues = await RunAsync(Vision(80));

        Assert.Contains(issues, i => i.Code == IssueCode.VehicleFallen);
    }

    [Fact]
    public async Task Missing_vehicle_is_reported()
    {
        var issues = await RunAsync(Vision(null, detected: false));

        Assert.Contains(issues, i => i.Code == IssueCode.VehicleNotDetected);
    }

    [Fact]
    public async Task Fresh_vehicle_telemetry_wins_over_the_image_estimate()
    {
        var metadata = TestData.Metadata();
        metadata.Telemetry = new MicroMobility.ParkingPhoto.Api.Contracts.VehicleTelemetry
        {
            TiltDegrees = 40,
            MeasuredAt = TestData.Now.AddSeconds(-20)
        };

        var context = TestData.Context(metadata, vision: Vision(2));
        var issues = await CreateRule().EvaluateAsync(context, CancellationToken.None);

        Assert.Contains(issues, i => i.Code == IssueCode.VehicleNotUpright);
        Assert.Equal("vehicle-imu", context.PostureSource);
    }
}

public class LicensePlateRuleTests
{
    private static LicensePlateRule CreateRule() => new(Options.Create(TestData.PhotoOptions()));

    private static async Task<IReadOnlyList<ValidationIssue>> RunAsync(VisionAnalysis vision, Vehicle vehicle)
    {
        var context = TestData.Context(TestData.Metadata(), vehicle: vehicle, vision: vision);
        return await CreateRule().EvaluateAsync(context, CancellationToken.None);
    }

    private static VisionAnalysis Plate(bool detected, string? text, double? confidence) =>
        new(true, 3, detected, text, confidence, "test");

    [Fact]
    public async Task Vehicle_without_plate_skips_the_check()
    {
        var issues = await RunAsync(Plate(false, null, null), TestData.Scooter);

        Assert.Empty(issues);
    }

    [Fact]
    public async Task Readable_matching_plate_passes()
    {
        var issues = await RunAsync(Plate(true, "34ABC123", 0.93), TestData.Moped);

        Assert.Empty(issues);
    }

    [Fact]
    public async Task Invisible_plate_is_reported()
    {
        var issues = await RunAsync(Plate(false, null, null), TestData.Moped);

        var issue = Assert.Single(issues);
        Assert.Equal(IssueCode.PlateNotVisible, issue.Code);
    }

    [Fact]
    public async Task Blurry_plate_is_reported()
    {
        var issues = await RunAsync(Plate(true, "34 ABC 123", 0.42), TestData.Moped);

        Assert.Contains(issues, i => i.Code == IssueCode.PlateNotReadable);
    }

    [Fact]
    public async Task Plate_of_another_vehicle_is_reported()
    {
        var issues = await RunAsync(Plate(true, "06 XYZ 999", 0.97), TestData.Moped);

        Assert.Contains(issues, i => i.Code == IssueCode.PlateMismatch);
    }

    [Theory]
    [InlineData("34 ABC 123", "34abc123", true)]
    [InlineData("34 ABC 123", "34 ABC 12З", true)] // single character OCR slip
    [InlineData("34 ABC 123", "06 XYZ 999", false)]
    public void Plate_comparison_tolerates_formatting_and_one_bad_character(
        string expected, string detected, bool matches) =>
        Assert.Equal(matches, PlateNormalizer.Matches(expected, detected));
}
