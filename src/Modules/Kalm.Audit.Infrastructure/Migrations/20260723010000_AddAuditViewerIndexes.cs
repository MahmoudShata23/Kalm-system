using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Kalm.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditViewerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_organization_branch_occurred_id",
                schema: "audit",
                table: "audit_logs",
                columns: new[] { "organization_id", "branch_id", "occurred_at_utc", "id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_organization_occurred_id",
                schema: "audit",
                table: "audit_logs",
                columns: new[] { "organization_id", "occurred_at_utc", "id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_logs_organization_branch_occurred_id",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_organization_occurred_id",
                schema: "audit",
                table: "audit_logs");
        }
    }
}
#pragma warning restore CA1861
