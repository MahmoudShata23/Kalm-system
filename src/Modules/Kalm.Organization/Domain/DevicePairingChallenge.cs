namespace Kalm.Organization.Domain;

public sealed class DevicePairingChallenge
{
    private DevicePairingChallenge() { }
    private DevicePairingChallenge(Guid id, Guid deviceId, string challengeHash, DateTimeOffset now, DateTimeOffset expiresAtUtc)
    {
        Id = id; DeviceId = deviceId; ChallengeHash = challengeHash; CreatedAtUtc = now; ExpiresAtUtc = expiresAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }
    public string ChallengeHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public DateTimeOffset? InvalidatedAtUtc { get; private set; }
    public static DevicePairingChallenge Create(Guid id, Guid deviceId, string hash, DateTimeOffset now, DateTimeOffset expires)
    {
        if (id == Guid.Empty || deviceId == Guid.Empty || string.IsNullOrWhiteSpace(hash) || hash.Length > 128 || now.Offset != TimeSpan.Zero || expires.Offset != TimeSpan.Zero || expires <= now) throw new ArgumentException("Pairing challenge is invalid.");
        return new(id, deviceId, hash, now, expires);
    }
    public bool IsUsable(DateTimeOffset now) => ConsumedAtUtc is null && InvalidatedAtUtc is null && now < ExpiresAtUtc;
    public void Consume(DateTimeOffset now) { if (!IsUsable(now)) throw new InvalidOperationException("Pairing challenge is unavailable."); ConsumedAtUtc = now; }
    public void Invalidate(DateTimeOffset now) { if (ConsumedAtUtc is null && InvalidatedAtUtc is null) InvalidatedAtUtc = now; }
}
