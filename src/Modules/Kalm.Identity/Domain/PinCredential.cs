namespace Kalm.Identity.Domain;

public sealed class PinCredential
{
    private PinCredential() { }
    private PinCredential(Guid id, Guid userId, string encodedHash, DateTimeOffset now)
    { Id = id; UserId = userId; EncodedHash = encodedHash; Version = 1; CreatedAtUtc = now; UpdatedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string EncodedHash { get; private set; } = string.Empty;
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public static PinCredential Create(Guid id, Guid userId, string hash, DateTimeOffset now)
    {
        Validate(id, userId, hash, now); return new(id, userId, hash, now);
    }
    public void Replace(string hash, DateTimeOffset now) { Validate(Id, UserId, hash, now); EncodedHash = hash; Version++; UpdatedAtUtc = now; }
    private static void Validate(Guid id, Guid userId, string hash, DateTimeOffset now) { if (id == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(hash) || hash.Length > 512 || now.Offset != TimeSpan.Zero) throw new ArgumentException("PIN credential is invalid."); }
}
