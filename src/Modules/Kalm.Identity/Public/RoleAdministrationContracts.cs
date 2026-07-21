namespace Kalm.Identity;

public sealed record RoleWriteRequest(string Name, IReadOnlyCollection<string> PermissionCodes);

public sealed record RoleSummaryResponse(
    Guid Id,
    string Name,
    string Status,
    bool IsProtectedSystemRole,
    int PermissionCount,
    int ActiveAssignmentCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record RoleListResponse(
    IReadOnlyCollection<RoleSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record RoleDetailResponse(
    Guid Id,
    string Name,
    string Status,
    bool IsProtectedSystemRole,
    int ActiveAssignmentCount,
    IReadOnlyCollection<string> PermissionCodes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

public sealed record PermissionCatalogueResponse(
    string CatalogueVersion,
    IReadOnlyCollection<PermissionPresentationResponse> Permissions);

public sealed record PermissionPresentationResponse(
    string Code,
    string GroupCode,
    int GroupOrder,
    int ItemOrder,
    string EnglishLabel,
    string EnglishDescription,
    string ArabicLabel,
    string ArabicDescription);
