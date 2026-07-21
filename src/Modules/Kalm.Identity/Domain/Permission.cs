using Kalm.Identity.Domain.ValueObjects;
using System.Diagnostics.CodeAnalysis;

namespace Kalm.Identity.Domain;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Permission is the approved domain term from IAM-004.")]
public sealed class Permission
{
    private Permission()
    {
    }

    private Permission(Guid id, PermissionCode code, DateTimeOffset now)
    {
        EnsureUtc(now);
        Id = id;
        Code = code.Value;
        Status = PermissionStatus.Active;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public PermissionStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RetiredAtUtc { get; private set; }

    public static Permission Create(Guid id, PermissionCode code, DateTimeOffset now) => new(id, code, now);

    public void Retire(DateTimeOffset now)
    {
        EnsureUtc(now);
        Status = PermissionStatus.Retired;
        RetiredAtUtc ??= now;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be UTC.", nameof(value));
        }
    }
}
