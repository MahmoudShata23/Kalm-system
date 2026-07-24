namespace Kalm.Api.Features.AuditViewer;

public sealed record AuditLogListResponse(
    IReadOnlyList<AuditLogListItemResponse> Items,
    int PageSize,
    string? NextCursor,
    string? PreviousCursor);

public sealed record AuditLogListItemResponse(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Action,
    string Result,
    Guid? ActorId,
    string? ActorDisplayName,
    string TargetType,
    Guid? TargetId,
    AuditBranchHintResponse? Branch,
    string CorrelationId,
    string Summary);

public sealed record AuditLogDetailResponse(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Action,
    string Result,
    Guid? ActorId,
    string? ActorDisplayName,
    string TargetType,
    Guid? TargetId,
    AuditBranchHintResponse? Branch,
    string CorrelationId,
    string Summary,
    string? ReasonCode,
    AuditSafeMetadataResponse? Metadata);

public sealed record AuditBranchHintResponse(Guid Id, string Code, string Name);

public sealed record AuditViewerOptionsResponse(
    IReadOnlyList<AuditFilterOptionResponse> Actions,
    IReadOnlyList<AuditFilterOptionResponse> Results,
    IReadOnlyList<AuditBranchHintResponse> Branches);

public sealed record AuditFilterOptionResponse(string Code, string PresentationKey, string Category);

public sealed record AuditSafeMetadataResponse(
    IReadOnlyList<string> ChangedFields,
    string? PreviousStatus,
    string? NewStatus,
    int? RegisteredDeviceCount,
    int? ActiveDeviceCount,
    int? ActiveCredentialCount,
    int? ActiveSessionCount,
    int? ActiveUserAssignmentCount,
    int? ActiveRoleAssignmentCount,
    int? SessionsRevokedCount,
    int? AffectedCount,
    Guid? RelatedUserId,
    Guid? RelatedBranchId,
    Guid? RelatedDeviceId);

public sealed record AuditViewerFilter(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? Action,
    string? Result,
    Guid? ActorId,
    string? TargetType,
    Guid? TargetId,
    Guid? BranchId,
    string? CorrelationId,
    string? Cursor,
    int PageSize);
