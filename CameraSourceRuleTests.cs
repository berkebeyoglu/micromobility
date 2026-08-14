using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Services;
using MicroMobility.ParkingPhoto.Api.Validation;
using MicroMobility.ParkingPhoto.Api.Validation.Rules;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Tests;

public class CameraSourceRuleTests
{
    private readonly CaptureSessionOptions _sessionOptions = new();
    private readonly ICaptureSessionService _sessions;

    public CameraSourceRuleTests() =>
        _sessions = new CaptureSessionService(
            Options.Create(_sessionOptions), new FixedTimeProvider(TestData.Now));

    private CameraSourceRule CreateRule(PhotoValidationOptions? photoOptions = null) =>
        new(_sessions,
            Options.Create(photoOptions ?? TestData.PhotoOptions()),
            Options.Create(_sessionOptions));

    private CaptureSession NewSession(string rideId = "ride-test") => _sessions.Create(rideId, "device-1");

    private async Task<IReadOnlyList<ValidationIssue>> RunAsync(
        ParkingPhotoValidationContext context,
        PhotoValidationOptions? photoOptions = null) =>
        await CreateRule(photoOptions).EvaluateAsync(context, CancellationToken.None);

    [Fact]
    public async Task Live_camera_capture_with_valid_session_passes()
    {
        var session = NewSession();
        var context = TestData.Context(
            TestData.Metadata(sessionId: session.Id, token: session.Token));

        var issues = await RunAsync(context);

        Assert.Empty(issues);
        Assert.True(context.PhotoTrusted);
    }

    [Fact]
    public async Task Gallery_photo_is_rejected()
    {
        var session = NewSession();
        var context = TestData.Context(
            TestData.Metadata(PhotoSource.Gallery, sessionId: session.Id, token: session.Token));

        var issues = await RunAsync(context);

        Assert.Contains(issues, i => i.Code == IssueCode.PhotoNotFromCamera);
        Assert.False(context.PhotoTrusted);
    }

    [Fact]
    public async Task Missing_capture_token_is_rejected()
    {
        var context = TestData.Context(TestData.Metadata(sessionId: "nope", token: "nope"));

        var issues = await RunAsync(context);

        Assert.Contains(issues, i => i.Code == IssueCode.CaptureSessionInvalid);
    }

    [Fact]
    public async Task Reused_capture_token_is_rejected()
    {
        var session = NewSession();
        _sessions.Consume(session.Id);

        var context = TestData.Context(TestData.Metadata(sessionId: session.Id, token: session.Token));

        var issues = await RunAsync(context);

        Assert.Contains(issues, i => i.Code == IssueCode.CaptureSessionInvalid);
    }

    [Fact]
    public async Task Photo_without_camera_exif_is_rejected_when_exif_is_required()
    {
        var session = NewSession();
        var context = TestData.Context(
            TestData.Metadata(sessionId: session.Id, token: session.Token),
            exif: PhotoExifFacts.Empty);

        var issues = await RunAsync(context, TestData.PhotoOptions(o => o.RequireCameraExif = true));

        Assert.Contains(issues, i =>
            i.Code == IssueCode.PhotoNotFromCamera &&
            i.Details.TryGetValue("reason", out var reason) &&
            reason == "exif-camera-signature-missing");
    }

    [Fact]
    public async Task Edited_photo_is_rejected()
    {
        var session = NewSession();
        var context = TestData.Context(
            TestData.Metadata(sessionId: session.Id, token: session.Token),
            exif: PhotoExifFacts.Empty with { Make = "Apple", Software = "Adobe Photoshop 25.0" });

        var issues = await RunAsync(context);

        Assert.Contains(issues, i => i.Code == IssueCode.PhotoEdited);
    }

    [Fact]
    public async Task Old_photo_is_rejected()
    {
        var session = NewSession();
        var metadata = TestData.Metadata(sessionId: session.Id, token: session.Token);
        metadata.CapturedAt = TestData.Now.AddHours(-3);

        var issues = await RunAsync(TestData.Context(metadata));

        Assert.Contains(issues, i => i.Code == IssueCode.PhotoStale);
    }

    [Fact]
    public async Task Mismatched_photo_hash_is_rejected()
    {
        var session = NewSession();
        var metadata = TestData.Metadata(sessionId: session.Id, token: session.Token);
        metadata.PhotoSha256 = "DEADBEEF";

        var issues = await RunAsync(TestData.Context(metadata));

        Assert.Contains(issues, i => i.Code == IssueCode.PhotoIntegrityMismatch);
    }

    [Fact]
    public async Task Low_resolution_photo_is_rejected()
    {
        var session = NewSession();
        var context = TestData.Context(
            TestData.Metadata(sessionId: session.Id, token: session.Token),
            exif: PhotoExifFacts.Empty with { Make = "Apple", Width = 320, Height = 240 });

        var issues = await RunAsync(context);

        Assert.Contains(issues, i => i.Code == IssueCode.PhotoResolutionTooLow);
    }

    [Fact]
    public async Task Portrait_photo_is_not_rejected_for_swapped_dimensions()
    {
        var session = NewSession();
        var context = TestData.Context(
            TestData.Metadata(sessionId: session.Id, token: session.Token),
            exif: PhotoExifFacts.Empty with { Make = "Apple", Width = 1080, Height = 1920 });

        var issues = await RunAsync(context);

        Assert.DoesNotContain(issues, i => i.Code == IssueCode.PhotoResolutionTooLow);
    }
}
