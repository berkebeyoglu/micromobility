using System.Text;

namespace MicroMobility.ParkingPhoto.Api.Services;

/// <summary>
/// Compares OCR output with the registered plate. Formatting differences ("34 ABC 123" vs
/// "34abc123") are irrelevant, and a single character OCR slip is tolerated instead of accusing the
/// user of photographing the wrong vehicle.
/// </summary>
public static class PlateNormalizer
{
    public const int OcrToleranceDistance = 1;

    public static string Normalize(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(plate.Length);
        foreach (var c in plate.ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    public static bool Matches(string? expected, string? detected)
    {
        var a = Normalize(expected);
        var b = Normalize(detected);

        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        return a == b || LevenshteinDistance(a, b) <= OcrToleranceDistance;
    }

    public static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
