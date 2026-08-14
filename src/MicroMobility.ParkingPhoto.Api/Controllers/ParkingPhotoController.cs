using System.Text.Json;
using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Contracts;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Localization;
using MicroMobility.ParkingPhoto.Api.Services;
using MicroMobility.ParkingPhoto.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Api.Controllers;

[ApiController]
[Route("api/v1/rides/{rideId}/parking-photo")]
[Produces("application/json")]
public sealed class ParkingPhotoController(
    IRideRepository rides,
    IVehicleRepository vehicles,
    ICaptureSessionService captureSessions,
    IParkingPhotoValidator validator,
    IOptions<PhotoValidationOptions> photoOptions,
    IOptions<CaptureSessionOptions> sessionOptions) : ControllerBase
{
    private static readonly JsonSerializerOptions MetadataJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly PhotoValidationOptions _photo = photoOptions.Value;
    private readonly CaptureSessionOptions _session = sessionOptions.Value;

    /// <summary>
    /// Opens a camera session. The app must call this right before showing the camera; the returned
    /// single use token is what proves the photo came from the in-app camera.
    /// </summary>
    [HttpPost("session")]
    [ProducesResponseType(typeof(CaptureSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<CaptureSessionResponse> CreateSession(string rideId, [FromBody] CaptureSessionRequest? request)
    {
        var ride = rides.Find(rideId);
        if (ride is null)
        {
            return NotFound(Problem($"Ride '{rideId}' was not found.", StatusCodes.Status404NotFound));
        }

        if (ride.Status is RideStatus.Completed)
        {
            return Conflict(Problem("The ride is already completed.", StatusCodes.Status409Conflict));
        }

        var vehicle = vehicles.Find(ride.VehicleId);
        if (vehicle is null)
        {
            return NotFound(Problem($"Vehicle '{ride.VehicleId}' was not found.", StatusCodes.Status404NotFound));
        }

        var session = captureSessions.Create(ride.Id, request?.DeviceId);
        var tr = IssueCatalog.IsTurkish(ResolveLanguage());

        return Ok(new CaptureSessionResponse
        {
            SessionId = session.Id,
            CaptureToken = session.Token,
            IssuedAt = session.IssuedAt,
            ExpiresAt = session.ExpiresAt,
            MaxPhotoAgeSeconds = _session.MaxPhotoAgeSeconds,
            MinPhotoWidth = _photo.MinWidth,
            MinPhotoHeight = _photo.MinHeight,
            PlateRequired = vehicle.RequiresPlate,
            PlateNumber = vehicle.PlateNumber,
            Instructions = BuildInstructions(vehicle, tr)
        });
    }

    /// <summary>
    /// Validates the end-of-ride photo. Returns every problem at once together with a ready to show
    /// warning sheet; the ride can only be ended when no blocking issue remains.
    /// </summary>
    [HttpPost("validate")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ParkingPhotoValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParkingPhotoValidationResponse>> Validate(
        string rideId,
        [FromForm] IFormFile? photo,
        [FromForm] string? metadata,
        CancellationToken cancellationToken)
    {
        var ride = rides.Find(rideId);
        if (ride is null)
        {
            return NotFound(Problem($"Ride '{rideId}' was not found.", StatusCodes.Status404NotFound));
        }

        if (ride.Status == RideStatus.Completed)
        {
            return Conflict(Problem("The ride is already completed.", StatusCodes.Status409Conflict));
        }

        var vehicle = vehicles.Find(ride.VehicleId);
        if (vehicle is null)
        {
            return NotFound(Problem($"Vehicle '{ride.VehicleId}' was not found.", StatusCodes.Status404NotFound));
        }

        if (photo is null || photo.Length == 0)
        {
            return BadRequest(Problem("A photo file is required.", StatusCodes.Status400BadRequest));
        }

        if (photo.Length > _photo.MaxFileSizeBytes)
        {
            return BadRequest(Problem(
                $"The photo is larger than the {_photo.MaxFileSizeBytes} byte limit.",
                StatusCodes.Status400BadRequest));
        }

        var contentType = photo.ContentType ?? "application/octet-stream";
        if (!_photo.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(Problem(
                $"Unsupported content type '{contentType}'. Allowed: {string.Join(", ", _photo.AllowedContentTypes)}.",
                StatusCodes.Status400BadRequest));
        }

        CaptureMetadata? captureMetadata;
        try
        {
            captureMetadata = string.IsNullOrWhiteSpace(metadata)
                ? null
                : JsonSerializer.Deserialize<CaptureMetadata>(metadata, MetadataJson);
        }
        catch (JsonException ex)
        {
            return BadRequest(Problem($"Capture metadata is not valid JSON: {ex.Message}",
                StatusCodes.Status400BadRequest));
        }

        if (captureMetadata is null)
        {
            return BadRequest(Problem("Capture metadata is required.", StatusCodes.Status400BadRequest));
        }

        using var buffer = new MemoryStream();
        await photo.CopyToAsync(buffer, cancellationToken);

        var result = await validator.ValidateAsync(
            new ValidateParkingPhotoCommand(
                ride, vehicle, buffer.ToArray(), contentType, captureMetadata, ResolveLanguage()),
            cancellationToken);

        return Ok(result);
    }

    private string ResolveLanguage()
    {
        if (Request.Query.TryGetValue("lang", out var queryLanguage) && !string.IsNullOrWhiteSpace(queryLanguage))
        {
            return queryLanguage.ToString();
        }

        var header = Request.Headers.AcceptLanguage.ToString();
        return string.IsNullOrWhiteSpace(header) ? IssueCatalog.DefaultLanguage : header.Split(',')[0];
    }

    private static IReadOnlyList<string> BuildInstructions(Vehicle vehicle, bool tr)
    {
        var instructions = new List<string>();

        if (tr)
        {
            instructions.Add("Fotoğrafı yalnızca telefon kamerasıyla, aracın yanındayken çekin.");
            instructions.Add("Aracın tamamı görünecek şekilde 2-3 metre geriden çekin.");
            instructions.Add("Aracı dik konumda ve ayaklığı açık bırakın.");
            instructions.Add("Yaya yolu, rampa, yaya geçidi ve durakları kapatmayın.");

            if (vehicle.RequiresPlate)
            {
                instructions.Add($"{vehicle.PlateNumber} plakası fotoğrafta net okunacak şekilde görünsün.");
            }
        }
        else
        {
            instructions.Add("Take the photo with the phone camera while standing next to the vehicle.");
            instructions.Add("Step back 2-3 metres so the whole vehicle is in the frame.");
            instructions.Add("Leave the vehicle upright on its kickstand.");
            instructions.Add("Do not block walkways, ramps, crossings or transit stops.");

            if (vehicle.RequiresPlate)
            {
                instructions.Add($"Make sure plate {vehicle.PlateNumber} is clearly readable in the photo.");
            }
        }

        return instructions;
    }

    private ProblemDetails Problem(string detail, int status) => new()
    {
        Title = status == StatusCodes.Status404NotFound ? "Not found" : "Invalid request",
        Detail = detail,
        Status = status,
        Instance = HttpContext.Request.Path
    };
}
