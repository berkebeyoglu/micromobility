using System.Collections.Concurrent;
using MicroMobility.ParkingPhoto.Api.Domain;

namespace MicroMobility.ParkingPhoto.Api.Services;

public interface IVehicleRepository
{
    Vehicle? Find(string vehicleId);

    IReadOnlyCollection<Vehicle> GetAll();
}

public interface IRideRepository
{
    Ride? Find(string rideId);

    IReadOnlyCollection<Ride> GetAll();

    Ride Add(Ride ride);
}

public sealed class InMemoryVehicleRepository : IVehicleRepository
{
    private readonly ConcurrentDictionary<string, Vehicle> _vehicles = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryVehicleRepository()
    {
        foreach (var vehicle in Seed())
        {
            _vehicles[vehicle.Id] = vehicle;
        }
    }

    public Vehicle? Find(string vehicleId) =>
        !string.IsNullOrWhiteSpace(vehicleId) && _vehicles.TryGetValue(vehicleId, out var vehicle) ? vehicle : null;

    public IReadOnlyCollection<Vehicle> GetAll() => _vehicles.Values.ToArray();

    private static IEnumerable<Vehicle> Seed() =>
    [
        new Vehicle { Id = "SCOOTER-1001", Kind = VehicleKind.Scooter },
        new Vehicle { Id = "BIKE-2001", Kind = VehicleKind.Bicycle },
        // Mopeds carry a registration plate, so the plate check applies to them.
        new Vehicle { Id = "MOPED-3001", Kind = VehicleKind.Moped, PlateNumber = "34 ABC 123" },
        new Vehicle { Id = "MOPED-3002", Kind = VehicleKind.Moped, PlateNumber = "34 XYZ 987" }
    ];
}

public sealed class InMemoryRideRepository : IRideRepository
{
    private readonly ConcurrentDictionary<string, Ride> _rides = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryRideRepository()
    {
        foreach (var ride in Seed())
        {
            _rides[ride.Id] = ride;
        }
    }

    public Ride? Find(string rideId) =>
        !string.IsNullOrWhiteSpace(rideId) && _rides.TryGetValue(rideId, out var ride) ? ride : null;

    public IReadOnlyCollection<Ride> GetAll() => _rides.Values.ToArray();

    public Ride Add(Ride ride)
    {
        _rides[ride.Id] = ride;
        return ride;
    }

    private static IEnumerable<Ride> Seed() =>
    [
        new Ride
        {
            Id = "ride-demo-scooter",
            UserId = "user-1",
            VehicleId = "SCOOTER-1001",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-12)
        },
        new Ride
        {
            Id = "ride-demo-moped",
            UserId = "user-1",
            VehicleId = "MOPED-3001",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-25)
        }
    ];
}
