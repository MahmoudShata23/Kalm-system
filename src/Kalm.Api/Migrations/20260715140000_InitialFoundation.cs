using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalm.Api.Migrations;

public partial class InitialFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "platform");

        migrationBuilder.CreateTable(
            name: "idempotency_records",
            schema: "platform",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                response_body = table.Column<string>(type: "jsonb", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_idempotency_records", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "platform",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                processed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox_messages", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "schema_markers",
            schema: "platform",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_schema_markers", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_idempotency_records_key",
            schema: "platform",
            table: "idempotency_records",
            column: "key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_processed_at_utc_occurred_at_utc",
            schema: "platform",
            table: "outbox_messages",
            columns: ["processed_at_utc", "occurred_at_utc"]);

        migrationBuilder.CreateIndex(
            name: "ix_schema_markers_name",
            schema: "platform",
            table: "schema_markers",
            column: "name",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "idempotency_records", schema: "platform");
        migrationBuilder.DropTable(name: "outbox_messages", schema: "platform");
        migrationBuilder.DropTable(name: "schema_markers", schema: "platform");
    }
}
