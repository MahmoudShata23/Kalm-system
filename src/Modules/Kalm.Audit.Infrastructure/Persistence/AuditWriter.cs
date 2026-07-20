using Kalm.Audit.Application;
using Kalm.Audit.Domain;

namespace Kalm.Audit.Infrastructure.Persistence;

public sealed class AuditWriter : IAuditWriter
{
    private readonly AuditDbContext _context;

    public AuditWriter(AuditDbContext context)
    {
        _context = context;
    }

    public Task AppendAsync(AuditWriteRequest request, CancellationToken cancellationToken)
    {
        _context.AuditEntries.Add(AuditEntry.Create(
            request.Id, request.OccurredAtUtc, request.OrganizationId, request.BranchId, request.BusinessDate,
            request.ActorId, request.ActorType, request.DeviceId, request.Action, request.EntityType, request.EntityId,
            request.Result, request.ReasonCode, request.CorrelationId, request.BeforeJson, request.AfterJson,
            request.NetworkIdentifier, request.UserAgent));
        return Task.CompletedTask;
    }
}
