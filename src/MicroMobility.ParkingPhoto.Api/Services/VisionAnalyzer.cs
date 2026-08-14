using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Contracts;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Api.Services;

public sealed record VisionRequest(
    byte[] Photo,
    string ContentType,
    bool PlateRequired,
    string? ExpectedPlate,
    VisionHints? Hints);

public sealed record VisionAnalysis(
    bool VehicleDetected,
    double? TiltDegrees,
    bool PlateDetected,
    string? PlateText,
    double? PlateConfidence,
    string Source);

public interface IVisionAnalyzer
{
    Task<VisionAnalysis> AnalyzeAsync(VisionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Deterministic stand-in for the plate + posture model. Results are derived from the image bytes
/// so the same photo always produces the same verdict, which keeps demos and tests reproducible.
/// </summary>
public sealed class SimulatedVisionAnalyzer : IVisionAnalyzer
{
    public Task<VisionAnalysis> AnalyzeAsync(VisionRequest request, CancellationToken cancellationToken)
    {
        var digest = SHA256.HashData(request.Photo);

        // Two independent pseudo-random draws from the digest.
        var detectionDraw = digest[0] / 255d;
        var tiltDraw = digest[1] / 255d;
        var plateDraw = digest[2] / 255d;

        var vehicleDetected = detectionDraw > 0.05;
        var tilt = Math.Round(tiltDraw * 30, 1);

        var plateDetected = request.PlateRequired && plateDraw > 0.2;
        var confidence = plateDetected ? Math.Round(0.6 + plateDraw * 0.39, 2) : (double?)null;

        return Task.FromResult(new VisionAnalysis(
            vehicleDetected,
            vehicleDetected ? tilt : null,
            plateDetected,
            plateDetected ? request.ExpectedPlate : null,
            confidence,
            "simulated"));
    }
}

/// <summary>Calls an external inference service that returns plate and posture predictions.</summary>
public sealed class HttpVisionAnalyzer(
    HttpClient httpClient,
    IOptions<VisionOptions> options,
    ILogger<HttpVisionAnalyzer> logger) : IVisionAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly VisionOptions _options = options.Value;

    public async Task<VisionAnalysis> AnalyzeAsync(VisionRequest request, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var photo = new ByteArrayContent(request.Photo);
        photo.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
        content.Add(photo, "photo", "parking.jpg");
        content.Add(new StringContent(request.ExpectedPlate ?? string.Empty), "expectedPlate");
        content.Add(new StringContent(request.PlateRequired.ToString()), "plateRequired");

        using var message = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint) { Content = content };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            message.Headers.TryAddWithoutValidation("X-Api-Key", _options.ApiKey);
        }

        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VisionResponsePayload>(JsonOptions, cancellationToken);
        if (payload is null)
        {
            logger.LogWarning("Vision service returned an empty payload");
            return new VisionAnalysis(false, null, false, null, null, "vision-service:empty");
        }

        return new VisionAnalysis(
            payload.VehicleDetected,
            payload.TiltDegrees,
            payload.PlateDetected,
            payload.PlateText,
            payload.PlateConfidence,
            $"vision-service:{payload.ModelVersion ?? "unknown"}");
    }

    private sealed record VisionResponsePayload(
        [property: JsonPropertyName("vehicleDetected")] bool VehicleDetected,
        [property: JsonPropertyName("tiltDegrees")] double? TiltDegrees,
        [property: JsonPropertyName("plateDetected")] bool PlateDetected,
        [property: JsonPropertyName("plateText")] string? PlateText,
        [property: JsonPropertyName("plateConfidence")] double? PlateConfidence,
        [property: JsonPropertyName("modelVersion")] string? ModelVersion);
}

/// <summary>
/// Lets the client pin the vision results for local development and automated tests. Disabled by
/// configuration in any deployed environment.
/// </summary>
public sealed class HintOverridingVisionAnalyzer(
    IVisionAnalyzer inner,
    IOptions<VisionOptions> options) : IVisionAnalyzer
{
    public async Task<VisionAnalysis> AnalyzeAsync(VisionRequest request, CancellationToken cancellationToken)
    {
        var analysis = await inner.AnalyzeAsync(request, cancellationToken);
        var hints = request.Hints;

        if (!options.Value.AllowClientHints || hints is null)
        {
            return analysis;
        }

        return analysis with
        {
            VehicleDetected = hints.VehicleDetected ?? analysis.VehicleDetected,
            TiltDegrees = hints.VehicleTiltDegrees ?? analysis.TiltDegrees,
            PlateDetected = hints.PlateDetected ?? analysis.PlateDetected,
            PlateText = hints.PlateText ?? analysis.PlateText,
            PlateConfidence = hints.PlateConfidence ?? analysis.PlateConfidence,
            Source = analysis.Source + "+hints"
        };
    }
}
