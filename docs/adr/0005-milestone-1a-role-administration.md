# ADR 0005: Milestone 1A Role Administration

Date: 2026-07-21

## Status

Accepted

## Context

Slice 3 resolves permissions from database-authoritative organization-scoped roles on every request but intentionally exposes no authorization administration surface. Slice 4 must allow safe role and permission-set maintenance without making the Angular client authoritative, exposing user assignments, or permitting concurrent changes to remove the final management-capable user.

## Decision

- The role administration API contains exactly list, detail, create, complete PUT replacement, archive action, and permission catalogue endpoints under `/api/v1/management`.
- Every endpoint requires both `management.access` and `roles.manage`; unsafe operations also require antiforgery validation.
- Organization scope comes only from the authoritative request session. Cross-organization identifiers are indistinguishable from missing roles.
- Role versions are exposed as strong quoted ETags. PUT and archive require one exact `If-Match`; missing, malformed, and stale values return 428, 400, and 412 respectively. There is no wildcard or force overwrite.
- Active roles require at least one non-revoked permission. PostgreSQL enforces the completed aggregate through deferred constraint triggers so creation and complete replacement may have temporary intermediate states.
- Protected system roles reject normal rename, archive, and permission replacement in the domain. Their raw system keys are never returned. Recovery uses only the explicit trusted CLI path and the fixed versioned first-administrator permission set.
- Assigned roles cannot be archived. Archiving never revokes or changes assignments.
- One PostgreSQL function counts effective active `management.access` users. Destructive authorization triggers acquire an organization advisory lock and reject a completed state of zero with the named `ck_identity_last_management_access` violation.
- Successful role mutations and their audit entries share one local Npgsql transaction across Identity and Audit. A last-management rejection is audited only after the failed mutation transaction has rolled back.
- Permission presentation metadata is immutable compiled bilingual metadata. Authorization semantics continue to use only compiled codes and active database rows.
- Actual permission diffs increment every actively assigned user's `AuthorizationVersion` once in the same transaction. Authorization remains uncached and takes effect on the next request.

## Consequences

Slice 4 adds one Identity migration and one Audit migration. It adds no Organization migration, user administration, assignment API, permission catalogue administration, role deletion, or recovery HTTP endpoint. The Angular guard and navigation remain usability aids; the server policy remains authoritative.
