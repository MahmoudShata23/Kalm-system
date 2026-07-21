using Kalm.Identity.Domain.ValueObjects;

namespace Kalm.Identity.Domain;

public sealed class User
{
    private User()
    {
    }

    private User(Guid id, Guid organizationId, Username username, EmailAddress? email, DisplayName displayName, string preferredLanguage, DateTimeOffset now)
    {
        EnsureUtc(now);
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        Id = id;
        OrganizationId = organizationId;
        Username = username.Value;
        NormalizedUsername = username.NormalizedValue;
        Email = email?.Value;
        NormalizedEmail = email?.NormalizedValue;
        DisplayName = displayName.Value;
        PreferredLanguage = NormalizeLanguage(preferredLanguage);
        Status = UserStatus.Suspended;
        Version = 1;
        AuthorizationVersion = 1;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string NormalizedUsername { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? NormalizedEmail { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string PreferredLanguage { get; private set; } = string.Empty;
    public UserStatus Status { get; private set; }
    public long Version { get; private set; }
    public long AuthorizationVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ActivatedAtUtc { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public static User Create(Guid id, Guid organizationId, Username username, EmailAddress? email, DisplayName displayName, string preferredLanguage, DateTimeOffset now)
        => new(id, organizationId, username, email, displayName, preferredLanguage, now);

    public void Activate(PasswordCredential credential, DateTimeOffset now)
    {
        EnsureUtc(now);
        if (credential.UserId != Id || credential.Status != PasswordCredentialStatus.Active)
        {
            throw new InvalidOperationException("An active password credential for this user is required.");
        }

        if (Status == UserStatus.Archived)
        {
            throw new InvalidOperationException("Archived users cannot be activated.");
        }

        Status = UserStatus.Active;
        ActivatedAtUtc = now;
        AdvanceVersion(now);
    }

    public void AdvanceAuthorizationVersion(DateTimeOffset now)
    {
        EnsureUtc(now);
        AuthorizationVersion++;
        AdvanceVersion(now);
    }

    private static string NormalizeLanguage(string value)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is not ("en" or "ar"))
        {
            throw new ArgumentException("Preferred language must be en or ar.", nameof(value));
        }

        return normalized;
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
