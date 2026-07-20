namespace Kalm.Identity.Domain;

public sealed class PasswordCredential
{
    private PasswordCredential()
    {
    }

    private PasswordCredential(Guid id, Guid userId, DateTimeOffset now)
    {
        EnsureUtc(now);
        Id = id;
        UserId = userId;
        Status = PasswordCredentialStatus.PendingSetup;
        Version = 1;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string? EncodedHash { get; private set; }
    public PasswordCredentialStatus Status { get; private set; }
    public int FailedAttemptCount { get; private set; }
    public DateTimeOffset? FailureWindowStartedAtUtc { get; private set; }
    public DateTimeOffset? LockedUntilUtc { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? PasswordChangedAtUtc { get; private set; }

    public static PasswordCredential Create(Guid id, Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User is required.", nameof(userId));
        }

        return new PasswordCredential(id, userId, now);
    }

    public void CompleteSetup(string encodedHash, DateTimeOffset now)
    {
        if (Status != PasswordCredentialStatus.PendingSetup)
        {
            throw new InvalidOperationException("Credential setup is already complete.");
        }

        SetHash(encodedHash, now);
        Status = PasswordCredentialStatus.Active;
    }

    public void ReplaceHash(string encodedHash, DateTimeOffset now) => SetHash(encodedHash, now);

    public bool IsLocked(DateTimeOffset now) => LockedUntilUtc is not null && now < LockedUntilUtc.Value;

    public bool RegisterFailure(DateTimeOffset now, int threshold, TimeSpan observationWindow, TimeSpan lockDuration)
    {
        EnsureUtc(now);
        if (IsLocked(now))
        {
            return false;
        }

        if (FailureWindowStartedAtUtc is null || now - FailureWindowStartedAtUtc.Value >= observationWindow)
        {
            FailureWindowStartedAtUtc = now;
            FailedAttemptCount = 1;
        }
        else
        {
            FailedAttemptCount++;
        }

        bool newlyLocked = FailedAttemptCount >= threshold;
        LockedUntilUtc = newlyLocked ? now.Add(lockDuration) : null;
        AdvanceVersion(now);
        return newlyLocked;
    }

    public void ClearFailures(DateTimeOffset now)
    {
        EnsureUtc(now);
        FailedAttemptCount = 0;
        FailureWindowStartedAtUtc = null;
        LockedUntilUtc = null;
        AdvanceVersion(now);
    }

    private void SetHash(string encodedHash, DateTimeOffset now)
    {
        EnsureUtc(now);
        if (string.IsNullOrWhiteSpace(encodedHash) || encodedHash.Length > 512)
        {
            throw new ArgumentException("Encoded password hash is invalid.", nameof(encodedHash));
        }

        EncodedHash = encodedHash;
        PasswordChangedAtUtc = now;
        AdvanceVersion(now);
    }

    private void AdvanceVersion(DateTimeOffset now)
    {
        Version++;
        UpdatedAtUtc = now;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be UTC.", nameof(value));
        }
    }
}
