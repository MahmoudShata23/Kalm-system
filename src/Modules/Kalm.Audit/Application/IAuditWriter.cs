namespace Kalm.Audit.Application;

public interface IAuditWriter
{
    Task AppendAsync(AuditWriteRequest request, CancellationToken cancellationToken);
}
