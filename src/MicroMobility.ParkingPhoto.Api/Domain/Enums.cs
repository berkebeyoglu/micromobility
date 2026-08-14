namespace MicroMobility.ParkingPhoto.Api.Domain;

public enum VehicleKind
{
    Scooter,
    Bicycle,
    Moped
}

/// <summary>
/// Where the client claims the image came from. Only <see cref="Camera"/> is accepted;
/// everything else is rejected before any other check runs.
/// </summary>
public enum PhotoSource
{
    Unknown = 0,
    Camera = 1,
    Gallery = 2,
    Screenshot = 3,
    FileImport = 4
}

public enum IssueSeverity
{
    /// <summary>Shown to the user but does not block ending the ride.</summary>
    Warning,

    /// <summary>Blocks ending the ride, the photo has to be taken again.</summary>
    Blocking
}

public enum ParkingDecision
{
    /// <summary>Photo accepted, the ride can be ended.</summary>
    Accepted,

    /// <summary>At least one blocking issue, the user has to retake the photo.</summary>
    RetakeRequired,

    /// <summary>Retake limit exhausted, an operator has to look at the case.</summary>
    ManualReview
}

public enum RideStatus
{
    Active,
    PendingParkingCheck,
    Completed,
    UnderManualReview
}

/// <summary>
/// Stable machine readable identifiers for every problem the validator can report.
/// The user facing texts live in <see cref="Localization.IssueCatalog"/>.
/// </summary>
public enum IssueCode
{
    // --- Photo source / authenticity -------------------------------------
    CaptureSessionInvalid,
    CaptureSessionExpired,
    PhotoNotFromCamera,
    PhotoEdited,
    PhotoStale,
    PhotoIntegrityMismatch,
    PhotoResolutionTooLow,

    // --- Location ---------------------------------------------------------
    GpsAccuracyTooLow,
    GpsMissing,
    PhotoLocationMismatch,
    SidewalkObstruction,
    AccessibilityObstruction,
    CrosswalkArea,
    TransitStopArea,
    PrivatePropertyProximity,
    NoParkingZone,
    OutsideServiceArea,

    // --- Plate ------------------------------------------------------------
    PlateNotVisible,
    PlateNotReadable,
    PlateMismatch,

    // --- Posture ----------------------------------------------------------
    VehicleNotDetected,
    VehicleNotUpright,
    VehicleFallen
}
