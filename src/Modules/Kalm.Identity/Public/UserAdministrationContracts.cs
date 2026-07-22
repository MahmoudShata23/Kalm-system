namespace Kalm.Identity;

public sealed class UserCreateRequest
{
    public string Username { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string PreferredLanguage { get; init; } = string.Empty;
    public IReadOnlyCollection<Guid> RoleIds { get; init; } = [];
    public string BranchAccessScope { get; init; } = string.Empty;
    public IReadOnlyCollection<Guid> BranchIds { get; init; } = [];
    public string? InitialPassword { get; set; }
}

public sealed record UserUpdateRequest(
    string Username,
    string? Email,
    string DisplayName,
    string PreferredLanguage,
    IReadOnlyCollection<Guid> RoleIds,
    string BranchAccessScope,
    IReadOnlyCollection<Guid> BranchIds);

public sealed record UserSuspendRequest(bool ConfirmSelfSuspension);

public sealed class UserPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}

public sealed record UserSummaryResponse(
    Guid Id,
    string Username,
    string? Email,
    string DisplayName,
    string PreferredLanguage,
    string Status,
    IReadOnlyCollection<string> RoleNames,
    DateTimeOffset UpdatedAtUtc);

public sealed record UserListResponse(
    IReadOnlyCollection<UserSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record UserDetailResponse(
    Guid Id,
    string Username,
    string? Email,
    string DisplayName,
    string PreferredLanguage,
    string Status,
    string CredentialStatus,
    IReadOnlyCollection<Guid> RoleIds,
    string BranchAccessScope,
    IReadOnlyCollection<Guid> BranchIds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ActivatedAtUtc);

public sealed record UserEditorOptionsResponse(
    IReadOnlyCollection<UserRoleOptionResponse> Roles,
    IReadOnlyCollection<UserBranchOptionResponse> Branches);

public sealed record UserRoleOptionResponse(Guid Id, string Name);

public sealed record UserBranchOptionResponse(Guid Id, string Name, string Code);
