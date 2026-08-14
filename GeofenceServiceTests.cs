using MicroMobility.ParkingPhoto.Api.Geo;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Tests;

public class GeofenceServiceTests
{
    private static GeofenceService CreateService() =>
        new(new InMemoryZoneRepository(SeedZones.Create()), Options.Create(new GeofenceOptions()));

    [Fact]
    public void Parking_bay_is_suitable()
    {
        var assessment = CreateService().Assess(new GeoPoint(TestData.SuitableLat, TestData.SuitableLng), 5);

        Assert.True(assessment.IsSuitable);
        Assert.Empty(assessment.Conflicts);
        Assert.True(assessment.InsideServiceArea);
    }

    [Theory]
    [InlineData(TestData.SidewalkLat, TestData.SidewalkLng, ZoneKind.Sidewalk)]
    [InlineData(TestData.CrosswalkLat, TestData.CrosswalkLng, ZoneKind.Crosswalk)]
    [InlineData(TestData.TransitStopLat, TestData.TransitStopLng, ZoneKind.TransitStop)]
    [InlineData(40.9896, 29.0291, ZoneKind.AccessibleRoute)]
    [InlineData(40.9880, 29.0255, ZoneKind.PrivateProperty)]
    [InlineData(40.9920, 29.0245, ZoneKind.NoParking)]
    public void Restricted_areas_are_reported(double lat, double lng, ZoneKind expected)
    {
        var assessment = CreateService().Assess(new GeoPoint(lat, lng), 3);

        Assert.False(assessment.IsSuitable);
        Assert.Contains(assessment.Conflicts, c => c.Zone.Kind == expected);
    }

    [Fact]
    public void Point_outside_every_service_area_is_flagged()
    {
        var assessment = CreateService().Assess(new GeoPoint(41.0200, 29.0800), 5);

        Assert.True(assessment.ServiceAreaConfigured);
        Assert.False(assessment.InsideServiceArea);
        Assert.False(assessment.IsSuitable);
    }

    [Fact]
    public void Poor_gps_accuracy_widens_the_conflict_check()
    {
        var service = CreateService();
        // ~12 m north of the bus stop: clear with a sharp fix, conflicting once the error is added.
        var point = new GeoPoint(TestData.TransitStopLat + 12 / 111_320d, TestData.TransitStopLng);

        Assert.Empty(service.Assess(point, 1).Conflicts);
        Assert.Contains(service.Assess(point, 10).Conflicts, c => c.Zone.Kind == ZoneKind.TransitStop);
    }

    [Fact]
    public void Suggestions_are_ordered_by_distance()
    {
        var assessment = CreateService().Assess(new GeoPoint(TestData.SidewalkLat, TestData.SidewalkLng), 5);

        Assert.NotEmpty(assessment.Suggestions);
        Assert.Equal(
            assessment.Suggestions.OrderBy(s => s.DistanceMeters).Select(s => s.ZoneId),
            assessment.Suggestions.Select(s => s.ZoneId));
    }
}
