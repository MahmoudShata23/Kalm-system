namespace Kalm.Api.Persistence;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
    }

    public IdempotencyRecord(Guid id, string key, string requestHash, string responseBody, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Key = key;
        RequestHash = requestHash;
        ResponseBody = responseBody;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public string ResponseBody { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
