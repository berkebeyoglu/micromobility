namespace MicroMobility.ParkingPhoto.Api.Geo;

public readonly record struct GeoPoint(double Latitude, double Longitude)
{
    public bool IsValid =>
        Latitude is >= -90 and <= 90 &&
        Longitude is >= -180 and <= 180 &&
        !(Math.Abs(Latitude) < double.Epsilon && Math.Abs(Longitude) < double.Epsilon);

    public override string ToString() =>
        $"{Latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}," +
        $"{Longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";
}
