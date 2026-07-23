using Kalm.Audit.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Audit.Infrastructure.Persistence;

public sealed class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit");
        modelBuilder.Entity<AuditEntry>(builder =>
        {
            builder.ToTable("audit_logs");
            builder.HasKey(entry => entry.Id);
            builder.Property(entry => entry.Id).HasColumnName("id");
            builder.Property(entry => entry.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(entry => entry.OrganizationId).HasColumnName("organization_id");
            builder.Property(entry => entry.BranchId).HasColumnName("branch_id");
            builder.Property(entry => entry.BusinessDate).HasColumnName("business_date").HasColumnType("date");
            builder.Property(entry => entry.ActorId).HasColumnName("actor_id");
            builder.Property(entry => entry.ActorType).HasColumnName("actor_type").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(entry => entry.DeviceId).HasColumnName("device_id");
            builder.Property(entry => entry.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(100).IsRequired();
            builder.Property(entry => entry.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            builder.Property(entry => entry.EntityId).HasColumnName("entity_id");
            builder.Property(entry => entry.Result).HasColumnName("result").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(entry => entry.ReasonCode).HasColumnName("reason_code").HasMaxLength(100);
            builder.Property(entry => entry.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
            builder.Property(entry => entry.BeforeJson).HasColumnName("before_json").HasColumnType("jsonb");
            builder.Property(entry => entry.AfterJson).HasColumnName("after_json").HasColumnType("jsonb");
            builder.Property(entry => entry.NetworkIdentifier).HasColumnName("network_identifier").HasMaxLength(128);
            builder.Property(entry => entry.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
            builder.HasIndex(entry => entry.OccurredAtUtc).HasDatabaseName("ix_audit_logs_occurred_at_utc");
            builder.HasIndex(entry => new { entry.OrganizationId, entry.OccurredAtUtc, entry.Id })
                .IsDescending(false, true, true)
                .HasDatabaseName("ix_audit_logs_organization_occurred_id");
            builder.HasIndex(entry => new { entry.OrganizationId, entry.BranchId, entry.OccurredAtUtc, entry.Id })
                .IsDescending(false, false, true, true)
                .HasDatabaseName("ix_audit_logs_organization_branch_occurred_id");
            builder.HasIndex(entry => new { entry.BranchId, entry.OccurredAtUtc }).HasDatabaseName("ix_audit_logs_branch_id_occurred_at_utc");
            builder.HasIndex(entry => new { entry.ActorId, entry.OccurredAtUtc }).HasDatabaseName("ix_audit_logs_actor_id_occurred_at_utc");
            builder.HasIndex(entry => new { entry.EntityType, entry.EntityId, entry.OccurredAtUtc }).HasDatabaseName("ix_audit_logs_entity_type_entity_id_occurred_at_utc");
            builder.HasIndex(entry => new { entry.Action, entry.OccurredAtUtc }).HasDatabaseName("ix_audit_logs_action_occurred_at_utc");
            builder.HasIndex(entry => entry.CorrelationId).HasDatabaseName("ix_audit_logs_correlation_id");
        });
    }
}
