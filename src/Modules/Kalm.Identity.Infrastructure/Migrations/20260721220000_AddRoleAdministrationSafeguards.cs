using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Kalm.Identity.Infrastructure.Migrations;

public partial class AddRoleAdministrationSafeguards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "ck_roles_archived_state",
            schema: "identity",
            table: "roles",
            sql: "(status = 'Active' and archived_at_utc is null) or (status = 'Archived' and archived_at_utc is not null)");

        migrationBuilder.CreateIndex(
            name: "ix_roles_organization_status_normalized_name_id",
            schema: "identity",
            table: "roles",
            columns: new[] { "organization_id", "status", "normalized_name", "id" });

        migrationBuilder.CreateIndex(
            name: "ix_user_role_assignments_role_id_user_id_active",
            schema: "identity",
            table: "user_role_assignments",
            columns: new[] { "role_id", "user_id" },
            filter: "revoked_at_utc is null");

        migrationBuilder.Sql(
            """
            create function identity.assert_active_role_has_permission(target_role_id uuid)
            returns void
            language plpgsql
            as $$
            declare
                target_status character varying;
            begin
                select status into target_status
                from identity.roles
                where id = target_role_id;

                if target_status = 'Active'
                   and not exists (
                       select 1
                       from identity.role_permissions grant_row
                       where grant_row.role_id = target_role_id
                         and grant_row.revoked_at_utc is null)
                then
                    raise exception using
                        errcode = '23514',
                        constraint = 'ck_identity_active_role_has_permission',
                        message = 'Active role requires an active permission';
                end if;
            end;
            $$;

            create function identity.enforce_active_role_has_permission()
            returns trigger
            language plpgsql
            as $$
            begin
                if tg_table_name = 'roles' then
                    perform identity.assert_active_role_has_permission(coalesce(new.id, old.id));
                else
                    perform identity.assert_active_role_has_permission(coalesce(new.role_id, old.role_id));
                    if tg_op = 'UPDATE' and old.role_id is distinct from new.role_id then
                        perform identity.assert_active_role_has_permission(old.role_id);
                    end if;
                end if;
                return null;
            end;
            $$;

            create constraint trigger trg_roles_active_permission
            after insert or update of status on identity.roles
            deferrable initially deferred
            for each row execute function identity.enforce_active_role_has_permission();

            create constraint trigger trg_role_permissions_active_role
            after insert or update or delete on identity.role_permissions
            deferrable initially deferred
            for each row execute function identity.enforce_active_role_has_permission();

            create function identity.effective_management_user_count(target_organization_id uuid)
            returns bigint
            language sql
            stable
            as $$
                select count(distinct user_row.id)
                from identity.users user_row
                join identity.user_role_assignments assignment
                  on assignment.user_id = user_row.id
                 and assignment.organization_id = user_row.organization_id
                 and assignment.revoked_at_utc is null
                join identity.roles role_row
                  on role_row.id = assignment.role_id
                 and role_row.organization_id = assignment.organization_id
                 and role_row.status = 'Active'
                join identity.role_permissions grant_row
                  on grant_row.role_id = role_row.id
                 and grant_row.revoked_at_utc is null
                join identity.permissions permission_row
                  on permission_row.id = grant_row.permission_id
                 and permission_row.status = 'Active'
                 and permission_row.code = 'management.access'
                where user_row.organization_id = target_organization_id
                  and user_row.status = 'Active';
            $$;

            create function identity.assert_last_management_access(target_organization_id uuid)
            returns void
            language plpgsql
            as $$
            declare
                lock_key text;
            begin
                if target_organization_id is null then
                    return;
                end if;
                lock_key := 'kalm.identity.management-access:' || target_organization_id::text;
                perform pg_advisory_xact_lock(hashtextextended(lock_key, 0));
                if identity.effective_management_user_count(target_organization_id) = 0 then
                    raise exception using
                        errcode = '23514',
                        constraint = 'ck_identity_last_management_access',
                        message = 'Organization requires an active management access user';
                end if;
            end;
            $$;

            create function identity.enforce_last_management_access()
            returns trigger
            language plpgsql
            as $$
            declare
                target_organization_id uuid;
                affected boolean := false;
            begin
                if tg_table_name = 'users' then
                    affected := old.status = 'Active' and (tg_op = 'DELETE' or new.status <> 'Active');
                    target_organization_id := old.organization_id;
                    if affected and not exists (
                        select 1
                        from identity.user_role_assignments assignment
                        join identity.roles role_row on role_row.id = assignment.role_id
                        join identity.role_permissions grant_row on grant_row.role_id = role_row.id
                        join identity.permissions permission_row on permission_row.id = grant_row.permission_id
                        where assignment.user_id = old.id
                          and assignment.revoked_at_utc is null
                          and role_row.status = 'Active'
                          and grant_row.revoked_at_utc is null
                          and permission_row.status = 'Active'
                          and permission_row.code = 'management.access') then
                        affected := false;
                    end if;
                elsif tg_table_name = 'roles' then
                    affected := old.status = 'Active' and (tg_op = 'DELETE' or new.status <> 'Active');
                    target_organization_id := old.organization_id;
                    if affected and not exists (
                        select 1
                        from identity.role_permissions grant_row
                        join identity.permissions permission_row on permission_row.id = grant_row.permission_id
                        where grant_row.role_id = old.id
                          and permission_row.code = 'management.access') then
                        affected := false;
                    end if;
                elsif tg_table_name = 'user_role_assignments' then
                    affected := old.revoked_at_utc is null
                        and (tg_op = 'DELETE' or new.revoked_at_utc is not null
                             or new.role_id is distinct from old.role_id
                             or new.user_id is distinct from old.user_id
                             or new.organization_id is distinct from old.organization_id);
                    target_organization_id := old.organization_id;
                    if affected and not exists (
                        select 1
                        from identity.role_permissions grant_row
                        join identity.permissions permission_row on permission_row.id = grant_row.permission_id
                        where grant_row.role_id = old.role_id
                          and permission_row.code = 'management.access') then
                        affected := false;
                    end if;
                elsif tg_table_name = 'role_permissions' then
                    affected := old.revoked_at_utc is null
                        and (tg_op = 'DELETE' or new.revoked_at_utc is not null
                             or new.role_id is distinct from old.role_id
                             or new.permission_id is distinct from old.permission_id);
                    select role_row.organization_id into target_organization_id
                    from identity.roles role_row where role_row.id = old.role_id;
                    if affected and not exists (
                        select 1 from identity.permissions permission_row
                        where permission_row.id = old.permission_id
                          and permission_row.code = 'management.access') then
                        affected := false;
                    end if;
                elsif tg_table_name = 'permissions' then
                    affected := old.status = 'Active' and old.code = 'management.access'
                        and (tg_op = 'DELETE' or new.status <> 'Active' or new.code <> 'management.access');
                    if affected then
                        for target_organization_id in
                            select distinct role_row.organization_id
                            from identity.role_permissions grant_row
                            join identity.roles role_row on role_row.id = grant_row.role_id
                            where grant_row.permission_id = old.id
                            order by role_row.organization_id
                        loop
                            perform identity.assert_last_management_access(target_organization_id);
                        end loop;
                        return null;
                    end if;
                end if;

                if affected then
                    perform identity.assert_last_management_access(target_organization_id);
                end if;
                return null;
            end;
            $$;

            create constraint trigger trg_users_last_management_access
            after update of status or delete on identity.users
            deferrable initially deferred
            for each row execute function identity.enforce_last_management_access();

            create constraint trigger trg_roles_last_management_access
            after update of status or delete on identity.roles
            deferrable initially deferred
            for each row execute function identity.enforce_last_management_access();

            create constraint trigger trg_user_role_assignments_last_management_access
            after update or delete on identity.user_role_assignments
            deferrable initially deferred
            for each row execute function identity.enforce_last_management_access();

            create constraint trigger trg_role_permissions_last_management_access
            after update or delete on identity.role_permissions
            deferrable initially deferred
            for each row execute function identity.enforce_last_management_access();

            create constraint trigger trg_permissions_last_management_access
            after update or delete on identity.permissions
            deferrable initially deferred
            for each row execute function identity.enforce_last_management_access();

            do $$
            declare
                target_role_id uuid;
            begin
                for target_role_id in
                    select id from identity.roles where status = 'Active' order by id
                loop
                    perform identity.assert_active_role_has_permission(target_role_id);
                end loop;
            end;
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            drop trigger if exists trg_permissions_last_management_access on identity.permissions;
            drop trigger if exists trg_role_permissions_last_management_access on identity.role_permissions;
            drop trigger if exists trg_user_role_assignments_last_management_access on identity.user_role_assignments;
            drop trigger if exists trg_roles_last_management_access on identity.roles;
            drop trigger if exists trg_users_last_management_access on identity.users;
            drop function if exists identity.enforce_last_management_access();
            drop function if exists identity.assert_last_management_access(uuid);
            drop function if exists identity.effective_management_user_count(uuid);
            drop trigger if exists trg_role_permissions_active_role on identity.role_permissions;
            drop trigger if exists trg_roles_active_permission on identity.roles;
            drop function if exists identity.enforce_active_role_has_permission();
            drop function if exists identity.assert_active_role_has_permission(uuid);
            """);

        migrationBuilder.DropIndex(
            name: "ix_user_role_assignments_role_id_user_id_active",
            schema: "identity",
            table: "user_role_assignments");
        migrationBuilder.DropIndex(
            name: "ix_roles_organization_status_normalized_name_id",
            schema: "identity",
            table: "roles");
        migrationBuilder.DropCheckConstraint(
            name: "ck_roles_archived_state",
            schema: "identity",
            table: "roles");
    }
}
#pragma warning restore CA1861
