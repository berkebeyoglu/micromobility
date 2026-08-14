using MicroMobility.ParkingPhoto.Api.Contracts;
using MicroMobility.ParkingPhoto.Api.Geo;
using MicroMobility.ParkingPhoto.Api.Localization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMobility.ParkingPhoto.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class ZonesController(IZoneRepository zones, IGeofenceService geofence) : ControllerBase
{
    [HttpGet("zones")]
    public ActionResult<IEnumerable<object>> List() =>
        Ok(zones.GetAll().Select(z => new
        {
            z.Id,
            z.Name,
            Kind = z.Kind.ToString(),
            z.BufferMeters,
            Polygon = z.Polygon.Select(p => new { p.Latitude, p.Longitude })
        }));

    /// <summary>
    /// Live parking suitability for a point, used by the map before the user even opens the camera.
    /// Same geofence engine as the photo validation, so both screens always agree.
    /// </summary>
    [HttpGet("parking/check")]
    public ActionResult<object> Check(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double accuracy = 5)
    {
        var point = new GeoPoint(lat, lng);
        if (!point.IsValid)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid coordinates",
                Detail = "lat/lng are outside the valid range.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var assessment = geofence.Assess(point, accuracy);
        var language = Request.Headers.AcceptLanguage.ToString();
        var tr = IssueCatalog.IsTurkish(language);

        var issues = assessment.Conflicts
            .Where(c => c.Zone.IssueCode is not null)
            .Select(c => IssueCatalog.Create(c.Zone.IssueCode!.Value, language))
            .ToArray();

        return Ok(new
        {
            Suitable = assessment.IsSuitable,
            assessment.InsideServiceArea,
            Message = assessment.IsSuitable
                ? tr ? "Bu alan park etmeye uygun." : "This spot is suitable for parking."
                : tr
                    ? "Aracınızın konumu park etmeye uygun değil, lütfen uygun alana geçiniz."
                    : "Your vehicle's location is not suitable for parking, please move to a suitable area.",
            Issues = issues.Select(i => new IssueDto
            {
                Code = i.Code.ToString(),
                Severity = i.Severity.ToString(),
                Title = i.Title,
                Message = i.Message,
                Action = i.Action
            }),
            Conflicts = assessment.Conflicts.Select(c => new ZoneConflictDto
            {
                ZoneId = c.Zone.Id,
                ZoneName = c.Zone.Name,
                ZoneKind = c.Zone.Kind.ToString(),
                DistanceMeters = Math.Round(c.DistanceMeters, 1),
                Inside = c.IsInside
            }),
            Suggestions = assessment.Suggestions.Select(s => new ParkingSuggestionDto
            {
                ZoneId = s.ZoneId,
                Name = s.Name,
                Latitude = s.Center.Latitude,
                Longitude = s.Center.Longitude,
                DistanceMeters = Math.Round(s.DistanceMeters, 1)
            })
        });
    }
}
