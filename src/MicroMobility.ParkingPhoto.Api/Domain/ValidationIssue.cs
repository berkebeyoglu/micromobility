namespace MicroMobility.ParkingPhoto.Api.Domain;

/// <summary>
/// A single problem detected on the end-of-ride photo. Several issues can be reported at once so
/// the user sees everything that has to be fixed before taking the next photo.
/// </summary>
public sealed class ValidationIssue
{
    public required IssueCode Code { get; init; }

    public required IssueSeverity Severity { get; init; }

    /// <summary>Short headline for the warning list.</summary>
    public required string Title { get; init; }

    /// <summary>The sentence shown to the user.</summary>
    public required string Message { get; init; }

    /// <summary>Concrete instruction, e.g. "Aracı kaldırıma değil park cebine bırakın".</summary>
    public string? Action { get; init; }

    /// <summary>Diagnostic values (distances, confidences, ...) for support and analytics.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Lower number = shown higher in the warning list.</summary>
    public int DisplayOrder { get; init; } = 100;
}
