using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Kalm.Organization.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "devices",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    platform = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    security_version = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    paired_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                    table.UniqueConstraint("ak_devices_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_devices_status", "status in ('PendingPairing', 'Active', 'Revoked')");
                    table.ForeignKey(
                        name: "FK_devices_branches_branch_id_organization_id",
                        columns: x => new { x.branch_id, x.organization_id },
                        principalSchema: "organization",
                        principalTable: "branches",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_credentials",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credential_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    security_version = table.Column<int>(type: "integer", nullable: false),
                    issued_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_credentials", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_credentials_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "organization",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_pairing_challenges",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    challenge_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    consumed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    invalidated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_pairing_challenges", x => x.id);
                    table.CheckConstraint("ck_device_pairing_challenges_expiry", "created_at_utc < expires_at_utc");
                    table.ForeignKey(
                        name: "FK_device_pairing_challenges_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "organization",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_device_credentials_active",
                schema: "organization",
                table: "device_credentials",
                column: "device_id",
                unique: true,
                filter: "revoked_at_utc is null");

            migrationBuilder.CreateIndex(
                name: "ux_device_credentials_hash",
                schema: "organization",
                table: "device_credentials",
                column: "credential_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_pairing_challenges_device_expiry",
                schema: "organization",
                table: "device_pairing_challenges",
                columns: new[] { "device_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_device_pairing_challenges_hash",
                schema: "organization",
                table: "device_pairing_challenges",
                column: "challenge_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_branch_id_organization_id",
                schema: "organization",
                table: "devices",
                columns: new[] { "branch_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_devices_organization_status_name_id",
                schema: "organization",
                table: "devices",
                columns: new[] { "organization_id", "status", "name", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_credentials",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "device_pairing_challenges",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "organization");
        }
    }
}
