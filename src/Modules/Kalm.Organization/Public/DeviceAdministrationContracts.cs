namespace Kalm.Organization;

public sealed record DeviceCreateRequest(Guid BranchId, string Name, string Type, string? Platform);
public sealed record DeviceUpdateRequest(Guid BranchId, string Name, string Type, string? Platform);
public sealed record DevicePairRequest(Guid DeviceId, string PairingChallenge);
public sealed record DevicePairingChallengeResponse(Guid DeviceId, string PairingChallenge, DateTimeOffset ExpiresAtUtc);
public sealed record DeviceSummaryResponse(Guid Id, Guid BranchId, string BranchName, string Name, string Type, string? Platform, string Status, DateTimeOffset? PairedAtUtc, DateTimeOffset? LastSeenAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record DeviceDetailResponse(Guid Id, Guid BranchId, string BranchName, string Name, string Type, string? Platform, string Status, DateTimeOffset? PairedAtUtc, DateTimeOffset? LastSeenAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record DeviceListResponse(IReadOnlyCollection<DeviceSummaryResponse> Items, int Page, int PageSize, int TotalCount);
public sealed record DeviceOptionsResponse(IReadOnlyCollection<DeviceBranchOptionResponse> Branches, IReadOnlyCollection<DeviceTypeOptionResponse> Types);
public sealed record DeviceBranchOptionResponse(Guid Id, string Name, string Code);
public sealed record DeviceTypeOptionResponse(string Code, string NameEn, string NameAr);
