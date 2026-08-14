using System.Globalization;
using System.Security.Cryptography;
using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Localization;
using MicroMobility.ParkingPhoto.Api.Services;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Api.Validation.Rules;

/// <summary>
/// Enforces that the photo was taken live with the phone camera during this end-of-ride flow.
/// Four independent signals have to agree: the single use capture token, the client's own source
/// flag, the EXIF camera signature, and the capture timestamp.
/// </summary>
public sealed class CameraSourceRule(
    ICaptureSessionService sessions,
    IOptions<PhotoValidationOptions> photoOptions,
    IOptions<CaptureSessionOptions> sessionOptions) : IParkingPhotoRule
{
    private readonly PhotoValidationOptions _photo = photoOptions.Value;
    private readonly CaptureSessionOptions _session = sessionOptions.Value;

    public int Order => RuleOrder.Authenticity;

    public ValueTask<IReadOnlyList<ValidationIssue>> EvaluateAsync(
        ParkingPhotoValidationContext context,
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        var metadata = context.Metadata;

        context.SessionState = sessions.Validate(
            metadata.CaptureSessionId,
            metadata.CaptureToken,
            context.Ride.Id,
            metadata.Device.DeviceId);

        switch (context.SessionState)
        {
            case CaptureSessionState.Expired:
                issues.Add(Issue(context, IssueCode.CaptureSessionExpired, ("state", "expired")));
                break;
            case CaptureSessionState.Valid:
                break;
            default:
                issues.Add(Issue(context, IssueCode.CaptureSessionInvalid,
                    ("state", context.SessionState.ToString())));
                break;
        }

        if (metadata.Source != PhotoSource.Camera || !metadata.Device.LiveCameraCapture)
        {
            issues.Add(Issue(context, IssueCode.PhotoNotFromCamera,
                ("reportedSource", metadata.Source.ToString()),
                ("liveCameraCapture", metadata.Device.LiveCameraCapture.ToString())));
        }
        else if (_photo.RequireCameraExif && !context.Exif.HasCameraSignature)
        {
            // A gallery image can claim to be a camera capture; the missing camera signature in the
            // file itself is what actually catches it.
            issues.Add(Issue(context, IssueCode.PhotoNotFromCamera, ("reason", "exif-camera-signature-missing")));
        }
        else if (context.Exif.LooksLikeScreenshot)
        {
            issues.Add(Issue(context, IssueCode.PhotoNotFromCamera, ("reason", "screenshot")));
        }

        var software = context.Exif.Software;
        if (!string.IsNullOrWhiteSpace(software) && IsEditingSoftware(software))
        {
            issues.Add(Issue(context, IssueCode.PhotoEdited, ("software", software)));
        }

        var maxAge = _session.MaxPhotoAgeSeconds + _session.ClockSkewSeconds;

        if (ClientPhotoAgeSeconds(context) is { } clientAge && clientAge > maxAge)
        {
            issues.Add(Issue(context, IssueCode.PhotoStale,
                ("source", "client-timestamp"),
                ("ageSeconds", clientAge.ToString("F0", CultureInfo.InvariantCulture)),
                ("maxAgeSeconds", _session.MaxPhotoAgeSeconds.ToString(CultureInfo.InvariantCulture))));
        }
        else if (ExifDriftSeconds(context) is { } drift && drift > maxAge)
        {
            // EXIF has no UTC offset, so the file's wall clock is compared with the device's own
            // local time. A large gap means an old file is being passed off as a fresh capture.
            issues.Add(Issue(context, IssueCode.PhotoStale,
                ("source", "exif-timestamp"),
                ("driftSeconds", drift.ToString("F0", CultureInfo.InvariantCulture))));
        }

        if (!string.IsNullOrWhiteSpace(metadata.PhotoSha256) || _photo.RequirePhotoHash)
        {
            var actual = Convert.ToHexString(SHA256.HashData(context.PhotoBytes));
            var claimed = metadata.PhotoSha256?.Replace("-", string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(claimed) ||
                !string.Equals(actual, claimed, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Issue(context, IssueCode.PhotoIntegrityMismatch, ("expectedSha256", actual)));
            }
        }

        var width = context.Exif.Width;
        var height = context.Exif.Height;
        if (width is not null && height is not null)
        {
            // Portrait captures report a swapped width/height, so compare on the long/short edges.
            var longEdge = Math.Max(width.Value, height.Value);
            var shortEdge = Math.Min(width.Value, height.Value);
            var requiredLong = Math.Max(_photo.MinWidth, _photo.MinHeight);
            var requiredShort = Math.Min(_photo.MinWidth, _photo.MinHeight);

            if (longEdge < requiredLong || shortEdge < requiredShort)
            {
                issues.Add(Issue(context, IssueCode.PhotoResolutionTooLow,
                    ("width", width.Value.ToString(CultureInfo.InvariantCulture)),
                    ("height", height.Value.ToString(CultureInfo.InvariantCulture))));
            }
        }

        context.PhotoTrusted = issues.All(i => i.Severity != IssueSeverity.Blocking);
        return ValueTask.FromResult<IReadOnlyList<ValidationIssue>>(issues);
    }

    /// <summary>
    /// Age of the capture according to the client clock. A timestamp in the future beyond the
    /// tolerated skew is just as suspicious as an old one, so it is folded into the same value.
    /// </summary>
    private double? ClientPhotoAgeSeconds(ParkingPhotoValidationContext context)
    {
        if (context.Metadata.CapturedAt is not { } capturedAt)
        {
            return null;
        }

        var age = (context.Now - capturedAt).TotalSeconds;
        return age < 0 ? Math.Abs(age) - _session.ClockSkewSeconds : age;
    }

    private static double? ExifDriftSeconds(ParkingPhotoValidationContext context)
    {
        if (context.Exif.TakenAtLocal is not { } exifTaken || context.Metadata.CapturedAt is not { } capturedAt)
        {
            return null;
        }

        return Math.Abs((capturedAt.DateTime - exifTaken).TotalSeconds);
    }

    private bool IsEditingSoftware(string software)
    {
        if (_photo.AllowedSoftwareKeywords.Any(k => software.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return _photo.BlockedSoftwareKeywords.Any(k => software.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static ValidationIssue Issue(
        ParkingPhotoValidationContext context,
        IssueCode code,
        params (string Key, string Value)[] details) =>
        IssueCatalog.Create(code, context.Language, details.ToDictionary(d => d.Key, d => d.Value));
}
