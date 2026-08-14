using System.Collections.Concurrent;

namespace MicroMobility.ParkingPhoto.Api.Geo;

public interface IZoneRepository
{
    IReadOnlyCollection<Zone> GetAll();

    /// <summary>Zones whose padded bounding box contains the point (cheap pre-filter).</summary>
    IReadOnlyCollection<Zone> QueryNear(GeoPoint point, double radiusMeters);

    void Upsert(Zone zone);

    bool Remove(string zoneId);
}

public sealed class InMemoryZoneRepository : IZoneRepository
{
    private readonly ConcurrentDictionary<string, Zone> _zones = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryZoneRepository(IEnumerable<Zone> seed)
    {
        foreach (var zone in seed)
        {
            _zones[zone.Id] = zone;
        }
    }

    public IReadOnlyCollection<Zone> GetAll() => _zones.Values.ToArray();

    public IReadOnlyCollection<Zone> QueryNear(GeoPoint point, double radiusMeters)
    {
        var result = new List<Zone>();

        foreach (var zone in _zones.Values)
        {
            var padding = radiusMeters + zone.BufferMeters;
            var (minLat, minLon, maxLat, maxLon) = GeoMath.BoundingBox(zone.Polygon, padding);

            if (point.Latitude >= minLat && point.Latitude <= maxLat &&
                point.Longitude >= minLon && point.Longitude <= maxLon)
            {
                result.Add(zone);
            }
        }

        return result;
    }

    public void Upsert(Zone zone) => _zones[zone.Id] = zone;

    public bool Remove(string zoneId) => _zones.TryRemove(zoneId, out _);
}
