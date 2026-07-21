using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Kalm.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorizationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "authorization_version",
                schema: "identity",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_users_id_organization_id",
                schema: "identity",
                table: "users",
                columns: new[] { "id", "organization_id" });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    retired_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                    table.CheckConstraint("ck_permissions_code", "code ~ '^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$'");
                    table.CheckConstraint("ck_permissions_retired_state", "(status = 'Active' and retired_at_utc is null) or (status = 'Retired' and retired_at_utc is not null)");
                    table.CheckConstraint("ck_permissions_status", "status in ('Active', 'Retired')");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    system_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                    table.UniqueConstraint("ak_roles_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_roles_status", "status in ('Active', 'Archived')");
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.id);
                    table.CheckConstraint("ck_role_permissions_revocation", "revoked_at_utc is null or revoked_at_utc >= granted_at_utc");
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "identity",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_role_assignments",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role_assignments", x => x.id);
                    table.CheckConstraint("ck_user_role_assignments_revocation", "revoked_at_utc is null or revoked_at_utc >= assigned_at_utc");
                    table.ForeignKey(
                        name: "FK_user_role_assignments_roles_role_id_organization_id",
                        columns: x => new { x.role_id, x.organization_id },
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_role_assignments_users_user_id_organization_id",
                        columns: x => new { x.user_id, x.organization_id },
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_permissions_code",
                schema: "identity",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                schema: "identity",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ux_role_permissions_active",
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true,
                filter: "revoked_at_utc is null");

            migrationBuilder.CreateIndex(
                name: "ux_roles_organization_id_normalized_name",
                schema: "identity",
                table: "roles",
                columns: new[] { "organization_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_roles_organization_id_system_key",
                schema: "identity",
                table: "roles",
                columns: new[] { "organization_id", "system_key" },
                unique: true,
                filter: "system_key is not null");

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_role_id_organization_id",
                schema: "identity",
                table: "user_role_assignments",
                columns: new[] { "role_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_user_id_organization_id",
                schema: "identity",
                table: "user_role_assignments",
                columns: new[] { "user_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_role_assignments_user_id_revoked_at_utc",
                schema: "identity",
                table: "user_role_assignments",
                columns: new[] { "user_id", "revoked_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_user_role_assignments_active",
                schema: "identity",
                table: "user_role_assignments",
                columns: new[] { "user_id", "role_id" },
                unique: true,
                filter: "revoked_at_utc is null");

            migrationBuilder.Sql(
                """
                insert into identity.permissions (id, code, status, created_at_utc, retired_at_utc) values
                ('00000000-0000-4000-8000-000000000001', 'audit.view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000002', 'backups.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000003', 'branches.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000004', 'branches.view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000005', 'cash.pay_in', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000006', 'cash.pay_out', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000007', 'cash.safe_drop', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000008', 'cash.view_expected', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000009', 'catalog.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000000a', 'catalog.view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000000b', 'costs.view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000000c', 'devices.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000000d', 'discounts.configure', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000000e', 'expenses.approve', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000000f', 'expenses.create', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000010', 'expenses.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000011', 'expenses.view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000012', 'inventory.adjust', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000013', 'inventory.cost_view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000014', 'inventory.count', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000015', 'inventory.receive', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000016', 'inventory.transfer', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000017', 'inventory.view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000018', 'inventory.waste', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000019', 'kds.update', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000001a', 'kds.view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000001b', 'management.access', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000001c', 'orders.edit_submitted', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000001d', 'orders.reprint', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000001e', 'orders.view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000001f', 'pos.discount.basic', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000020', 'pos.discount.override', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000021', 'pos.refund', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000022', 'pos.sell', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000023', 'pos.void', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000024', 'prices.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000025', 'printers.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000026', 'purchasing.approve', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000027', 'purchasing.create', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000028', 'purchasing.receive', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000029', 'purchasing.view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000002a', 'recipes.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000002b', 'recipes.view', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000002c', 'reports.cost', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000002d', 'reports.export', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000002e', 'reports.finance', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000002f', 'reports.inventory', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000030', 'reports.sales', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000031', 'roles.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000032', 'settings.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000033', 'shifts.close', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000034', 'shifts.open', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000035', 'shifts.reopen', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000036', 'shifts.view_all', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000037', 'supplier_payments.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000038', 'suppliers.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-000000000039', 'users.manage', 'Active', '2026-07-21T00:00:00Z', null),
                ('00000000-0000-4000-8000-00000000003a', 'users.view', 'Active', '2026-07-21T00:00:00Z', null);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_role_assignments",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_users_id_organization_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "authorization_version",
                schema: "identity",
                table: "users");
        }
    }
}
#pragma warning restore CA1861
