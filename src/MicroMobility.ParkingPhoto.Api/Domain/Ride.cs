namespace MicroMobility.ParkingPhoto.Api.Domain;

public sealed class Ride
{
    public required string Id { get; init; }

    public required string UserId { get; init; }

    public required string VehicleId { get; init; }

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt { get; set; }

    public RideStatus Status { get; set; } = RideStatus.Active;

    /// <summary>Number of parking photos that were rejected for this ride.</summary>
    public int FailedPhotoAttempts { get; set; }

    /// <summary>Id of the last validation that produced an acceptable photo.</summary>
    public string? ApprovedValidationId { get; set; }
}
