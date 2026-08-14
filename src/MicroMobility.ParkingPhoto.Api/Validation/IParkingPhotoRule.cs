using MicroMobility.ParkingPhoto.Api.Domain;

namespace MicroMobility.ParkingPhoto.Api.Validation;

public interface IParkingPhotoRule
{
    /// <summary>Execution order; lower runs first.</summary>
    int Order { get; }

    ValueTask<IReadOnlyList<ValidationIssue>> EvaluateAsync(
        ParkingPhotoValidationContext context,
        CancellationToken cancellationToken);
}

public static class RuleOrder
{
    public const int Authenticity = 10;
    public const int Location = 20;
    public const int Posture = 30;
    public const int Plate = 40;
}
