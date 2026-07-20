namespace Kalm.Identity.Domain;

public sealed class LoginAttempt
{
    private LoginAttempt()
    {
    }

    private LoginAttempt(Guid id, Guid? userId, string identifierFingerprint, int fingerprintKeyVersion, string? networkIdentifier, LoginAttemptOutcome outcome, DateTimeOffset occurredAtUtc, string correlationId)
    {
        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be UTC.", nameof(occurredAtUtc));
        }

        Id = id;
        UserId = userId;
        IdentifierFingerprint = Required(identifierFingerprint, 64, nameof(identifierFingerprint));
        FingerprintKeyVersion = fingerprintKeyVersion > 0 ? fingerprintKeyVersion : throw new ArgumentOutOfRangeException(nameof(fingerprintKeyVersion));
        NetworkIdentifier = Optional(networkIdentifier, 64, nameof(networkIdentifier));
        Outcome = outcome;
        OccurredAtUtc = occurredAtUtc;
        CorrelationId = Required(correlationId, 128, nameof(correlationId));
    }

    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string IdentifierFingerprint { get; private set; } = string.Empty;
    public int FingerprintKeyVersion { get; private set; }
    public string? NetworkIdentifier { get; private set; }
    public LoginAttemptOutcome Outcome { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;

    public static LoginAttempt Create(Guid id, Guid? userId, string identifierFingerprint, int fingerprintKeyVersion, string? networkIdentifier, LoginAttemptOutcome outcome, DateTimeOffset occurredAtUtc, string correlationId)
        => new(id, userId, identifierFingerprint, fingerprintKeyVersion, networkIdentifier, outcome, occurredAtUtc, correlationId);

    private static string Required(string? value, int maximum, string name)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= 128 && normalized.Length <= maximum
            ? normalized
            : throw new ArgumentException($"{name} is invalid.", name);
    }

    private static string? Optional(string? value, int maximum, string name)
        => string.IsNullOrWhiteSpace(value) ? null : Required(value, maximum, name);
}
