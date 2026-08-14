namespace MicroMobility.ParkingPhoto.Api.Configuration;

public sealed class CaptureSessionOptions
{
    public const string SectionName = "CaptureSession";

    /// <summary>How long the camera token stays valid after it is issued.</summary>
    public int TtlSeconds { get; set; } = 240;

    /// <summary>A photo older than this is treated as "not taken now".</summary>
    public int MaxPhotoAgeSeconds { get; set; } = 120;

    /// <summary>Clock skew tolerated on the device timestamps.</summary>
    public int ClockSkewSeconds { get; set; } = 60;

    /// <summary>Reject a token that was already used for a validation.</summary>
    public bool SingleUse { get; set; } = true;
}

public sealed class PhotoValidationOptions
{
    public const string SectionName = "PhotoValidation";

    public int MinWidth { get; set; } = 1280;

    public int MinHeight { get; set; } = 720;

    public long MaxFileSizeBytes { get; set; } = 12 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/jpg", "image/heic", "image/heif"];

    /// <summary>Require EXIF camera make/model. Disable only if your camera pipeline strips EXIF.</summary>
    public bool RequireCameraExif { get; set; } = true;

    /// <summary>Require the SHA-256 the camera layer computed to match the uploaded bytes.</summary>
    public bool RequirePhotoHash { get; set; } = false;

    /// <summary>Worst GPS accuracy we still accept for a parking decision.</summary>
    public double MaxGpsAccuracyMeters { get; set; } = 25;

    /// <summary>Max distance between the EXIF GPS tag and the reported device fix.</summary>
    public double MaxPhotoLocationDeltaMeters { get; set; } = 40;

    /// <summary>Above this tilt the vehicle is not considered upright.</summary>
    public double MaxTiltDegrees { get; set; } = 20;

    /// <summary>Above this tilt the vehicle is reported as fallen over.</summary>
    public double FallenTiltDegrees { get; set; } = 55;

    public double MinPlateConfidence { get; set; } = 0.75;

    /// <summary>After this many rejected photos the ride goes to manual review instead of looping.</summary>
    public int MaxRetakeAttempts { get; set; } = 3;

    /// <summary>Editing software allowed to appear in the EXIF Software tag (camera firmware).</summary>
    public string[] AllowedSoftwareKeywords { get; set; } = ["camera", "ios", "android", "hdr"];

    public string[] BlockedSoftwareKeywords { get; set; } =
        ["photoshop", "lightroom", "gimp", "snapseed", "picsart", "facetune", "canva", "screenshot", "paint"];
}

public sealed class VisionOptions
{
    public const string SectionName = "Vision";

    /// <summary>Inference endpoint for the plate + posture model. Empty = use the local simulator.</summary>
    public string? Endpoint { get; set; }

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 8;

    /// <summary>
    /// Allow the client to send deterministic vision results (VisionHints). Development only:
    /// never enable in production, it lets the app decide its own verdict.
    /// </summary>
    public bool AllowClientHints { get; set; } = true;
}

public sealed class PhotoStorageOptions
{
    public const string SectionName = "PhotoStorage";

    public string RootPath { get; set; } = "App_Data/parking-photos";

    public bool Enabled { get; set; } = true;
}
