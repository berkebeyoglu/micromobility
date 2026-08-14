using MicroMobility.ParkingPhoto.Api.Contracts;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Localization;
using MicroMobility.ParkingPhoto.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MicroMobility.ParkingPhoto.Api.Controllers;

[ApiController]
[Route("api/v1/rides")]
[Produces("application/json")]
public sealed class RidesController(
    IRideRepository rides,
    IVehicleRepository vehicles,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Ride), StatusCodes.Status201Created)]
    public ActionResult<Ride> Start([FromBody] StartRideRequest request)
    {
        var vehicleId = request.VehicleId ?? "SCOOTER-1001";
        if (vehicles.Find(vehicleId) is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not found",
                Detail = $"Vehicle '{vehicleId}' was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var ride = rides.Add(new Ride
        {
            Id = $"ride-{Guid.NewGuid():N}"[..16],
            UserId = request.UserId ?? "user-1",
            VehicleId = vehicleId,
            StartedAt = timeProvider.GetUtcNow()
        });

        return CreatedAtAction(nameof(Get), new { rideId = ride.Id }, ride);
    }

    [HttpGet("{rideId}")]
    [ProducesResponseType(typeof(Ride), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Ride> Get(string rideId) =>
        rides.Find(rideId) is { } ride ? Ok(ride) : NotFound();

    [HttpGet]
    public ActionResult<IEnumerable<Ride>> List() => Ok(rides.GetAll());

    /// <summary>
    /// Ends the ride. Only allowed once a parking photo has been accepted (or handed to support),
    /// so the photo check cannot be skipped.
    /// </summary>
    [HttpPost("{rideId}/end")]
    [ProducesResponseType(typeof(EndRideResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<EndRideResponse> End(string rideId)
    {
        var ride = rides.Find(rideId);
        if (ride is null)
        {
            return NotFound();
        }

        var tr = IssueCatalog.IsTurkish(Request.Headers.AcceptLanguage.ToString());

        if (ride.Status == RideStatus.Completed)
        {
            return Ok(new EndRideResponse
            {
                RideId = ride.Id,
                Status = ride.Status,
                EndedAt = ride.EndedAt,
                Message = tr ? "Sürüş zaten bitirilmiş." : "The ride is already completed."
            });
        }

        if (ride.ApprovedValidationId is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Parking photo required",
                Detail = tr
                    ? "Sürüşü bitirmek için önce park fotoğrafının onaylanması gerekiyor."
                    : "An approved parking photo is required before the ride can be ended.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var underReview = ride.Status == RideStatus.UnderManualReview;
        ride.EndedAt = timeProvider.GetUtcNow();
        ride.Status = underReview ? RideStatus.UnderManualReview : RideStatus.Completed;

        return Ok(new EndRideResponse
        {
            RideId = ride.Id,
            Status = ride.Status,
            EndedAt = ride.EndedAt,
            Message = underReview
                ? tr
                    ? "Sürüş bitirildi, park fotoğrafı destek ekibi tarafından incelenecek."
                    : "Ride ended; the parking photo will be reviewed by support."
                : tr
                    ? "Sürüş bitirildi. İyi günler!"
                    : "Ride completed. Have a nice day!"
        });
    }

    [HttpGet("/api/v1/vehicles")]
    public ActionResult<IEnumerable<Vehicle>> Vehicles() => Ok(vehicles.GetAll());
}
