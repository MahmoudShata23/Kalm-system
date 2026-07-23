# ADR 0009: Milestone 1A Minimal Audit Viewer

Date: 2026-07-23

## Status

Accepted

## Decision

- The Audit Viewer exposes exactly three read-only endpoints under `/api/v1/management/audit-logs`: bounded list, safe options, and detail. It adds no mutation, acknowledgement, replay, export, diagnostic, or public route.
- Every endpoint requires authoritative management authentication, `management.access`, and the existing `audit.view` permission. The fixed first-administrator permission set and permission catalogue are unchanged.
- Organization scope comes only from the server-maintained session. Users with `AllOrganizationBranches` may view organization audit records. Users with `AssignedBranches` see only records for their current operational branch IDs; branchless, inactive-branch, unauthorized-branch, and cross-organization records fail closed.
- List queries require a UTC interval of at most 90 days, default to 25 rows, permit at most 100 rows, and sort by `OccurredAtUtc DESC, Id DESC`. Protected cursors bind the organization, current operational branch scope, normalized filters, page size, timestamp, ID, and direction. Cursor paging is keyset-only and supports stable next/previous navigation.
- List projection never reads raw metadata. Detail metadata passes through an explicit action-specific presenter. Unknown keys and malformed JSON are omitted by default. Passwords, PINs, hashes, salts, challenges, credentials, tokens, cookies, tickets, session IDs, request bodies, network identifiers, user agents, exception text, and unnecessary personal data are never returned.
- Actor display names are resolved in one organization-scoped Identity query. Branch hints are resolved in one active, accessible Organization query. Audit queries use `AsNoTracking`, cancellation tokens, and bounded projections; reads append no audit event and call no save operation.
- Every audit response carries `Cache-Control: no-store, no-cache, max-age=0`, `Pragma: no-cache`, and `Expires: 0`.
- Angular keeps filters, cursors, list rows, and details only in component-scoped signals. It writes no audit data to localStorage, sessionStorage, IndexedDB, service-worker caches, or unrelated URLs. Protected bilingual list and deep-link detail routes provide keyboard access, visible focus, safe ID copy actions, and 44px targets.
- Existing indexes do not begin with `OrganizationId` and do not include the ID keyset tie-breaker. Audit migration `20260723010000_AddAuditViewerIndexes` therefore adds only `(OrganizationId, OccurredAtUtc DESC, Id DESC)` and `(OrganizationId, BranchId, OccurredAtUtc DESC, Id DESC)`.

## Consequences

The viewer is intentionally operational and minimal. Retention automation, raw metadata exploration, arbitrary search, polling, analytics, alerts, and export remain excluded. Audit immutability and all existing write transactions are unchanged.
