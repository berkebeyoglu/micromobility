using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Api.Geo;

public sealed class GeofenceOptions
{
    public const string SectionName = "Geofence";

    /// <summary>How far around the vehicle we look for conflicting zones.</summary>
    public double QueryRadiusMeters { get; set; } = 80;

    /// <summary>Reject the photo when the point is not inside any service area polygon.</summary>
    public bool EnforceServiceArea { get; set; } = true;

    /// <summary>How many alternative parking spots are offered to the user.</summary>
    public int MaxParkingSuggestions { get; set; } = 3;

    /// <summary>
    /// The GPS error is added to the measured distance, so a spot that is only "clear" because the
    /// fix is fuzzy is still reported. Capped so a bad fix cannot flag the whole neighbourhood.
    /// </summary>
    public double MaxAccuracyCompensationMeters { get; set; } = 10;
}

public sealed record ParkingSuggestion(string ZoneId, string Name, GeoPoint Center, double DistanceMeters);

public sealed record ParkingLocationAssessment(
    bool ServiceAreaConfigured,
    bool InsideServiceArea,
    IReadOnlyList<ZoneConflict> Conflicts,
    IReadOnlyList<ParkingSuggestion> Suggestions)
{
    public bool IsSuitable => Conflicts.Count == 0 && (!ServiceAreaConfigured || InsideServiceArea);
}

public interface IGeofenceService
{
    ParkingLocationAssessment Assess(GeoPoint point, double accuracyMeters);
}

public sealed class GeofenceService(IZoneRepository zones, IOptions<GeofenceOptions> options) : IGeofenceService
{
    private readonly GeofenceOptions _options = options.Value;

    public ParkingLocationAssessment Assess(GeoPoint point, double accuracyMeters)
    {
        var candidates = zones.QueryNear(point, _options.QueryRadiusMeters);
        var slack = Math.Clamp(accuracyMeters, 0, _options.MaxAccuracyCompensationMeters);

        var conflicts = new List<ZoneConflict>();
        var suggestions = new List<ParkingSuggestion>();
        var serviceAreaConfigured = false;
        var insideServiceArea = false;

        foreach (var zone in candidates)
        {
            var distance = GeoMath.DistanceToPolygonMeters(point, zone.Polygon);
            var inside = distance <= 0;

            switch (zone.Kind)
            {
                case ZoneKind.ServiceArea:
                    serviceAreaConfigured = true;
                    insideServiceArea |= inside;
                    break;

                case ZoneKind.ParkingSpot:
                    suggestions.Add(new ParkingSuggestion(zone.Id, zone.Name, Centroid(zone.Polygon), distance));
                    break;

                default:
                    if (distance - slack <= zone.BufferMeters)
                    {
                        conflicts.Add(new ZoneConflict(zone, distance, inside));
                    }

                    break;
            }
        }

        // A service area polygon can be much larger than the query radius, so re-check globally
        // when the pre-filter found none nearby.
        if (!serviceAreaConfigured)
        {
            foreach (var zone in zones.GetAll().Where(z => z.Kind == ZoneKind.ServiceArea))
            {
                serviceAreaConfigured = true;
                insideServiceArea |= GeoMath.IsInsidePolygon(point, zone.Polygon);
            }
        }

        if (suggestions.Count == 0)
        {
            suggestions.AddRange(zones.GetAll()
                .Where(z => z.Kind == ZoneKind.ParkingSpot)
                .Select(z => new ParkingSuggestion(
                    z.Id, z.Name, Centroid(z.Polygon), GeoMath.DistanceToPolygonMeters(point, z.Polygon))));
        }

        return new ParkingLocationAssessment(
            serviceAreaConfigured && _options.EnforceServiceArea,
            insideServiceArea,
            conflicts.OrderBy(c => c.DistanceMeters).ToArray(),
            suggestions
                .OrderBy(s => s.DistanceMeters)
                .Take(Math.Max(0, _options.MaxParkingSuggestions))
                .ToArray());
    }

    private static GeoPoint Centroid(IReadOnlyList<GeoPoint> polygon) =>
        new(polygon.Average(p => p.Latitude), polygon.Average(p => p.Longitude));
}
