using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Persistence;

public sealed class KalmDbContext : DbContext
{
    public KalmDbContext(DbContextOptions<KalmDbContext> options)
        : base(options)
    {
    }

    public DbSet<SchemaMarker> SchemaMarkers => Set<SchemaMarker>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");

        modelBuilder.Entity<SchemaMarker>(builder =>
        {
            builder.ToTable("schema_markers");
            builder.HasKey(marker => marker.Id);
            builder.Property(marker => marker.Name).HasMaxLength(128).IsRequired();
            builder.Property(marker => marker.CreatedAtUtc).HasColumnType("timestamptz").IsRequired();
            builder.HasIndex(marker => marker.Name).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Type).HasMaxLength(256).IsRequired();
            builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            builder.Property(message => message.OccurredAtUtc).HasColumnType("timestamptz").IsRequired();
            builder.Property(message => message.ProcessedAtUtc).HasColumnType("timestamptz");
            builder.Property(message => message.Error).HasMaxLength(2048);
            builder.HasIndex(message => new { message.ProcessedAtUtc, message.OccurredAtUtc });
        });

        modelBuilder.Entity<IdempotencyRecord>(builder =>
        {
            builder.ToTable("idempotency_records");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Key).HasMaxLength(128).IsRequired();
            builder.Property(record => record.RequestHash).HasMaxLength(128).IsRequired();
            builder.Property(record => record.ResponseBody).HasColumnType("jsonb").IsRequired();
            builder.Property(record => record.CreatedAtUtc).HasColumnType("timestamptz").IsRequired();
            builder.HasIndex(record => record.Key).IsUnique();
        });
    }
}
