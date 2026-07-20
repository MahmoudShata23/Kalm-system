using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Kalm.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    business_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true),
                    network_identifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_logs_actor_type",
                schema: "audit",
                table: "audit_logs",
                sql: "actor_type in ('Anonymous', 'System', 'User')");
            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_logs_action",
                schema: "audit",
                table: "audit_logs",
                sql: "action in ('OrganizationCreated', 'OrganizationUpdated', 'OrganizationStatusChanged', 'BranchCreated', 'BranchUpdated', 'BranchStatusChanged')");
            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_logs_result",
                schema: "audit",
                table: "audit_logs",
                sql: "result in ('Succeeded', 'Failed', 'Denied')");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_action_occurred_at_utc",
                schema: "audit",
                table: "audit_logs",
                columns: new[] { "action", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_id_occurred_at_utc",
                schema: "audit",
                table: "audit_logs",
                columns: new[] { "actor_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_branch_id_occurred_at_utc",
                schema: "audit",
                table: "audit_logs",
                columns: new[] { "branch_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_correlation_id",
                schema: "audit",
                table: "audit_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type_entity_id_occurred_at_utc",
                schema: "audit",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at_utc",
                schema: "audit",
                table: "audit_logs",
                column: "occurred_at_utc");

            migrationBuilder.Sql(
                """
                create function audit.reject_audit_log_mutation()
                returns trigger
                language plpgsql
                as $$
                begin
                    raise exception 'audit.audit_logs is immutable';
                end;
                $$;

                create trigger trg_audit_logs_immutable
                before update or delete on audit.audit_logs
                for each row execute function audit.reject_audit_log_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("drop trigger if exists trg_audit_logs_immutable on audit.audit_logs;");
            migrationBuilder.Sql("drop function if exists audit.reject_audit_log_mutation();");
            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "audit");
        }
    }
}
#pragma warning restore CA1861
