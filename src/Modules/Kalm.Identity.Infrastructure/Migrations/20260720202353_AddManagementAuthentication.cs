using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Kalm.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagementAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    normalized_username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    preferred_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    activated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_preferred_language", "preferred_language in ('en', 'ar')");
                    table.CheckConstraint("ck_users_status", "status in ('Suspended', 'Active', 'Archived')");
                });

            migrationBuilder.CreateTable(
                name: "login_attempts",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    identifier_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fingerprint_key_version = table.Column<int>(type: "integer", nullable: false),
                    network_identifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_attempts", x => x.id);
                    table.CheckConstraint("ck_login_attempts_outcome", "outcome in ('Succeeded', 'InvalidCredentials', 'Locked', 'Ineligible')");
                    table.ForeignKey(
                        name: "FK_login_attempts_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "password_credentials",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encoded_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    failed_attempt_count = table.Column<int>(type: "integer", nullable: false),
                    failure_window_started_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    locked_until_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    password_changed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_credentials", x => x.id);
                    table.CheckConstraint("ck_password_credentials_failure_count", "failed_attempt_count >= 0");
                    table.CheckConstraint("ck_password_credentials_hash_state", "(status = 'PendingSetup' and encoded_hash is null) or (status <> 'PendingSetup' and encoded_hash is not null)");
                    table.CheckConstraint("ck_password_credentials_status", "status in ('PendingSetup', 'Active', 'Disabled')");
                    table.ForeignKey(
                        name: "FK_password_credentials_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_activity_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    inactivity_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    absolute_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_reauthenticated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.id);
                    table.CheckConstraint("ck_user_sessions_expiry", "created_at_utc <= last_activity_at_utc and last_activity_at_utc < inactivity_expires_at_utc and inactivity_expires_at_utc <= absolute_expires_at_utc");
                    table.CheckConstraint("ck_user_sessions_revocation_reason", "(revoked_at_utc is null and revocation_reason is null) or (revoked_at_utc is not null and revocation_reason is not null)");
                    table.ForeignKey(
                        name: "FK_user_sessions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_login_attempts_identifier_fingerprint_occurred_at_utc",
                schema: "identity",
                table: "login_attempts",
                columns: new[] { "identifier_fingerprint", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_login_attempts_network_identifier_occurred_at_utc",
                schema: "identity",
                table: "login_attempts",
                columns: new[] { "network_identifier", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_login_attempts_user_id_occurred_at_utc",
                schema: "identity",
                table: "login_attempts",
                columns: new[] { "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_password_credentials_user_id",
                schema: "identity",
                table: "password_credentials",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_inactivity_expires_at_utc",
                schema: "identity",
                table: "user_sessions",
                column: "inactivity_expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_user_id_revoked_at_absolute_expires_at",
                schema: "identity",
                table: "user_sessions",
                columns: new[] { "user_id", "revoked_at_utc", "absolute_expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_email",
                schema: "identity",
                table: "users",
                column: "normalized_email",
                unique: true,
                filter: "normalized_email is not null");

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_username",
                schema: "identity",
                table: "users",
                column: "normalized_username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_attempts",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "password_credentials",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_sessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
#pragma warning restore CA1861
