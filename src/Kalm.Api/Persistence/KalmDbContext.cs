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
            builder.Property(marker => marker.Id).HasColumnName("id");
            builder.Property(marker => marker.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
            builder.Property(marker => marker.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.HasIndex(marker => marker.Name).IsUnique().HasDatabaseName("ix_schema_markers_name");
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Id).HasColumnName("id");
            builder.Property(message => message.Type).HasColumnName("type").HasMaxLength(256).IsRequired();
            builder.Property(message => message.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            builder.Property(message => message.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(message => message.ProcessedAtUtc).HasColumnName("processed_at_utc").HasColumnType("timestamptz");
            builder.Property(message => message.Error).HasColumnName("error").HasMaxLength(2048);
            builder.HasIndex(message => new { message.ProcessedAtUtc, message.OccurredAtUtc })
                .HasDatabaseName("ix_outbox_messages_processed_at_utc_occurred_at_utc");
        });

        modelBuilder.Entity<IdempotencyRecord>(builder =>
        {
            builder.ToTable("idempotency_records");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Id).HasColumnName("id");
            builder.Property(record => record.Key).HasColumnName("key").HasMaxLength(128).IsRequired();
            builder.Property(record => record.RequestHash).HasColumnName("request_hash").HasMaxLength(128).IsRequired();
            builder.Property(record => record.ResponseBody).HasColumnName("response_body").HasColumnType("jsonb").IsRequired();
            builder.Property(record => record.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.HasIndex(record => record.Key).IsUnique().HasDatabaseName("ix_idempotency_records_key");
        });
    }
}
