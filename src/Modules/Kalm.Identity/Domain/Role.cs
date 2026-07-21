using Kalm.Identity.Domain.ValueObjects;

namespace Kalm.Identity.Domain;

public sealed class Role
{
    private Role()
    {
    }

    private Role(Guid id, Guid organizationId, RoleName name, string? systemKey, DateTimeOffset now)
    {
        EnsureUtc(now);
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        Id = id;
        OrganizationId = organizationId;
        Name = name.Value;
        NormalizedName = name.NormalizedValue;
        SystemKey = NormalizeSystemKey(systemKey);
        Status = RoleStatus.Active;
        Version = 1;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? SystemKey { get; private set; }
    public RoleStatus Status { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public static Role Create(Guid id, Guid organizationId, RoleName name, string? systemKey, DateTimeOffset now)
        => new(id, organizationId, name, systemKey, now);

    public void RecordPermissionSetChanged(DateTimeOffset now)
    {
        EnsureActive();
        AdvanceVersion(now);
    }

    public void Archive(DateTimeOffset now)
    {
        EnsureUtc(now);
        if (Status == RoleStatus.Archived)
        {
            return;
        }

        Status = RoleStatus.Archived;
        ArchivedAtUtc = now;
        AdvanceVersion(now);
    }

    private void EnsureActive()
    {
        if (Status != RoleStatus.Active)
        {
            throw new InvalidOperationException("Archived roles cannot be changed.");
        }
    }

    private void AdvanceVersion(DateTimeOffset now)
    {
        EnsureUtc(now);
        Version++;
        UpdatedAtUtc = now;
    }

    private static string? NormalizeSystemKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > 100)
        {
            throw new ArgumentException("System role key cannot exceed 100 characters.", nameof(value));
        }

        return normalized;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be UTC.", nameof(value));
        }
    }
}
