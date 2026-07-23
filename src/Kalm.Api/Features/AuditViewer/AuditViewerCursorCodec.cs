using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kalm.Api.Features.Authorization;
using Microsoft.AspNetCore.DataProtection;

namespace Kalm.Api.Features.AuditViewer;

public sealed class AuditViewerCursorCodec(IDataProtectionProvider protectionProvider)
{
    private const int CurrentVersion = 1;
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("Kalm.AuditViewer.Cursor.v1");

    public string Encode(
        Guid organizationId,
        EffectiveBranchAccessSnapshot access,
        AuditViewerFilter filter,
        DateTimeOffset occurredAtUtc,
        Guid auditId,
        AuditCursorDirection direction)
        => _protector.Protect(JsonSerializer.Serialize(new CursorPayload(
            CurrentVersion,
            organizationId,
            ScopeFingerprint(access),
            FilterFingerprint(filter),
            occurredAtUtc,
            auditId,
            direction)));

    public bool TryDecode(
        string cursor,
        Guid organizationId,
        EffectiveBranchAccessSnapshot access,
        AuditViewerFilter filter,
        out AuditCursorAnchor anchor)
    {
        anchor = default;
        try
        {
            CursorPayload? payload = JsonSerializer.Deserialize<CursorPayload>(_protector.Unprotect(cursor));
            if (payload is null
                || payload.Version != CurrentVersion
                || payload.OrganizationId != organizationId
                || payload.OccurredAtUtc.Offset != TimeSpan.Zero
                || payload.AuditId == Guid.Empty
                || !Enum.IsDefined(payload.Direction)
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(payload.ScopeFingerprint),
                    Encoding.ASCII.GetBytes(ScopeFingerprint(access)))
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(payload.FilterFingerprint),
                    Encoding.ASCII.GetBytes(FilterFingerprint(filter))))
            {
                return false;
            }

            anchor = new AuditCursorAnchor(payload.OccurredAtUtc, payload.AuditId, payload.Direction);
            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            return false;
        }
    }

    private static string ScopeFingerprint(EffectiveBranchAccessSnapshot access)
        => Hash($"{access.Scope}|{string.Join(',', access.OperationalBranchIds.OrderBy(id => id))}");

    private static string FilterFingerprint(AuditViewerFilter filter)
        => Hash(string.Join('|',
            filter.FromUtc.ToString("O"),
            filter.ToUtc.ToString("O"),
            filter.Action ?? string.Empty,
            filter.Result ?? string.Empty,
            filter.ActorId?.ToString("D") ?? string.Empty,
            filter.TargetType ?? string.Empty,
            filter.TargetId?.ToString("D") ?? string.Empty,
            filter.BranchId?.ToString("D") ?? string.Empty,
            filter.CorrelationId ?? string.Empty,
            filter.PageSize));

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record CursorPayload(
        int Version,
        Guid OrganizationId,
        string ScopeFingerprint,
        string FilterFingerprint,
        DateTimeOffset OccurredAtUtc,
        Guid AuditId,
        AuditCursorDirection Direction);
}

public enum AuditCursorDirection
{
    Next,
    Previous
}

public readonly record struct AuditCursorAnchor(
    DateTimeOffset OccurredAtUtc,
    Guid AuditId,
    AuditCursorDirection Direction);
