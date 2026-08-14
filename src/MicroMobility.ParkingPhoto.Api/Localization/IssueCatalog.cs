using MicroMobility.ParkingPhoto.Api.Domain;

namespace MicroMobility.ParkingPhoto.Api.Localization;

public sealed record IssueText(string Title, string Message, string? Action);

public sealed record IssueTemplate(
    IssueSeverity Severity,
    int DisplayOrder,
    IssueText Tr,
    IssueText En);

/// <summary>
/// Single source of truth for the user facing wording of every <see cref="IssueCode"/>.
/// Turkish is the default language, English is used as a fallback for other locales.
/// </summary>
public static class IssueCatalog
{
    public const string DefaultLanguage = "tr";

    private static readonly Dictionary<IssueCode, IssueTemplate> Templates = new()
    {
        // ---------------------------------------------------------------- source
        [IssueCode.PhotoNotFromCamera] = new(
            IssueSeverity.Blocking, 5,
            new("Fotoğraf kameradan çekilmedi",
                "Sürüş bitirme fotoğrafı yalnızca telefon kamerasıyla, o anda çekilebilir. Galeriden veya başka bir uygulamadan seçilen görseller kabul edilmez.",
                "Uygulama içindeki kamerayı kullanarak aracın fotoğrafını yeniden çekin."),
            new("Photo was not taken with the camera",
                "The end-of-ride photo can only be captured live with the phone camera. Images picked from the gallery or other apps are not accepted.",
                "Retake the photo using the in-app camera.")),

        [IssueCode.CaptureSessionInvalid] = new(
            IssueSeverity.Blocking, 4,
            new("Kamera oturumu geçersiz",
                "Fotoğraf, uygulamanın başlattığı kamera oturumuna ait değil.",
                "Sürüşü bitir ekranından kamerayı yeniden açıp fotoğrafı tekrar çekin."),
            new("Capture session is invalid",
                "The photo does not belong to a capture session started by the app.",
                "Open the camera again from the end-ride screen and retake the photo.")),

        [IssueCode.CaptureSessionExpired] = new(
            IssueSeverity.Blocking, 4,
            new("Kamera oturumunun süresi doldu",
                "Fotoğraf çekim süresi doldu, bu yüzden fotoğraf doğrulanamıyor.",
                "Kamerayı yeniden açıp fotoğrafı tekrar çekin."),
            new("Capture session expired",
                "The capture window has expired, so the photo cannot be verified.",
                "Open the camera again and retake the photo.")),

        [IssueCode.PhotoEdited] = new(
            IssueSeverity.Blocking, 6,
            new("Fotoğraf düzenlenmiş",
                "Fotoğrafın üzerinde düzenleme yapıldığı tespit edildi. Düzenlenmiş fotoğraflar kabul edilmiyor.",
                "Aracın düzenlenmemiş, doğrudan kameradan çekilmiş fotoğrafını gönderin."),
            new("Photo has been edited",
                "The photo appears to have been edited. Edited photos are not accepted.",
                "Send an unedited photo taken directly with the camera.")),

        [IssueCode.PhotoStale] = new(
            IssueSeverity.Blocking, 6,
            new("Fotoğraf güncel değil",
                "Fotoğraf sürüşün bitirildiği anda çekilmemiş.",
                "Aracın şu anki halinin fotoğrafını çekin."),
            new("Photo is not current",
                "The photo was not taken at the moment the ride was ended.",
                "Take a photo of the vehicle as it is right now.")),

        [IssueCode.PhotoIntegrityMismatch] = new(
            IssueSeverity.Blocking, 6,
            new("Fotoğraf doğrulanamadı",
                "Gönderilen dosya, kameranın ürettiği görüntüyle eşleşmiyor.",
                "Fotoğrafı uygulama içindeki kameradan yeniden çekin."),
            new("Photo could not be verified",
                "The uploaded file does not match the image produced by the camera.",
                "Retake the photo with the in-app camera.")),

        [IssueCode.PhotoResolutionTooLow] = new(
            IssueSeverity.Blocking, 7,
            new("Fotoğraf çözünürlüğü düşük",
                "Fotoğraf, kontrollerin yapılabilmesi için fazla düşük çözünürlüklü.",
                "Aracın tamamı görünecek şekilde net bir fotoğraf çekin."),
            new("Photo resolution is too low",
                "The photo resolution is too low for the checks to run.",
                "Take a sharp photo where the whole vehicle is visible.")),

        // -------------------------------------------------------------- location
        [IssueCode.GpsMissing] = new(
            IssueSeverity.Blocking, 10,
            new("Konum bilgisi yok",
                "Fotoğrafın çekildiği konum alınamadı, park yeri kontrol edilemiyor.",
                "Konum iznini açıp açık bir alanda fotoğrafı tekrar çekin."),
            new("Location is missing",
                "The capture location could not be read, so the parking spot cannot be checked.",
                "Enable location permission and retake the photo in an open area.")),

        [IssueCode.GpsAccuracyTooLow] = new(
            IssueSeverity.Blocking, 11,
            new("Konum hassasiyeti yetersiz",
                "Konum yeterince hassas değil, park alanının uygunluğu doğrulanamıyor.",
                "Birkaç saniye bekleyip açık bir alanda fotoğrafı tekrar çekin."),
            new("Location accuracy is insufficient",
                "The location is not accurate enough to verify the parking spot.",
                "Wait a few seconds and retake the photo in an open area.")),

        [IssueCode.PhotoLocationMismatch] = new(
            IssueSeverity.Blocking, 12,
            new("Fotoğraf ile konum uyuşmuyor",
                "Fotoğrafın çekildiği yer ile aracın bulunduğu konum birbirinden uzak.",
                "Aracın yanında durup fotoğrafı yeniden çekin."),
            new("Photo and location do not match",
                "The place where the photo was taken is far from the vehicle location.",
                "Stand next to the vehicle and retake the photo.")),

        [IssueCode.SidewalkObstruction] = new(
            IssueSeverity.Blocking, 20,
            new("Yaya yolu işgal ediliyor",
                "Aracınızın konumu park etmeye uygun değil, lütfen uygun alana geçiniz. Araç yaya yolunu işgal ediyor.",
                "Aracı yaya geçişini kapatmayacak şekilde park cebine veya bisiklet park alanına bırakın."),
            new("Pedestrian walkway is blocked",
                "Your vehicle's location is not suitable for parking, please move to a suitable area. The vehicle blocks the pedestrian walkway.",
                "Move the vehicle to a parking bay or bike rack without blocking pedestrians.")),

        [IssueCode.AccessibilityObstruction] = new(
            IssueSeverity.Blocking, 21,
            new("Engelli erişimi engelleniyor",
                "Aracınızın konumu park etmeye uygun değil, lütfen uygun alana geçiniz. Araç engelli rampasını / hissedilebilir yüzeyi kapatıyor.",
                "Rampa, kılavuz yüzey ve engelli geçişlerinden en az 2 metre uzaklaşın."),
            new("Accessibility route is blocked",
                "Your vehicle's location is not suitable for parking, please move to a suitable area. The vehicle blocks an accessibility ramp or tactile paving.",
                "Move at least 2 metres away from ramps, tactile paving and accessible crossings.")),

        [IssueCode.CrosswalkArea] = new(
            IssueSeverity.Blocking, 22,
            new("Yaya geçidine park edilmiş",
                "Aracınızın konumu park etmeye uygun değil, lütfen uygun alana geçiniz. Araç yaya geçidinin üzerinde veya çok yakınında.",
                "Yaya geçidinden uzaklaşıp park için ayrılmış bir alana bırakın."),
            new("Parked on a crosswalk",
                "Your vehicle's location is not suitable for parking, please move to a suitable area. The vehicle is on or too close to a crosswalk.",
                "Move away from the crosswalk and park in a designated area.")),

        [IssueCode.TransitStopArea] = new(
            IssueSeverity.Blocking, 23,
            new("Toplu taşıma durağı işgal ediliyor",
                "Aracınızın konumu park etmeye uygun değil, lütfen uygun alana geçiniz. Araç toplu taşıma durağını işgal ediyor.",
                "Durak alanından en az 5 metre uzaklaşın."),
            new("Transit stop is blocked",
                "Your vehicle's location is not suitable for parking, please move to a suitable area. The vehicle blocks a public transport stop.",
                "Move at least 5 metres away from the stop area.")),

        [IssueCode.PrivatePropertyProximity] = new(
            IssueSeverity.Blocking, 24,
            new("Özel mülke çok yakın",
                "Aracınızın konumu park etmeye uygun değil, lütfen uygun alana geçiniz. Araç özel mülkün içinde veya girişini kapatacak kadar yakın.",
                "Özel mülk sınırından uzaklaşıp kamuya açık bir park alanına bırakın."),
            new("Too close to private property",
                "Your vehicle's location is not suitable for parking, please move to a suitable area. The vehicle is inside or too close to private property.",
                "Move away from the property boundary and park in a public area.")),

        [IssueCode.NoParkingZone] = new(
            IssueSeverity.Blocking, 25,
            new("Park yasağı bölgesi",
                "Aracınızın konumu park etmeye uygun değil, lütfen uygun alana geçiniz.",
                "Haritadaki en yakın uygun park alanına geçin."),
            new("No-parking zone",
                "Your vehicle's location is not suitable for parking, please move to a suitable area.",
                "Move to the nearest suitable parking area shown on the map.")),

        [IssueCode.OutsideServiceArea] = new(
            IssueSeverity.Blocking, 26,
            new("Hizmet alanı dışında",
                "Aracınızın konumu park etmeye uygun değil, lütfen uygun alana geçiniz. Bulunduğunuz yer hizmet alanının dışında.",
                "Hizmet alanına dönüp sürüşü orada bitirin."),
            new("Outside the service area",
                "Your vehicle's location is not suitable for parking, please move to a suitable area. You are outside the service area.",
                "Return to the service area and end the ride there.")),

        // ----------------------------------------------------------------- plate
        [IssueCode.PlateNotVisible] = new(
            IssueSeverity.Blocking, 40,
            new("Plaka görünmüyor",
                "Aracın plakası fotoğrafta görünmüyor.",
                "Plakayı kadraja alacak şekilde fotoğrafı yeniden çekin."),
            new("Plate is not visible",
                "The vehicle plate is not visible in the photo.",
                "Retake the photo with the plate inside the frame.")),

        [IssueCode.PlateNotReadable] = new(
            IssueSeverity.Blocking, 41,
            new("Plaka net okunmuyor",
                "Aracın plakası fotoğrafta net okunmuyor.",
                "Plakaya biraz yaklaşıp net bir şekilde tekrar çekin."),
            new("Plate is not readable",
                "The vehicle plate cannot be read clearly in the photo.",
                "Get a bit closer to the plate and retake a sharp photo.")),

        [IssueCode.PlateMismatch] = new(
            IssueSeverity.Blocking, 42,
            new("Plaka eşleşmiyor",
                "Fotoğraftaki plaka, kiraladığınız araca ait değil.",
                "Kiraladığınız aracın fotoğrafını çektiğinizden emin olun."),
            new("Plate does not match",
                "The plate in the photo does not belong to the vehicle you rented.",
                "Make sure you are photographing the vehicle you rented.")),

        // --------------------------------------------------------------- posture
        [IssueCode.VehicleNotDetected] = new(
            IssueSeverity.Blocking, 30,
            new("Araç fotoğrafta görünmüyor",
                "Fotoğrafta araç tespit edilemedi.",
                "Aracın tamamı görünecek şekilde 2-3 metre mesafeden fotoğraf çekin."),
            new("Vehicle is not visible",
                "No vehicle could be detected in the photo.",
                "Take the photo from 2-3 metres away so the whole vehicle is visible.")),

        [IssueCode.VehicleNotUpright] = new(
            IssueSeverity.Blocking, 31,
            new("Araç dik değil",
                "Araç dik konumda park edilmemiş. Lütfen aracınızı dik konuma getirin.",
                "Ayaklığı açıp aracı dengeli ve dik bırakın, sonra fotoğrafı tekrar çekin."),
            new("Vehicle is not upright",
                "The vehicle is not parked upright. Please put your vehicle in an upright position.",
                "Use the kickstand, leave the vehicle stable and upright, then retake the photo.")),

        [IssueCode.VehicleFallen] = new(
            IssueSeverity.Blocking, 32,
            new("Araç devrilmiş",
                "Araç yere devrilmiş durumda. Lütfen aracınızı dik konuma getirin.",
                "Aracı kaldırıp ayaklığın üzerine dik bırakın, sonra fotoğrafı tekrar çekin."),
            new("Vehicle has fallen over",
                "The vehicle is lying on the ground. Please put your vehicle in an upright position.",
                "Lift the vehicle onto its kickstand and retake the photo."))
    };

    public static IssueTemplate GetTemplate(IssueCode code) => Templates[code];

    public static ValidationIssue Create(
        IssueCode code,
        string language = DefaultLanguage,
        IReadOnlyDictionary<string, string>? details = null,
        IssueSeverity? severityOverride = null)
    {
        var template = Templates[code];
        var text = IsTurkish(language) ? template.Tr : template.En;

        return new ValidationIssue
        {
            Code = code,
            Severity = severityOverride ?? template.Severity,
            Title = text.Title,
            Message = text.Message,
            Action = text.Action,
            DisplayOrder = template.DisplayOrder,
            Details = details ?? new Dictionary<string, string>()
        };
    }

    public static bool IsTurkish(string? language) =>
        string.IsNullOrWhiteSpace(language) ||
        language.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
}
