using MicroMobility.ParkingPhoto.Api.Geo;

namespace MicroMobility.ParkingPhoto.Tests;

public class GeoMathTests
{
    [Fact]
    public void DistanceMeters_matches_known_distance()
    {
        var a = new GeoPoint(40.9900, 29.0280);
        var b = new GeoPoint(40.9910, 29.0280);

        var distance = GeoMath.DistanceMeters(a, b);

        Assert.InRange(distance, 110, 113);
    }

    [Fact]
    public void IsInsidePolygon_detects_centre_point()
    {
        var polygon = SeedZones.Rectangle(40.99, 29.03, 20, 20);

        Assert.True(GeoMath.IsInsidePolygon(new GeoPoint(40.99, 29.03), polygon));
        Assert.False(GeoMath.IsInsidePolygon(new GeoPoint(40.995, 29.03), polygon));
    }

    [Fact]
    public void DistanceToPolygon_is_zero_inside_and_positive_outside()
    {
        var polygon = SeedZones.Rectangle(40.99, 29.03, 20, 20);

        Assert.Equal(0, GeoMath.DistanceToPolygonMeters(new GeoPoint(40.99, 29.03), polygon));

        // 10 m north of the centre of a 20 m tall rectangle is right on the edge; 20 m is ~10 m out.
        var outside = GeoMath.DistanceToPolygonMeters(new GeoPoint(40.99 + 20 / 111_320d, 29.03), polygon);
        Assert.InRange(outside, 9, 11);
    }
}
