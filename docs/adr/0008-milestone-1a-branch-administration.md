# ADR 0008: Milestone 1A Branch Administration

Date: 2026-07-22

## Status

Accepted

## Decision

- Branch Administration exposes exactly bounded list/search/status filtering, detail, create, complete safe-field PUT, activate, and deactivate operations under `/api/v1/management/branches`.
- Viewing requires `management.access` and `branches.view`; mutations require `management.access` and `branches.manage`, antiforgery validation, and the approved write rate limit. Organization scope is derived only from the authoritative session, and cross-organization identifiers are indistinguishable from missing records.
- The existing Branch aggregate remains authoritative. Slice 7 edits only name, organization-reserved normalized code, locale, IANA time zone, and minute-precision business-day rollover. It adds no address, tax, service-charge, receipt, sequence, delete, reassignment, or recovery behavior.
- Strong quoted ETags protect update, activation, and deactivation. Missing, malformed, weak, wildcard, multiple, and stale preconditions return stable 428, 400, and 412 Problem Details. No-op writes preserve the version and append no audit event.
- Activation changes only Branch status. Deactivation fails closed while non-revoked devices, active device credentials, active device-bound sessions, or active explicit user-branch assignments remain. Conflict details contain counts only and never identities, session IDs, credentials, organization IDs, or request bodies.
- Branch-dependent Device and User Administration writes share a transaction-scoped PostgreSQL advisory lock with deactivation. Consequently concurrent dependency creation and deactivation cannot both commit. Rejected deactivation never revokes, deletes, reassigns, or otherwise changes a dependency.
- Successful Organization and immutable Audit writes share one explicit Npgsql connection and local transaction. Identity participates only for device-bound session dependency validation. Every context enlists with `UseTransactionAsync`; no ambient or distributed transaction is used.
- Slice 7 adds only Audit migration `20260722212000_ExtendBranchAdministrationAuditActions`. It follows Slice 6 and extends the immutable action constraint with `BranchActivated`, `BranchDeactivated`, and `BranchAdministrationRejected`. Organization and Identity require no schema migration.
- The Angular feature provides protected list, create, and detail routes, typed API access, signal state, typed reactive forms, safe dependency-conflict presentation, and stale-ETag recovery that preserves the user draft in Arabic RTL and English LTR.

## Consequences

Inactive branch codes remain reserved by the existing organization-scoped unique constraint. Deactivation never cascades to users, devices, credentials, or sessions; operators must resolve those dependencies through their owning administration workflows before trying again.
