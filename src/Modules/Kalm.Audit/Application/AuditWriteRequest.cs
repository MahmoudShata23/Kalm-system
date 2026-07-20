using Kalm.Audit.Domain;

namespace Kalm.Audit.Application;

public sealed record AuditWriteRequest(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    Guid? OrganizationId,
    Guid? BranchId,
    DateOnly? BusinessDate,
    Guid? ActorId,
    AuditActorType ActorType,
    Guid? DeviceId,
    AuditAction Action,
    string EntityType,
    Guid? EntityId,
    AuditResult Result,
    string? ReasonCode,
    string CorrelationId,
    string? BeforeJson,
    string? AfterJson,
    string? NetworkIdentifier,
    string? UserAgent);
