using MicroMobility.ParkingPhoto.Api.Configuration;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Api.Services;

public interface IPhotoStore
{
    Task<string?> SaveAsync(string rideId, string validationId, byte[] photo, string contentType, CancellationToken ct);
}

/// <summary>
/// Keeps the submitted photo for audit and dispute handling. A real deployment would write to
/// object storage with a retention policy; the interface is the same.
/// </summary>
public sealed class FileSystemPhotoStore(
    IOptions<PhotoStorageOptions> options,
    IHostEnvironment environment,
    ILogger<FileSystemPhotoStore> logger) : IPhotoStore
{
    private readonly PhotoStorageOptions _options = options.Value;

    public async Task<string?> SaveAsync(
        string rideId,
        string validationId,
        byte[] photo,
        string contentType,
        CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        try
        {
            var root = Path.IsPathRooted(_options.RootPath)
                ? _options.RootPath
                : Path.Combine(environment.ContentRootPath, _options.RootPath);

            var directory = Path.Combine(root, Sanitize(rideId));
            System.IO.Directory.CreateDirectory(directory);

            var extension = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
            var path = Path.Combine(directory, Sanitize(validationId) + extension);

            await File.WriteAllBytesAsync(path, photo, ct);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Storing the photo must never block the parking decision.
            logger.LogError(ex, "Parking photo could not be stored for ride {RideId}", rideId);
            return null;
        }
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
