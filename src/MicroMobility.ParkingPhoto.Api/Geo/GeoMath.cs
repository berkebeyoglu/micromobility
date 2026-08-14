namespace MicroMobility.ParkingPhoto.Api.Geo;

/// <summary>
/// Small geodesic helpers. Distances are metres; at city scale an equirectangular projection
/// around the query point is accurate enough (sub-centimetre error for the ranges we use here)
/// and much cheaper than a full geodesic solver.
/// </summary>
public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_008.8;

    public static double DistanceMeters(GeoPoint a, GeoPoint b)
    {
        var lat1 = ToRadians(a.Latitude);
        var lat2 = ToRadians(b.Latitude);
        var dLat = lat2 - lat1;
        var dLon = ToRadians(b.Longitude - a.Longitude);

        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>Ray casting test. The polygon is treated as closed even if the last point is not repeated.</summary>
    public static bool IsInsidePolygon(GeoPoint point, IReadOnlyList<GeoPoint> polygon)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            var intersects = pi.Longitude > point.Longitude != pj.Longitude > point.Longitude &&
                             point.Latitude <
                             (pj.Latitude - pi.Latitude) * (point.Longitude - pi.Longitude) /
                             (pj.Longitude - pi.Longitude) + pi.Latitude;

            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// Distance from the point to the polygon boundary. Returns 0 when the point is inside.
    /// </summary>
    public static double DistanceToPolygonMeters(GeoPoint point, IReadOnlyList<GeoPoint> polygon)
    {
        if (polygon.Count == 0)
        {
            return double.MaxValue;
        }

        if (polygon.Count == 1)
        {
            return DistanceMeters(point, polygon[0]);
        }

        if (IsInsidePolygon(point, polygon))
        {
            return 0d;
        }

        var min = double.MaxValue;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            min = Math.Min(min, DistanceToSegmentMeters(point, polygon[j], polygon[i]));
        }

        return min;
    }

    public static double DistanceToSegmentMeters(GeoPoint point, GeoPoint segmentStart, GeoPoint segmentEnd)
    {
        // Project to a local metric plane centred on the query point.
        var latScale = 111_320d;
        var lonScale = 111_320d * Math.Cos(ToRadians(point.Latitude));

        var px = 0d;
        var py = 0d;
        var ax = (segmentStart.Longitude - point.Longitude) * lonScale;
        var ay = (segmentStart.Latitude - point.Latitude) * latScale;
        var bx = (segmentEnd.Longitude - point.Longitude) * lonScale;
        var by = (segmentEnd.Latitude - point.Latitude) * latScale;

        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < 1e-9)
        {
            return Math.Sqrt((ax - px) * (ax - px) + (ay - py) * (ay - py));
        }

        var t = Math.Clamp(((px - ax) * dx + (py - ay) * dy) / lengthSquared, 0d, 1d);
        var cx = ax + t * dx;
        var cy = ay + t * dy;

        return Math.Sqrt((cx - px) * (cx - px) + (cy - py) * (cy - py));
    }

    /// <summary>Axis aligned bounding box of a polygon, padded by <paramref name="paddingMeters"/>.</summary>
    public static (double MinLat, double MinLon, double MaxLat, double MaxLon) BoundingBox(
        IReadOnlyList<GeoPoint> polygon,
        double paddingMeters = 0)
    {
        var minLat = polygon.Min(p => p.Latitude);
        var maxLat = polygon.Max(p => p.Latitude);
        var minLon = polygon.Min(p => p.Longitude);
        var maxLon = polygon.Max(p => p.Longitude);

        if (paddingMeters > 0)
        {
            var latPad = paddingMeters / 111_320d;
            var lonPad = paddingMeters / (111_320d * Math.Cos(ToRadians((minLat + maxLat) / 2)));
            minLat -= latPad;
            maxLat += latPad;
            minLon -= lonPad;
            maxLon += lonPad;
        }

        return (minLat, minLon, maxLat, maxLon);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
