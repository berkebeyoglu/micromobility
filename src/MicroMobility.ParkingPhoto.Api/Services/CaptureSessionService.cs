using System.Collections.Concurrent;
using System.Security.Cryptography;
using MicroMobility.ParkingPhoto.Api.Configuration;
using Microsoft.Extensions.Options;

namespace MicroMobility.ParkingPhoto.Api.Services;

public sealed class CaptureSession
{
    public required string Id { get; init; }

    public required string Token { get; init; }

    public required string RideId { get; init; }

    public string? DeviceId { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? ConsumedAt { get; set; }
}

public enum CaptureSessionState
{
    Valid,
    NotFound,
    Expired,
    AlreadyUsed,
    RideMismatch,
    DeviceMismatch
}

public interface ICaptureSessionService
{
    CaptureSession Create(string rideId, string? deviceId);

    CaptureSessionState Validate(string? sessionId, string? token, string rideId, string? deviceId);

    void Consume(string sessionId);
}

/// <summary>
/// Issues the short lived, single use token that binds a photo to a camera session started by the
/// app. Without a matching token an upload cannot have come from the in-app camera flow.
/// </summary>
public sealed class CaptureSessionService(
    IOptions<CaptureSessionOptions> options,
    TimeProvider timeProvider) : ICaptureSessionService
{
    private readonly ConcurrentDictionary<string, CaptureSession> _sessions = new(StringComparer.Ordinal);
    private readonly CaptureSessionOptions _options = options.Value;

    public CaptureSession Create(string rideId, string? deviceId)
    {
        Prune();

        var now = timeProvider.GetUtcNow();
        var session = new CaptureSession
        {
            Id = Guid.NewGuid().ToString("N"),
            Token = Base64Url(RandomNumberGenerator.GetBytes(32)),
            RideId = rideId,
            DeviceId = deviceId,
            IssuedAt = now,
            ExpiresAt = now.AddSeconds(_options.TtlSeconds)
        };

        _sessions[session.Id] = session;
        return session;
    }

    public CaptureSessionState Validate(string? sessionId, string? token, string rideId, string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(token) ||
            !_sessions.TryGetValue(sessionId, out var session))
        {
            return CaptureSessionState.NotFound;
        }

        if (!FixedTimeEquals(session.Token, token))
        {
            return CaptureSessionState.NotFound;
        }

        if (!string.Equals(session.RideId, rideId, StringComparison.OrdinalIgnoreCase))
        {
            return CaptureSessionState.RideMismatch;
        }

        if (!string.IsNullOrWhiteSpace(session.DeviceId) &&
            !string.Equals(session.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
        {
            return CaptureSessionState.DeviceMismatch;
        }

        if (timeProvider.GetUtcNow() > session.ExpiresAt)
        {
            return CaptureSessionState.Expired;
        }

        if (_options.SingleUse && session.ConsumedAt is not null)
        {
            return CaptureSessionState.AlreadyUsed;
        }

        return CaptureSessionState.Valid;
    }

    public void Consume(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.ConsumedAt = timeProvider.GetUtcNow();
        }
    }

    private void Prune()
    {
        var cutoff = timeProvider.GetUtcNow().AddMinutes(-30);
        foreach (var (id, session) in _sessions)
        {
            if (session.ExpiresAt < cutoff)
            {
                _sessions.TryRemove(id, out _);
            }
        }
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a),
            System.Text.Encoding.UTF8.GetBytes(b));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
