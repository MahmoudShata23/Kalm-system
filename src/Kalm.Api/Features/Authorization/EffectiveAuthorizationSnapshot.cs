using System.Collections.Frozen;

namespace Kalm.Api.Features.Authorization;

public sealed record EffectiveAuthorizationSnapshot
{
    public EffectiveAuthorizationSnapshot(
        Guid userId,
        Guid organizationId,
        IEnumerable<string> permissions,
        EffectiveBranchAccessSnapshot? branchAccess)
    {
        UserId = userId;
        OrganizationId = organizationId;
        Permissions = permissions.ToFrozenSet(StringComparer.Ordinal);
        BranchAccess = branchAccess;
    }

    public Guid UserId { get; }
    public Guid OrganizationId { get; }
    public IReadOnlySet<string> Permissions { get; }
    public EffectiveBranchAccessSnapshot? BranchAccess { get; }

    public static EffectiveAuthorizationSnapshot Empty(Guid userId, Guid organizationId)
        => new(userId, organizationId, [], null);
}

public sealed record EffectiveBranchAccessSnapshot
{
    public EffectiveBranchAccessSnapshot(
        string scope,
        IEnumerable<Guid> branchIds,
        IEnumerable<Guid> operationalBranchIds)
    {
        Scope = scope;
        BranchIds = Array.AsReadOnly(branchIds.Distinct().OrderBy(id => id).ToArray());
        OperationalBranchIds = operationalBranchIds.ToFrozenSet();
    }

    public string Scope { get; }
    public IReadOnlyCollection<Guid> BranchIds { get; }
    public IReadOnlySet<Guid> OperationalBranchIds { get; }
}
