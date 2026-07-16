namespace Kalm.Api.Persistence;

public sealed class SchemaMarker
{
    private SchemaMarker()
    {
    }

    public SchemaMarker(Guid id, string name, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
