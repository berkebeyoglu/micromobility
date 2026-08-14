namespace MicroMobility.ParkingPhoto.Api.Domain;

public sealed class Vehicle
{
    public required string Id { get; init; }

    public required VehicleKind Kind { get; init; }

    /// <summary>Registration plate. Null for vehicles that are not required to carry one.</summary>
    public string? PlateNumber { get; init; }

    /// <summary>True when the plate has to be legible in the end-of-ride photo.</summary>
    public bool RequiresPlate => !string.IsNullOrWhiteSpace(PlateNumber);
}
