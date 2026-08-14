# Park Fotoğrafı Doğrulama Servisi (.NET 8)

Paylaşımlı mikromobilite araçlarında **sürüş bitişinde çekilen park fotoğrafını** doğrulayan bir
ASP.NET Core Web API. Fotoğrafın gerçekten telefon kamerasından geldiğini, park yerinin uygun
olduğunu, plakanın okunabildiğini ve aracın dik durduğunu kontrol eder; **tüm hataları tek bir
uyarıda birleştirip** kullanıcıya döner.

```
src/MicroMobility.ParkingPhoto.Api     API, kurallar, geofence, servisler, demo arayüz
tests/MicroMobility.ParkingPhoto.Tests 49 birim testi
scripts/smoke-test.ps1                 uçtan uca senaryo denemesi
```

## Çalıştırma

```bash
dotnet run --project src/MicroMobility.ParkingPhoto.Api --urls http://localhost:5080
```

| Adres | Açıklama |
| --- | --- |
| `http://localhost:5080/` | Tarayıcıdan senaryo denemek için demo arayüz |
| `http://localhost:5080/swagger` | OpenAPI arayüzü (Development) |
| `http://localhost:5080/health` | Sağlık kontrolü |

Testler ve uçtan uca deneme:

```bash
dotnet test
powershell -File scripts/smoke-test.ps1 -BaseUrl http://localhost:5080
```

## Akış

1. **`POST /api/v1/rides/{rideId}/parking-photo/session`**
   Uygulama kamerayı açmadan hemen önce çağırır. Tek kullanımlık, kısa ömürlü bir `captureToken`
   ve kullanıcıya gösterilecek çekim talimatlarını döner.
2. **`POST /api/v1/rides/{rideId}/parking-photo/validate`** (multipart: `photo` + `metadata`)
   Bütün kontrolleri çalıştırır, `decision` ve hazır `warning` nesnesiyle döner.
3. **`POST /api/v1/rides/{rideId}/end`**
   Fotoğraf onaylanmadıysa `409` döner; yani kontrol atlanamaz.

Ek olarak **`GET /api/v1/parking/check?lat=&lng=`** aynı geofence motorunu kullanır, böylece harita
ekranı ile fotoğraf kontrolü hiçbir zaman çelişmez.

### `decision` değerleri

| Değer | Anlamı |
| --- | --- |
| `Accepted` | Sorun yok, sürüş bitirilebilir |
| `RetakeRequired` | Engelleyici hata var, fotoğraf yeniden çekilecek |
| `ManualReview` | Deneme hakkı bitti; kullanıcı mağdur olmasın diye sürüş bitirilir, kayıt destek ekibine düşer |

## Kontroller

### 1. Fotoğraf yalnızca telefon kamerasından

Dört bağımsız sinyalin hepsi kontrol edilir; biri bile tutmazsa fotoğraf reddedilir:

- **Tek kullanımlık kamera oturumu:** token sürüşe ve cihaza bağlıdır, TTL'i vardır, ikinci kez
  kullanılamaz (`CaptureSessionInvalid`, `CaptureSessionExpired`).
- **İstemcinin kaynak bildirimi:** `source = Camera` ve `liveCameraCapture = true` olmalı
  (`PhotoNotFromCamera`).
- **Dosyanın kendi EXIF imzası:** kamera üretici/model etiketi yoksa ya da ekran görüntüsü
  görünümündeyse reddedilir; galeriden seçilip "kameradan geldi" diyen istemciyi yakalayan asıl
  kontrol budur.
- **Zaman ve bütünlük:** çekim zamanı taze olmalı (`PhotoStale`), düzenleme yazılımı etiketi
  olmamalı (`PhotoEdited`), SHA-256 gönderildiyse dosyayla eşleşmeli (`PhotoIntegrityMismatch`),
  çözünürlük yeterli olmalı (`PhotoResolutionTooLow`).

### 2. GPS ile park uygunluğu

Konum, poligon katmanlarına metre hassasiyetinde mesafe hesabıyla değerlendirilir. Her katmanın
kendi güvenlik payı (`BufferMeters`) vardır ve GPS hatası bu mesafeye eklenir; yani "sadece sinyal
kötü olduğu için temiz görünen" bir nokta da yakalanır.

| Katman | Hata kodu | Varsayılan pay |
| --- | --- | --- |
| Yaya yolu | `SidewalkObstruction` | 1 m |
| Engelli rampası / hissedilebilir yüzey | `AccessibilityObstruction` | 2 m |
| Yaya geçidi | `CrosswalkArea` | 3 m |
| Toplu taşıma durağı | `TransitStopArea` | 5 m |
| Özel mülk | `PrivatePropertyProximity` | 3 m |
| Park yasağı bölgesi | `NoParkingZone` | 0 m |
| Hizmet alanı dışı | `OutsideServiceArea` | — |

Ayrıca konum alınamazsa/taklit edilmişse (`GpsMissing`), hassasiyet yetersizse
(`GpsAccuracyTooLow`) ve fotoğrafın EXIF GPS etiketi cihazın bildirdiği konumdan uzaksa
(`PhotoLocationMismatch`) uyarı üretilir. Reddedilen her yanıt, kullanıcıyı yönlendirmek için en
yakın uygun park alanlarını da döner.

Demo poligonları Kadıköy çevresi için `Geo/SeedZones.cs` içinde tanımlı. Gerçek kurulumda
`IZoneRepository` şehrin açık verisine veya operatörün kendi katmanına bağlanır; kural kodu değişmez.

### 3. Plaka okunabilirliği

Yalnızca plakası olan araçlar (`Vehicle.PlateNumber` dolu, örn. mopedler) için çalışır:
plaka görünmüyorsa `PlateNotVisible`, net okunmuyorsa `PlateNotReadable`, başka bir araca aitse
`PlateMismatch`. Karşılaştırma biçim farklarını yok sayar ("34 ABC 123" = "34abc123") ve tek
karakterlik OCR hatasını tolere eder, böylece kullanıcı haksız yere "yanlış araç" uyarısı almaz.

### 4. Aracın dik duruşu

Eğim önce aracın kendi IMU telemetrisinden (taze ölçüm varsa), yoksa fotoğraf analizinden alınır.
`MaxTiltDegrees` (20°) üstü `VehicleNotUpright`, `FallenTiltDegrees` (55°) üstü `VehicleFallen`
üretir. Fotoğrafta araç bulunamazsa `VehicleNotDetected`.

## Birden fazla hata tek uyarıda

Kurallar ilk hatada durmaz. Yanıttaki `warning` nesnesi tüm sorunları birlikte verir:

```json
{
  "decision": "RetakeRequired",
  "canEndRide": false,
  "warning": {
    "title": "Düzeltilmesi gereken 3 konu var",
    "headline": "Sürüşü bitirebilmek için aşağıdaki konuların hepsini düzeltip fotoğrafı yeniden çekin.",
    "lines": [
      "1. Yaya yolu işgal ediliyor: Aracınızın konumu park etmeye uygun değil, lütfen uygun alana geçiniz. Araç yaya yolunu işgal ediyor.",
      "2. Araç devrilmiş: Araç yere devrilmiş durumda. Lütfen aracınızı dik konuma getirin.",
      "3. Plaka görünmüyor: Aracın plakası fotoğrafta görünmüyor."
    ],
    "actions": [
      "Aracı yaya geçişini kapatmayacak şekilde park cebine veya bisiklet park alanına bırakın.",
      "Aracı kaldırıp ayaklığın üzerine dik bırakın, sonra fotoğrafı tekrar çekin.",
      "Plakayı kadraja alacak şekilde fotoğrafı yeniden çekin."
    ],
    "combinedMessage": "…tek bir metin alanına basılabilecek tam hâli…",
    "primaryButton": "Fotoğrafı yeniden çek",
    "secondaryButton": "Uygun park alanlarını göster",
    "blocking": true
  }
}
```

Uyarı ekranı bloke eden bir diyalog değil: kullanıcı okuyup kapatır ve fotoğrafı baştan çeker.
Metinler `Localization/IssueCatalog.cs` içinde tek yerde tutulur; `Accept-Language` başlığına göre
Türkçe (varsayılan) veya İngilizce döner.

Fotoğraf güvenilmez bulunduğunda plaka ve duruş kontrolleri atlanır (reddedilmiş bir görüntü
üzerinden model çalıştırmak yanlış uyarı üretir), fakat konum kontrolü cihaz GPS'inden bağımsız
çalıştığı için yine raporlanır.

## Yapılandırma

`appsettings.json` üzerinden ayarlanır (Development profilinde eşikler gevşetilmiştir):

| Bölüm | Öne çıkan ayarlar |
| --- | --- |
| `CaptureSession` | `TtlSeconds`, `MaxPhotoAgeSeconds`, `ClockSkewSeconds`, `SingleUse` |
| `PhotoValidation` | `MinWidth/MinHeight`, `RequireCameraExif`, `MaxGpsAccuracyMeters`, `MaxTiltDegrees`, `FallenTiltDegrees`, `MinPlateConfidence`, `MaxRetakeAttempts` |
| `Geofence` | `QueryRadiusMeters`, `EnforceServiceArea`, `MaxParkingSuggestions` |
| `Vision` | `Endpoint`, `ApiKey`, `TimeoutSeconds`, `AllowClientHints` |
| `PhotoStorage` | `RootPath`, `Enabled` |

## Görüntü analizi bağlantısı

Plaka ve duruş tahmini `IVisionAnalyzer` arkasında:

- `Vision:Endpoint` doluysa `HttpVisionAnalyzer` model servisine multipart istek atar.
- Boşsa `SimulatedVisionAnalyzer` devreye girer; sonuçları görüntünün özetinden türettiği için
  aynı fotoğraf her zaman aynı sonucu verir.
- `Vision:AllowClientHints` yalnızca geliştirme/test içindir: istemci `visionHints` ile sonuçları
  sabitleyebilir. Üretimde kapalı olmalıdır, aksi hâlde uygulama kendi kararını dikte eder.

Model servisi cevap vermezse istek başarısız olmaz: görüntüye dayalı kurallar atlanır, diğer
kontroller kararı verir ve hata loglanır.

## Notlar

- Sürüş/araç/oturum verisi bellekte tutulur; kalıcılık için `IRideRepository`, `IVehicleRepository`,
  `IZoneRepository` ve `ICaptureSessionService` arayüzlerini gerçek altyapıya bağlamak yeterli.
- Gönderilen fotoğraflar itiraz yönetimi için `PhotoStorage:RootPath` altına yazılır; kaydetme
  hatası park kararını etkilemez.
- Kimlik doğrulama bu serviste yok; API'nin kullanıcı oturumu doğrulayan bir ağ geçidi arkasında
  çalıştığı varsayılmıştır.
