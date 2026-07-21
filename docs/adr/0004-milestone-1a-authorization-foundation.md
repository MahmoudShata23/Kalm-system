# ADR 0004: Milestone 1A Authorization Foundation

Date: 2026-07-21

## Status

Accepted

## Context

Milestone 1A Slice 2 authenticates management users with an opaque server-maintained session, but authentication alone grants no management permission. Slice 3 must add permission-based authorization, explicit branch access, trusted first-administrator provisioning, and a protected management shell without exposing authorization administration APIs.

## Decision

- `management.access` is the sole permission that permits entry to the management shell. Role names and business permissions are never substitutes.
- Permission definitions are a controlled hybrid: stable codes are centralized and compile-time discoverable; frozen migration literals materialize them in Identity for referential integrity. Unknown, malformed, retired, or uncompiled codes grant nothing.
- The first-administrator permission set is the explicit version `2026.07.slice3.v1` and contains the complete currently approved IAM-004 catalogue. Later permission additions are not silently granted.
- Identity owns permissions, organization-scoped roles, role grants, user-role assignments, and `User.AuthorizationVersion`.
- Organization owns `AssignedBranches` and `AllOrganizationBranches`, explicit branch assignments, same-organization branch constraints, and operational status resolution.
- `AssignedBranches` requires at least one active assignment at commit. `AllOrganizationBranches` requires none. Organization enforces the completed aggregate with deferred PostgreSQL constraint triggers.
- Clean bootstrap defaults to the initially created assigned branch. All-organization access requires the explicit `--all-organization-branches` CLI flag.
- Existing Slice 2 users are never elevated by migration. The trusted `provision-first-administrator` CLI requires an exact active user, active password credential, and explicit scope.
- Provisioning uses an organization-scoped system role key, `UNIQUE (organization_id, system_key)`, a transaction-scoped PostgreSQL advisory lock, and one local transaction shared by Organization, Identity, and Audit contexts.
- Authorization is resolved from authoritative database state on every authenticated request. There is no authorization cache and no authorization data or version in the cookie.
- Ordinary permission removal does not revoke authentication. The next request loses authorization and protected policies return forbidden.
- `KalmPolicies.ManagementAccess` is backed only by `management.access`. Reusable permission and operational-branch handlers consume a server-derived immutable request snapshot.
- Operational branch authorization requires an active Organization, active Branch, and inclusion through the explicit scope.
- `/auth/me` returns sorted effective permission codes and safe branch scope details. It does not return roles, role IDs, system keys, sessions, credentials, audit data, or authorization versions.
- The Angular guard is a navigation aid. Authenticated users without `management.access` remain signed in and see a localized access-denied experience.
- Authorization provisioning has no HTTP endpoint, diagnostic endpoint, public fixture, or CRUD surface.

## Transaction and Audit Consequences

Sensitive authorization changes and their successful audit entries commit together on one explicit local PostgreSQL transaction. Expected operational provisioning failures may be audited only after the failed mutation transaction rolls back. If Audit is unavailable, the command fails visibly.

Audit data is semantic and excludes passwords, hashes, credentials, cookies, session identifiers, CSRF tokens, authentication tickets, raw input, and unnecessary personal data.

## Migration Consequences

Slice 3 adds exactly three forward-only migrations owned by the existing Identity, Organization, and Audit histories. Previously released migrations remain byte-identical. Permission rows are migration data, but roles, assignments, users, and production scope are operational data and are never migration-seeded.
