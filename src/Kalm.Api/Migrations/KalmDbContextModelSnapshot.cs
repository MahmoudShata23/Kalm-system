using System;
using Kalm.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Kalm.Api.Migrations;

[DbContext(typeof(KalmDbContext))]
sealed partial class KalmDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.10")
            .HasDefaultSchema("platform");

        modelBuilder.Entity("Kalm.Api.Persistence.IdempotencyRecord", entity =>
        {
            entity.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("id");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("timestamptz")
                .HasColumnName("created_at_utc");

            entity.Property<string>("Key")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("key");

            entity.Property<string>("RequestHash")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("request_hash");

            entity.Property<string>("ResponseBody")
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("response_body");

            entity.HasKey("Id").HasName("pk_idempotency_records");
            entity.HasIndex("Key").IsUnique().HasDatabaseName("ix_idempotency_records_key");
            entity.ToTable("idempotency_records", "platform");
        });

        modelBuilder.Entity("Kalm.Api.Persistence.OutboxMessage", entity =>
        {
            entity.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("id");

            entity.Property<string>("Error")
                .HasMaxLength(2048)
                .HasColumnType("character varying(2048)")
                .HasColumnName("error");

            entity.Property<DateTimeOffset>("OccurredAtUtc")
                .HasColumnType("timestamptz")
                .HasColumnName("occurred_at_utc");

            entity.Property<string>("Payload")
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("payload");

            entity.Property<DateTimeOffset?>("ProcessedAtUtc")
                .HasColumnType("timestamptz")
                .HasColumnName("processed_at_utc");

            entity.Property<string>("Type")
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnType("character varying(256)")
                .HasColumnName("type");

            entity.HasKey("Id").HasName("pk_outbox_messages");
            entity.HasIndex("ProcessedAtUtc", "OccurredAtUtc").HasDatabaseName("ix_outbox_messages_processed_at_utc_occurred_at_utc");
            entity.ToTable("outbox_messages", "platform");
        });

        modelBuilder.Entity("Kalm.Api.Persistence.SchemaMarker", entity =>
        {
            entity.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("id");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("timestamptz")
                .HasColumnName("created_at_utc");

            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("name");

            entity.HasKey("Id").HasName("pk_schema_markers");
            entity.HasIndex("Name").IsUnique().HasDatabaseName("ix_schema_markers_name");
            entity.ToTable("schema_markers", "platform");
        });
    }
}
