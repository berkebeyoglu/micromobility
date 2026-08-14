<#
.SYNOPSIS
    End to end smoke test for the parking photo validation API.

.DESCRIPTION
    Walks through the three interesting outcomes: a gallery photo on a crosswalk (rejected before
    any vision work), a live camera photo with several problems at once (all reported together),
    and a clean photo that unlocks the end of the ride.

.EXAMPLE
    pwsh ./scripts/smoke-test.ps1 -BaseUrl http://localhost:5080
#>
param(
    [string]$BaseUrl = "http://localhost:5080"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = New-Object Text.UTF8Encoding $false

$photoPath = Join-Path $env:TEMP "parking-photo-smoke.jpg"
[IO.File]::WriteAllBytes($photoPath, (1..2048 | ForEach-Object { [byte]($_ % 251) }))

# Windows PowerShell mangles quotes in inline arguments to native executables, so every JSON
# payload is handed to curl through a file instead. BOM-less UTF-8 keeps the server side happy.
function Write-JsonFile($Value, [string]$Path) {
    $json = $Value | ConvertTo-Json -Depth 6 -Compress
    [IO.File]::WriteAllText($Path, $json, (New-Object Text.UTF8Encoding $false))
    return $Path
}

function New-CaptureSession([string]$RideId) {
    $bodyPath = Write-JsonFile @{ deviceId = "device-1"; platform = "ios"; appVersion = "1.0.0" } `
        (Join-Path $env:TEMP "parking-session-request.json")

    $json = curl.exe -s -X POST "$BaseUrl/api/v1/rides/$RideId/parking-photo/session" `
        -H "Content-Type: application/json" -H "Accept-Language: tr" --data "@$bodyPath"

    $session = $json | ConvertFrom-Json
    if (-not $session.sessionId) {
        throw "Kamera oturumu alınamadı: $json"
    }

    return $session
}

function Invoke-Validation {
    param(
        [string]$RideId,
        [hashtable]$Metadata
    )

    $metadataPath = Write-JsonFile $Metadata (Join-Path $env:TEMP "parking-metadata.json")

    $json = curl.exe -s -X POST "$BaseUrl/api/v1/rides/$RideId/parking-photo/validate" `
        -H "Accept-Language: tr" `
        -F "photo=@$photoPath;type=image/jpeg" `
        -F "metadata=<$metadataPath"

    return $json | ConvertFrom-Json
}

function New-Metadata {
    param(
        $Session,
        [string]$Source = "Camera",
        [double]$Lat = 40.9903,
        [double]$Lng = 29.0281,
        [double]$Accuracy = 4,
        [double]$Tilt = 3,
        [bool]$PlateDetected = $true,
        [string]$PlateText = $null,
        [double]$PlateConfidence = 0.95
    )

    return @{
        captureSessionId = $Session.sessionId
        captureToken     = $Session.captureToken
        source           = $Source
        capturedAt       = (Get-Date).ToUniversalTime().ToString("o")
        device           = @{
            deviceId          = "device-1"
            platform          = "ios"
            manufacturer      = "Apple"
            model             = "iPhone 15"
            appVersion        = "1.0.0"
            liveCameraCapture = ($Source -eq "Camera")
        }
        gps              = @{
            latitude       = $Lat
            longitude      = $Lng
            accuracyMeters = $Accuracy
            capturedAt     = (Get-Date).ToUniversalTime().ToString("o")
            isMocked       = $false
        }
        visionHints      = @{
            vehicleDetected    = $true
            vehicleTiltDegrees = $Tilt
            plateDetected      = $PlateDetected
            plateText          = $PlateText
            plateConfidence    = $PlateConfidence
        }
    }
}

function Show-Result([string]$Scenario, $Result) {
    Write-Host ""
    Write-Host "=== $Scenario ===" -ForegroundColor Cyan
    Write-Host ("Karar: {0} (sürüş bitirilebilir mi: {1})" -f $Result.decision, $Result.canEndRide)

    foreach ($issue in $Result.issues) {
        Write-Host (" - [{0}] {1}" -f $issue.code, $issue.message) -ForegroundColor Yellow
    }

    if ($Result.warning) {
        Write-Host "--- kullanıcıya gösterilen uyarı ---" -ForegroundColor DarkGray
        Write-Host $Result.warning.combinedMessage
    }
}

# 1) Gallery photo on a crosswalk: the source check and the location check both fire.
$session = New-CaptureSession "ride-demo-scooter"
$metadata = New-Metadata -Session $session -Source "Gallery" -Lat 40.9908 -Lng 29.0262
Show-Result "Galeriden seçilen fotoğraf + yaya geçidi" (Invoke-Validation -RideId "ride-demo-scooter" -Metadata $metadata)

# 2) Moped on the sidewalk, fallen over, plate not visible: three problems in one warning.
$session = New-CaptureSession "ride-demo-moped"
$metadata = New-Metadata -Session $session -Lat 40.9905 -Lng 29.0270 -Tilt 72 -PlateDetected $false
Show-Result "Yaya yolu + devrilmiş araç + görünmeyen plaka" (Invoke-Validation -RideId "ride-demo-moped" -Metadata $metadata)

# 3) Clean photo in a parking bay: accepted, and the ride can be ended.
$session = New-CaptureSession "ride-demo-scooter"
$metadata = New-Metadata -Session $session
$result = Invoke-Validation -RideId "ride-demo-scooter" -Metadata $metadata
Show-Result "Uygun park cebi" $result

if ($result.decision -eq "Accepted") {
    $end = curl.exe -s -X POST "$BaseUrl/api/v1/rides/ride-demo-scooter/end" -H "Accept-Language: tr" | ConvertFrom-Json
    Write-Host ("Sürüş: {0} — {1}" -f $end.status, $end.message) -ForegroundColor Green
}
