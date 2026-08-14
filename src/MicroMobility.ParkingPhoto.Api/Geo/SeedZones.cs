namespace MicroMobility.ParkingPhoto.Api.Geo;

/// <summary>
/// Demo geofence data for a few blocks in Kadıköy / İstanbul. In production these polygons come
/// from the city's open data (sidewalks, crossings, stops) and the operator's own no-parking layer;
/// the repository interface stays the same.
/// </summary>
public static class SeedZones
{
    public static IReadOnlyList<Zone> Create() =>
    [
        new Zone
        {
            Id = "service-area-kadikoy",
            Name = "Kadıköy hizmet alanı",
            Kind = ZoneKind.ServiceArea,
            Polygon = Rectangle(40.9900, 29.0280, 2200, 2200)
        },
        new Zone
        {
            Id = "sidewalk-bahariye",
            Name = "Bahariye Caddesi yaya yolu",
            Kind = ZoneKind.Sidewalk,
            BufferMeters = 1.0,
            Polygon = Rectangle(40.9905, 29.0270, 120, 6)
        },
        new Zone
        {
            Id = "crosswalk-bahariye-1",
            Name = "Bahariye yaya geçidi",
            Kind = ZoneKind.Crosswalk,
            BufferMeters = 3.0,
            Polygon = Rectangle(40.9908, 29.0262, 5, 14)
        },
        new Zone
        {
            Id = "ramp-altiyol",
            Name = "Altıyol engelli rampası",
            Kind = ZoneKind.AccessibleRoute,
            BufferMeters = 2.0,
            Polygon = Rectangle(40.9896, 29.0291, 6, 4)
        },
        new Zone
        {
            Id = "transit-stop-altiyol",
            Name = "Altıyol otobüs durağı",
            Kind = ZoneKind.TransitStop,
            BufferMeters = 5.0,
            Polygon = Rectangle(40.9890, 29.0300, 18, 4)
        },
        new Zone
        {
            Id = "private-residence-moda",
            Name = "Moda Apartmanı (özel mülk)",
            Kind = ZoneKind.PrivateProperty,
            BufferMeters = 3.0,
            Polygon = Rectangle(40.9880, 29.0255, 30, 25)
        },
        new Zone
        {
            Id = "no-parking-square",
            Name = "Meydan park yasağı",
            Kind = ZoneKind.NoParking,
            BufferMeters = 0,
            Polygon = Rectangle(40.9920, 29.0245, 60, 40)
        },
        new Zone
        {
            Id = "parking-bay-1",
            Name = "Bahariye park cebi",
            Kind = ZoneKind.ParkingSpot,
            Polygon = Rectangle(40.9903, 29.0281, 10, 4)
        },
        new Zone
        {
            Id = "parking-bay-2",
            Name = "Altıyol park cebi",
            Kind = ZoneKind.ParkingSpot,
            Polygon = Rectangle(40.9893, 29.0310, 12, 4)
        }
    ];

    /// <summary>Axis aligned rectangle around a centre point, sized in metres.</summary>
    public static IReadOnlyList<GeoPoint> Rectangle(
        double centerLat,
        double centerLon,
        double widthMeters,
        double heightMeters)
    {
        var latDelta = heightMeters / 2 / 111_320d;
        var lonDelta = widthMeters / 2 / (111_320d * Math.Cos(centerLat * Math.PI / 180d));

        return
        [
            new GeoPoint(centerLat - latDelta, centerLon - lonDelta),
            new GeoPoint(centerLat - latDelta, centerLon + lonDelta),
            new GeoPoint(centerLat + latDelta, centerLon + lonDelta),
            new GeoPoint(centerLat + latDelta, centerLon - lonDelta)
        ];
    }
}
