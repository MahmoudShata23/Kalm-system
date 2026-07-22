namespace Kalm.Organization;

public sealed record BranchWriteRequest(
    string Name,
    string Code,
    string LocaleCode,
    string TimeZoneId,
    string BusinessDayRollover);

public sealed record BranchSummaryResponse(
    Guid Id,
    string Name,
    string Code,
    string LocaleCode,
    string TimeZoneId,
    string BusinessDayRollover,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record BranchDetailResponse(
    Guid Id,
    string Name,
    string Code,
    string LocaleCode,
    string TimeZoneId,
    string BusinessDayRollover,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record BranchListResponse(
    IReadOnlyCollection<BranchSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record BranchDependencyCountsResponse(
    int RegisteredDeviceCount,
    int ActiveDeviceCount,
    int ActiveCredentialCount,
    int ActiveSessionCount,
    int ActiveUserAssignmentCount);
