namespace Kalm.Audit.Domain;

public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    private AuditEntry(
        Guid id,
        DateTimeOffset occurredAtUtc,
        Guid? organizationId,
        Guid? branchId,
        DateOnly? businessDate,
        Guid? actorId,
        AuditActorType actorType,
        Guid? deviceId,
        AuditAction action,
        string entityType,
        Guid? entityId,
        AuditResult result,
        string? reasonCode,
        string correlationId,
        string? beforeJson,
        string? afterJson,
        string? networkIdentifier,
        string? userAgent)
    {
        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Audit timestamp must be UTC.", nameof(occurredAtUtc));
        }

        Id = id;
        OccurredAtUtc = occurredAtUtc;
        OrganizationId = organizationId;
        BranchId = branchId;
        BusinessDate = businessDate;
        ActorId = actorId;
        ActorType = actorType;
        DeviceId = deviceId;
        Action = action;
        EntityType = RequiredBounded(entityType, 100, nameof(entityType));
        EntityId = entityId;
        Result = result;
        ReasonCode = OptionalBounded(reasonCode, 100, nameof(reasonCode));
        CorrelationId = RequiredBounded(correlationId, 128, nameof(correlationId));
        BeforeJson = OptionalBounded(beforeJson, 16000, nameof(beforeJson));
        AfterJson = OptionalBounded(afterJson, 16000, nameof(afterJson));
        NetworkIdentifier = OptionalBounded(networkIdentifier, 128, nameof(networkIdentifier));
        UserAgent = OptionalBounded(userAgent, 512, nameof(userAgent));
    }

    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? BranchId { get; private set; }
    public DateOnly? BusinessDate { get; private set; }
    public Guid? ActorId { get; private set; }
    public AuditActorType ActorType { get; private set; }
    public Guid? DeviceId { get; private set; }
    public AuditAction Action { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public AuditResult Result { get; private set; }
    public string? ReasonCode { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string? NetworkIdentifier { get; private set; }
    public string? UserAgent { get; private set; }

    public static AuditEntry Create(
        Guid id,
        DateTimeOffset occurredAtUtc,
        Guid? organizationId,
        Guid? branchId,
        DateOnly? businessDate,
        Guid? actorId,
        AuditActorType actorType,
        Guid? deviceId,
        AuditAction action,
        string entityType,
        Guid? entityId,
        AuditResult result,
        string? reasonCode,
        string correlationId,
        string? beforeJson,
        string? afterJson,
        string? networkIdentifier,
        string? userAgent)
        => new(id, occurredAtUtc, organizationId, branchId, businessDate, actorId, actorType, deviceId, action, entityType, entityId, result, reasonCode, correlationId, beforeJson, afterJson, networkIdentifier, userAgent);

    private static string RequiredBounded(string value, int maximumLength, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{parameterName} is required and cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? OptionalBounded(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{parameterName} cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }
}
