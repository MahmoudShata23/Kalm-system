namespace Kalm.Api.Features.Authentication;

public sealed record CurrentUserResponse(
    bool IsAuthenticated,
    string? Username,
    string? DisplayName,
    string? PreferredLanguage,
    DateTimeOffset? InactivityExpiresAtUtc,
    DateTimeOffset? AbsoluteExpiresAtUtc,
    DateTimeOffset? ReauthenticationValidUntilUtc,
    IReadOnlyCollection<string> Permissions,
    BranchAccessResponse? BranchAccess);

public sealed record BranchAccessResponse(
    string Scope,
    IReadOnlyCollection<Guid> BranchIds,
    IReadOnlyCollection<Guid> OperationalBranchIds);
