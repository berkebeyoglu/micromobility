using MicroMobility.ParkingPhoto.Api.Domain;

namespace MicroMobility.ParkingPhoto.Api.Geo;

public enum ZoneKind
{
    /// <summary>Pedestrian walkway / sidewalk that must stay clear.</summary>
    Sidewalk,

    /// <summary>Accessibility infrastructure: ramps, tactile paving, accessible crossings.</summary>
    AccessibleRoute,

    /// <summary>Pedestrian crossing.</summary>
    Crosswalk,

    /// <summary>Bus / tram / metro stop area.</summary>
    TransitStop,

    /// <summary>Private property; parking has to keep a clearance from its boundary.</summary>
    PrivateProperty,

    /// <summary>Any other explicit no-parking polygon defined by the city or the operator.</summary>
    NoParking,

    /// <summary>Operational area. Parking outside of every service area polygon is rejected.</summary>
    ServiceArea,

    /// <summary>Explicitly allowed parking area; suggested to the user as an alternative.</summary>
    ParkingSpot
}

public sealed class Zone
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required ZoneKind Kind { get; init; }

    public required IReadOnlyList<GeoPoint> Polygon { get; init; }

    /// <summary>
    /// Required clearance from the polygon boundary in metres. The vehicle is considered to be
    /// obstructing the zone when its distance to the polygon is smaller than this value.
    /// </summary>
    public double BufferMeters { get; init; }

    public IssueCode? IssueCode => Kind switch
    {
        ZoneKind.Sidewalk => Domain.IssueCode.SidewalkObstruction,
        ZoneKind.AccessibleRoute => Domain.IssueCode.AccessibilityObstruction,
        ZoneKind.Crosswalk => Domain.IssueCode.CrosswalkArea,
        ZoneKind.TransitStop => Domain.IssueCode.TransitStopArea,
        ZoneKind.PrivateProperty => Domain.IssueCode.PrivatePropertyProximity,
        ZoneKind.NoParking => Domain.IssueCode.NoParkingZone,
        _ => null
    };
}

/// <summary>A zone the vehicle currently conflicts with, together with the measured distance.</summary>
public sealed record ZoneConflict(Zone Zone, double DistanceMeters, bool IsInside);
