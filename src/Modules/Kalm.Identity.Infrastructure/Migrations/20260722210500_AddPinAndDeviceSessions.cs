using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Kalm.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPinAndDeviceSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "authorization_version",
                schema: "identity",
                table: "user_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "identity",
                table: "user_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "device_id",
                schema: "identity",
                table: "user_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "device_security_version",
                schema: "identity",
                table: "user_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pin_credential_version",
                schema: "identity",
                table: "user_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pin_credentials",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encoded_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pin_credentials", x => x.id);
                    table.ForeignKey(
                        name: "FK_pin_credentials_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pin_login_attempts",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pin_login_attempts", x => x.id);
                    table.CheckConstraint("ck_pin_login_attempts_outcome", "outcome in ('Succeeded', 'InvalidCredentials', 'Locked', 'Ineligible')");
                    table.ForeignKey(
                        name: "FK_pin_login_attempts_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_device_id_revoked_at_utc",
                schema: "identity",
                table: "user_sessions",
                columns: new[] { "device_id", "revoked_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_pin_credentials_user_id",
                schema: "identity",
                table: "pin_credentials",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pin_login_attempts_device_user_occurred",
                schema: "identity",
                table: "pin_login_attempts",
                columns: new[] { "device_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_pin_login_attempts_user_id",
                schema: "identity",
                table: "pin_login_attempts",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pin_credentials",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "pin_login_attempts",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ix_user_sessions_device_id_revoked_at_utc",
                schema: "identity",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "authorization_version",
                schema: "identity",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "identity",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "device_id",
                schema: "identity",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "device_security_version",
                schema: "identity",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "pin_credential_version",
                schema: "identity",
                table: "user_sessions");
        }
    }
}
