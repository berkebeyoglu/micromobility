using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.FileType;
using MetadataExtractor.Formats.Jpeg;
using MicroMobility.ParkingPhoto.Api.Geo;
using Directory = MetadataExtractor.Directory;

namespace MicroMobility.ParkingPhoto.Api.Services;

/// <summary>
/// The EXIF facts the parking checks rely on. <see cref="TakenAtLocal"/> is the camera's local
/// wall clock: EXIF carries no UTC offset, so it is only ever compared against the device's own
/// local timestamp, never against server time.
/// </summary>
public sealed record PhotoExifFacts(
    bool HasExif,
    string? FileType,
    string? Make,
    string? Model,
    string? Software,
    DateTime? TakenAtLocal,
    int? Width,
    int? Height,
    GeoPoint? Location)
{
    public bool HasCameraSignature => !string.IsNullOrWhiteSpace(Make) || !string.IsNullOrWhiteSpace(Model);

    /// <summary>Screenshots and re-encoded images typically arrive as PNG without any camera tag.</summary>
    public bool LooksLikeScreenshot =>
        string.Equals(FileType, "PNG", StringComparison.OrdinalIgnoreCase) && !HasCameraSignature;

    public static PhotoExifFacts Empty { get; } =
        new(false, null, null, null, null, null, null, null, null);
}

public interface IExifReader
{
    PhotoExifFacts Read(Stream photoStream);
}

public sealed class ExifReader(ILogger<ExifReader> logger) : IExifReader
{
    public PhotoExifFacts Read(Stream photoStream)
    {
        IReadOnlyList<Directory> directories;

        try
        {
            if (photoStream.CanSeek)
            {
                photoStream.Position = 0;
            }

            directories = ImageMetadataReader.ReadMetadata(photoStream);
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException)
        {
            logger.LogWarning(ex, "Photo metadata could not be read");
            return PhotoExifFacts.Empty;
        }
        finally
        {
            if (photoStream.CanSeek)
            {
                photoStream.Position = 0;
            }
        }

        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var jpeg = directories.OfType<JpegDirectory>().FirstOrDefault();
        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        var fileType = directories.OfType<FileTypeDirectory>().FirstOrDefault();

        DateTime? takenAt = null;
        if (subIfd?.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original) == true)
        {
            takenAt = original;
        }
        else if (ifd0?.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var modified) == true)
        {
            takenAt = modified;
        }

        var width = GetInt(subIfd, ExifDirectoryBase.TagExifImageWidth) ??
                    GetInt(jpeg, JpegDirectory.TagImageWidth) ??
                    GetInt(ifd0, ExifDirectoryBase.TagImageWidth);

        var height = GetInt(subIfd, ExifDirectoryBase.TagExifImageHeight) ??
                     GetInt(jpeg, JpegDirectory.TagImageHeight) ??
                     GetInt(ifd0, ExifDirectoryBase.TagImageHeight);

        GeoPoint? location = null;
        if (gps?.GetGeoLocation() is { IsZero: false } geo)
        {
            location = new GeoPoint(geo.Latitude, geo.Longitude);
        }

        return new PhotoExifFacts(
            HasExif: ifd0 is not null || subIfd is not null,
            FileType: fileType?.GetDescription(FileTypeDirectory.TagDetectedFileTypeName),
            Make: ifd0?.GetDescription(ExifDirectoryBase.TagMake)?.Trim(),
            Model: ifd0?.GetDescription(ExifDirectoryBase.TagModel)?.Trim(),
            Software: ifd0?.GetDescription(ExifDirectoryBase.TagSoftware)?.Trim(),
            TakenAtLocal: takenAt,
            Width: width,
            Height: height,
            Location: location);
    }

    private static int? GetInt(Directory? directory, int tag) =>
        directory is not null && directory.TryGetInt32(tag, out var value) && value > 0 ? value : null;
}
