using System.Text;
using MicroMobility.ParkingPhoto.Api.Contracts;
using MicroMobility.ParkingPhoto.Api.Domain;
using MicroMobility.ParkingPhoto.Api.Localization;

namespace MicroMobility.ParkingPhoto.Api.Validation;

/// <summary>
/// Turns every detected issue into one warning sheet. All problems are listed together, numbered
/// when there is more than one, so the user knows exactly what to fix before the next photo instead
/// of getting one popup per attempt.
/// </summary>
public static class WarningBuilder
{
    private static readonly IssueCode[] LocationCodes =
    [
        IssueCode.SidewalkObstruction,
        IssueCode.AccessibilityObstruction,
        IssueCode.CrosswalkArea,
        IssueCode.TransitStopArea,
        IssueCode.PrivatePropertyProximity,
        IssueCode.NoParkingZone,
        IssueCode.OutsideServiceArea
    ];

    public static WarningDto? Build(
        IReadOnlyList<ValidationIssue> issues,
        ParkingDecision decision,
        int remainingAttempts,
        string language)
    {
        if (issues.Count == 0)
        {
            return null;
        }

        var tr = IssueCatalog.IsTurkish(language);
        var ordered = issues
            .OrderBy(i => i.Severity == IssueSeverity.Blocking ? 0 : 1)
            .ThenBy(i => i.DisplayOrder)
            .ToArray();

        var lines = ordered
            .Select((issue, index) => ordered.Length == 1
                ? issue.Message
                : $"{index + 1}. {issue.Title}: {issue.Message}")
            .ToArray();

        var actions = ordered
            .Select(i => i.Action)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var blocking = ordered.Any(i => i.Severity == IssueSeverity.Blocking);
        var hasLocationIssue = ordered.Any(i => LocationCodes.Contains(i.Code));

        var title = decision switch
        {
            ParkingDecision.ManualReview => tr ? "Kontrol gerekiyor" : "Manual check required",
            _ when ordered.Length == 1 => tr ? "Fotoğrafı tekrar çekmelisiniz" : "Please retake the photo",
            _ => tr
                ? $"Düzeltilmesi gereken {ordered.Length} konu var"
                : $"{ordered.Length} issues need to be fixed"
        };

        var headline = decision switch
        {
            ParkingDecision.ManualReview => tr
                ? "Fotoğraf birkaç denemede de doğrulanamadı. Aşağıdaki konuları düzeltip tekrar deneyebilir ya da sürüşü destek ekibinin kontrolüne bırakabilirsiniz."
                : "The photo could not be verified after several attempts. Fix the items below and try again, or hand the ride over to support.",
            _ when ordered.Length == 1 => tr
                ? "Sürüşü bitirebilmek için aşağıdaki konuyu düzeltip fotoğrafı yeniden çekin."
                : "Fix the item below and retake the photo to end your ride.",
            _ => tr
                ? "Sürüşü bitirebilmek için aşağıdaki konuların hepsini düzeltip fotoğrafı yeniden çekin."
                : "Fix all of the items below and retake the photo to end your ride."
        };

        var combined = new StringBuilder();
        combined.AppendLine(headline).AppendLine();

        foreach (var line in lines)
        {
            combined.AppendLine(line);
        }

        if (actions.Length > 0)
        {
            combined.AppendLine();
            combined.AppendLine(tr ? "Ne yapmalısınız?" : "What to do:");
            foreach (var action in actions)
            {
                combined.AppendLine($"• {action}");
            }
        }

        if (decision != ParkingDecision.ManualReview && remainingAttempts >= 0)
        {
            combined.AppendLine();
            combined.AppendLine(tr
                ? $"Kalan deneme hakkı: {remainingAttempts}"
                : $"Remaining attempts: {remainingAttempts}");
        }

        return new WarningDto
        {
            Title = title,
            Headline = headline,
            Lines = lines,
            Actions = actions,
            CombinedMessage = combined.ToString().TrimEnd(),
            PrimaryButton = tr ? "Fotoğrafı yeniden çek" : "Retake photo",
            SecondaryButton = decision == ParkingDecision.ManualReview
                ? tr ? "Destek ekibine gönder" : "Send to support"
                : hasLocationIssue
                    ? tr ? "Uygun park alanlarını göster" : "Show suitable parking areas"
                    : null,
            Blocking = blocking
        };
    }
}
