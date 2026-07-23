using Kalm.Api.Features.Authorization;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Features.AuditViewer;

public sealed class AuditViewerQueries(
    AuditDbContext audit,
    IdentityDbContext identity,
    OrganizationDbContext organization,
    AuditViewerCursorCodec cursors)
{
    private static readonly HashSet<string> SafeTargetTypes = new(StringComparer.Ordinal)
    {
        "Authorization", "Branch", "Device", "Organization", "Role", "User"
    };

    public async Task<AuditLogListResponse> ListAsync(
        Guid organizationId,
        EffectiveBranchAccessSnapshot access,
        AuditViewerFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.FromUtc.Offset != TimeSpan.Zero
            || filter.ToUtc.Offset != TimeSpan.Zero
            || filter.FromUtc >= filter.ToUtc
            || filter.PageSize is < 1 or > 100)
            throw new AuditViewerQueryException("audit.invalid_filter");
        if (filter.ToUtc - filter.FromUtc > TimeSpan.FromDays(90))
            throw new AuditViewerQueryException("audit.interval_too_large");
        ValidateBranchFilter(access, filter.BranchId);
        AuditAction? action = filter.Action is null ? null : ParseAction(filter.Action);
        AuditResult? result = filter.Result is null ? null : ParseResult(filter.Result);

        AuditCursorAnchor? anchor = null;
        if (filter.Cursor is not null)
        {
            if (!cursors.TryDecode(filter.Cursor, organizationId, access, filter, out AuditCursorAnchor decoded))
            {
                throw new AuditViewerQueryException("audit.invalid_cursor");
            }
            anchor = decoded;
        }

        IQueryable<AuditEntry> query = ScopedQuery(organizationId, access)
            .Where(entry => entry.OccurredAtUtc >= filter.FromUtc && entry.OccurredAtUtc <= filter.ToUtc);
        if (action is not null) query = query.Where(entry => entry.Action == action.Value);
        if (result is not null) query = query.Where(entry => entry.Result == result.Value);
        if (filter.ActorId is not null) query = query.Where(entry => entry.ActorId == filter.ActorId);
        if (filter.TargetType is not null) query = query.Where(entry => entry.EntityType == filter.TargetType);
        if (filter.TargetId is not null) query = query.Where(entry => entry.EntityId == filter.TargetId);
        if (filter.BranchId is not null) query = query.Where(entry => entry.BranchId == filter.BranchId);
        if (filter.CorrelationId is not null) query = query.Where(entry => entry.CorrelationId == filter.CorrelationId);

        bool previous = anchor?.Direction == AuditCursorDirection.Previous;
        if (anchor is not null)
        {
            AuditCursorAnchor value = anchor.Value;
            query = previous
                ? query.Where(entry => entry.OccurredAtUtc > value.OccurredAtUtc
                    || (entry.OccurredAtUtc == value.OccurredAtUtc && entry.Id.CompareTo(value.AuditId) > 0))
                : query.Where(entry => entry.OccurredAtUtc < value.OccurredAtUtc
                    || (entry.OccurredAtUtc == value.OccurredAtUtc && entry.Id.CompareTo(value.AuditId) < 0));
        }

        IQueryable<AuditListRow> ordered = previous
            ? query.OrderBy(entry => entry.OccurredAtUtc).ThenBy(entry => entry.Id).Select(ToListRow())
            : query.OrderByDescending(entry => entry.OccurredAtUtc).ThenByDescending(entry => entry.Id).Select(ToListRow());
        List<AuditListRow> rows = await ordered.Take(filter.PageSize + 1).ToListAsync(cancellationToken);
        bool extra = rows.Count > filter.PageSize;
        if (extra) rows.RemoveAt(rows.Count - 1);
        if (previous) rows.Reverse();

        Dictionary<Guid, string> actors = await ActorNamesAsync(organizationId, rows, cancellationToken);
        Dictionary<Guid, AuditBranchHintResponse> branches = await BranchHintsAsync(organizationId, access, rows.Select(row => row.BranchId), cancellationToken);
        AuditLogListItemResponse[] items = rows.Select(row => ToListItem(row, actors, branches)).ToArray();

        bool hasPrevious = previous ? extra : anchor is not null;
        bool hasNext = previous ? anchor is not null : extra;
        string? previousCursor = hasPrevious && rows.Count > 0
            ? cursors.Encode(organizationId, access, filter, rows[0].OccurredAtUtc, rows[0].Id, AuditCursorDirection.Previous)
            : null;
        string? nextCursor = hasNext && rows.Count > 0
            ? cursors.Encode(organizationId, access, filter, rows[^1].OccurredAtUtc, rows[^1].Id, AuditCursorDirection.Next)
            : null;
        return new AuditLogListResponse(items, filter.PageSize, nextCursor, previousCursor);
    }

    public async Task<AuditLogDetailResponse?> GetAsync(
        Guid organizationId,
        EffectiveBranchAccessSnapshot access,
        Guid auditLogId,
        CancellationToken cancellationToken)
    {
        AuditEntry? entry = await ScopedQuery(organizationId, access)
            .SingleOrDefaultAsync(candidate => candidate.Id == auditLogId, cancellationToken);
        if (entry is null) return null;

        Dictionary<Guid, string> actors = await ActorNamesAsync(
            organizationId,
            [new AuditListRow(entry.Id, entry.OccurredAtUtc, entry.Action, entry.Result, entry.ActorId, entry.ActorType, entry.EntityType, entry.EntityId, entry.BranchId, entry.CorrelationId)],
            cancellationToken);
        Dictionary<Guid, AuditBranchHintResponse> branches = await BranchHintsAsync(organizationId, access, [entry.BranchId], cancellationToken);
        AuditLogListItemResponse item = ToListItem(
            new AuditListRow(entry.Id, entry.OccurredAtUtc, entry.Action, entry.Result, entry.ActorId, entry.ActorType, entry.EntityType, entry.EntityId, entry.BranchId, entry.CorrelationId),
            actors,
            branches);
        return new AuditLogDetailResponse(
            item.Id, item.OccurredAtUtc, item.Action, item.Result, item.ActorId, item.ActorDisplayName,
            item.TargetType, item.TargetId, item.Branch, item.CorrelationId, item.Summary,
            SafeReason(entry.ReasonCode), AuditViewerMetadataPresenter.Present(entry));
    }

    public async Task<AuditViewerOptionsResponse> OptionsAsync(
        Guid organizationId,
        EffectiveBranchAccessSnapshot access,
        CancellationToken cancellationToken)
    {
        Guid[] operationalBranchIds = access.OperationalBranchIds.ToArray();
        AuditBranchHintResponse[] branches = await organization.Branches.AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId
                && branch.Status == BranchStatus.Active
                && operationalBranchIds.Contains(branch.Id))
            .OrderBy(branch => branch.Code).ThenBy(branch => branch.Id)
            .Select(branch => new AuditBranchHintResponse(branch.Id, branch.Code, branch.Name))
            .ToArrayAsync(cancellationToken);
        AuditFilterOptionResponse[] actions = Enum.GetValues<AuditAction>()
            .Select(value => new AuditFilterOptionResponse(Code(value), $"audit.action.{Code(value)}", Category(value)))
            .OrderBy(option => option.Code, StringComparer.Ordinal)
            .ToArray();
        AuditFilterOptionResponse[] results = Enum.GetValues<AuditResult>()
            .Select(value => new AuditFilterOptionResponse(Code(value), $"audit.result.{Code(value)}", "result"))
            .ToArray();
        return new AuditViewerOptionsResponse(actions, results, branches);
    }

    public static bool TryAction(string value, out AuditAction action)
        => TryCode(value, out action);

    public static bool TryResult(string value, out AuditResult result)
        => TryCode(value, out result);

    private IQueryable<AuditEntry> ScopedQuery(Guid organizationId, EffectiveBranchAccessSnapshot access)
    {
        IQueryable<AuditEntry> query = audit.AuditEntries.AsNoTracking()
            .Where(entry => entry.OrganizationId == organizationId);
        if (!string.Equals(access.Scope, "allOrganizationBranches", StringComparison.Ordinal))
        {
            Guid[] branchIds = access.OperationalBranchIds.ToArray();
            query = query.Where(entry => entry.BranchId.HasValue && branchIds.Contains(entry.BranchId.Value));
        }
        return query;
    }

    private static void ValidateBranchFilter(EffectiveBranchAccessSnapshot access, Guid? branchId)
    {
        if (branchId is not null && !access.OperationalBranchIds.Contains(branchId.Value))
            throw new AuditViewerQueryException("audit.invalid_filter");
    }

    private async Task<Dictionary<Guid, string>> ActorNamesAsync(Guid organizationId, IEnumerable<AuditListRow> rows, CancellationToken cancellationToken)
    {
        Guid[] ids = rows.Where(row => row.ActorType == AuditActorType.User && row.ActorId.HasValue)
            .Select(row => row.ActorId!.Value).Distinct().ToArray();
        return ids.Length == 0 ? [] : await identity.Users.AsNoTracking()
            .Where(user => user.OrganizationId == organizationId && ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
    }

    private async Task<Dictionary<Guid, AuditBranchHintResponse>> BranchHintsAsync(
        Guid organizationId,
        EffectiveBranchAccessSnapshot access,
        IEnumerable<Guid?> candidateIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = candidateIds.Where(id => id.HasValue && access.OperationalBranchIds.Contains(id.Value))
            .Select(id => id!.Value).Distinct().ToArray();
        return ids.Length == 0 ? [] : await organization.Branches.AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId && branch.Status == BranchStatus.Active && ids.Contains(branch.Id))
            .ToDictionaryAsync(branch => branch.Id, branch => new AuditBranchHintResponse(branch.Id, branch.Code, branch.Name), cancellationToken);
    }

    private static AuditLogListItemResponse ToListItem(
        AuditListRow row,
        Dictionary<Guid, string> actors,
        Dictionary<Guid, AuditBranchHintResponse> branches)
    {
        string? actorName = row.ActorType switch
        {
            AuditActorType.System => "System",
            AuditActorType.Anonymous => "Anonymous",
            AuditActorType.User when row.ActorId is Guid id && actors.TryGetValue(id, out string? displayName) => displayName,
            _ => null
        };
        Guid? actorId = row.ActorType == AuditActorType.User && row.ActorId is Guid idValue && actors.ContainsKey(idValue) ? idValue : null;
        string targetType = SafeTargetTypes.Contains(row.EntityType) ? row.EntityType : "Unknown";
        Guid? targetId = targetType == "Unknown" ? null : row.EntityId;
        AuditBranchHintResponse? branch = row.BranchId is Guid branchId && branches.TryGetValue(branchId, out AuditBranchHintResponse? hint) ? hint : null;
        string action = Code(row.Action);
        return new AuditLogListItemResponse(
            row.Id, row.OccurredAtUtc, action, Code(row.Result), actorId, actorName,
            targetType, targetId, branch, row.CorrelationId, $"{action} {targetType}");
    }

    private static System.Linq.Expressions.Expression<Func<AuditEntry, AuditListRow>> ToListRow()
        => entry => new AuditListRow(entry.Id, entry.OccurredAtUtc, entry.Action, entry.Result, entry.ActorId,
            entry.ActorType, entry.EntityType, entry.EntityId, entry.BranchId, entry.CorrelationId);

    private static AuditAction ParseAction(string value) => TryAction(value, out AuditAction parsed) ? parsed : throw new AuditViewerQueryException("audit.invalid_filter");
    private static AuditResult ParseResult(string value) => TryResult(value, out AuditResult parsed) ? parsed : throw new AuditViewerQueryException("audit.invalid_filter");

    private static bool TryCode<T>(string value, out T result) where T : struct, Enum
    {
        foreach (T candidate in Enum.GetValues<T>())
        {
            if (string.Equals(Code(candidate), value, StringComparison.Ordinal)) { result = candidate; return true; }
        }
        result = default;
        return false;
    }

    internal static string Code<T>(T value) where T : struct, Enum
    {
        string name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string Category(AuditAction action)
    {
        string name = action.ToString();
        if (name.StartsWith("Branch", StringComparison.Ordinal) || name.StartsWith("Organization", StringComparison.Ordinal)) return "organization";
        if (name.StartsWith("Device", StringComparison.Ordinal) || name.StartsWith("PinLogin", StringComparison.Ordinal) || name == nameof(AuditAction.WorkstationLocked)) return "devices";
        if (name.StartsWith("Role", StringComparison.Ordinal) || name.Contains("Authorization", StringComparison.Ordinal) || name.Contains("ManagementAccess", StringComparison.Ordinal) || name.StartsWith("SystemRole", StringComparison.Ordinal) || name.StartsWith("LastManagement", StringComparison.Ordinal)) return "authorization";
        if (name.StartsWith("User", StringComparison.Ordinal) || name.StartsWith("Password", StringComparison.Ordinal)) return "users";
        if (name.StartsWith("Management", StringComparison.Ordinal)) return "authentication";
        return "system";
    }

    private static string? SafeReason(string? reason)
        => reason is not null && reason.Length <= 100 && reason.All(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-') ? reason : null;

    private sealed record AuditListRow(
        Guid Id, DateTimeOffset OccurredAtUtc, AuditAction Action, AuditResult Result,
        Guid? ActorId, AuditActorType ActorType, string EntityType, Guid? EntityId,
        Guid? BranchId, string CorrelationId);
}

public sealed class AuditViewerQueryException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
