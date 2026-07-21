using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Kalm.Organization.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExplicitUserBranchAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_branches_id_organization_id",
                schema: "organization",
                table: "branches",
                columns: new[] { "id", "organization_id" });

            migrationBuilder.CreateTable(
                name: "user_branch_access",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_branch_access", x => x.id);
                    table.UniqueConstraint("ak_user_branch_access_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_user_branch_access_scope", "scope in ('AssignedBranches', 'AllOrganizationBranches')");
                    table.ForeignKey(
                        name: "FK_user_branch_access_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "organization",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_branch_assignments",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_branch_assignments", x => x.id);
                    table.CheckConstraint("ck_user_branch_assignments_revocation", "revoked_at_utc is null or revoked_at_utc >= assigned_at_utc");
                    table.ForeignKey(
                        name: "FK_user_branch_assignments_branches_branch_id_organization_id",
                        columns: x => new { x.branch_id, x.organization_id },
                        principalSchema: "organization",
                        principalTable: "branches",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_branch_assignments_user_branch_access_access_id_organi~",
                        columns: x => new { x.access_id, x.organization_id },
                        principalSchema: "organization",
                        principalTable: "user_branch_access",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_access_organization_id",
                schema: "organization",
                table: "user_branch_access",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_branch_access_user_id",
                schema: "organization",
                table: "user_branch_access",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_assignments_access_id_organization_id",
                schema: "organization",
                table: "user_branch_assignments",
                columns: new[] { "access_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_assignments_branch_id_organization_id",
                schema: "organization",
                table: "user_branch_assignments",
                columns: new[] { "branch_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ux_user_branch_assignments_active",
                schema: "organization",
                table: "user_branch_assignments",
                columns: new[] { "access_id", "branch_id" },
                unique: true,
                filter: "revoked_at_utc is null");

            migrationBuilder.Sql(
                """
                create function organization.assert_user_branch_access_scope(target_access_id uuid)
                returns void
                language plpgsql
                as $$
                declare
                    target_scope varchar(40);
                    active_assignment_count integer;
                begin
                    select scope into target_scope
                    from organization.user_branch_access
                    where id = target_access_id;

                    if target_scope is null then
                        return;
                    end if;

                    select count(*) into active_assignment_count
                    from organization.user_branch_assignments
                    where access_id = target_access_id and revoked_at_utc is null;

                    if target_scope = 'AssignedBranches' and active_assignment_count < 1 then
                        raise exception using
                            errcode = '23514',
                            constraint = 'ck_user_branch_access_completed_scope',
                            message = 'AssignedBranches requires at least one active branch assignment';
                    end if;

                    if target_scope = 'AllOrganizationBranches' and active_assignment_count <> 0 then
                        raise exception using
                            errcode = '23514',
                            constraint = 'ck_user_branch_access_completed_scope',
                            message = 'AllOrganizationBranches cannot have active branch assignments';
                    end if;

                    return;
                end;
                $$;

                create function organization.enforce_user_branch_access_scope()
                returns trigger
                language plpgsql
                as $$
                begin
                    if tg_table_name = 'user_branch_access' then
                        perform organization.assert_user_branch_access_scope(coalesce(new.id, old.id));
                    else
                        perform organization.assert_user_branch_access_scope(coalesce(new.access_id, old.access_id));
                        if tg_op = 'UPDATE' and old.access_id is distinct from new.access_id then
                            perform organization.assert_user_branch_access_scope(old.access_id);
                        end if;
                    end if;

                    return null;
                end;
                $$;

                create constraint trigger trg_user_branch_access_completed_scope
                after insert or update on organization.user_branch_access
                deferrable initially deferred
                for each row execute function organization.enforce_user_branch_access_scope();

                create constraint trigger trg_user_branch_assignment_completed_scope
                after insert or update or delete on organization.user_branch_assignments
                deferrable initially deferred
                for each row execute function organization.enforce_user_branch_access_scope();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                drop trigger if exists trg_user_branch_assignment_completed_scope on organization.user_branch_assignments;
                drop trigger if exists trg_user_branch_access_completed_scope on organization.user_branch_access;
                drop function if exists organization.enforce_user_branch_access_scope();
                drop function if exists organization.assert_user_branch_access_scope(uuid);
                """);

            migrationBuilder.DropTable(
                name: "user_branch_assignments",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "user_branch_access",
                schema: "organization");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_branches_id_organization_id",
                schema: "organization",
                table: "branches");
        }
    }
}
#pragma warning restore CA1861
