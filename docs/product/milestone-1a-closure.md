# Milestone 1A Closure Review

Date: 2026-07-23

## Decision

Milestone 1A is complete when the repository gates recorded below pass. All mandatory Milestone 1A capabilities are implemented; explicitly deferred items are outside the approved 1A scope and remain scheduled for later milestones.

| # | Exit item | Status | Evidence |
|---:|---|---|---|
| 1 | Organization and Branch foundation | Complete | Organization-owned aggregate persistence, UUID keys, normalized branch codes, lifecycle invariants, PostgreSQL constraints, clean/upgrade migrations, and ADRs 0003/0008. |
| 2 | Management bootstrap and password authentication | Complete | Non-public operational bootstrap, adaptive password hashing, generic login failures, opaque server sessions, logout, `/auth/me`, and authentication integration tests. |
| 3 | CSRF, recent reauthentication, rate limiting, and secure cookies | Complete | Strict host cookies, antiforgery validation, HTTP-boundary rate limits, recent-reauthentication policies, and authentication/user/device endpoint tests. |
| 4 | Roles, permissions, and database-authoritative authorization | Complete | Stable 58-code catalogue including `audit.view`, database-materialized grants, authoritative per-request resolution, server policies, and ADRs 0004/0005. |
| 5 | Branch scope and last-management-access protection | Complete | Explicit assigned/all-branch scope, active operational-branch resolution, deferred PostgreSQL invariants, advisory locking, and concurrency tests. |
| 6 | Role Administration | Complete | Bounded list/detail/create/update/archive/catalogue endpoints, strong ETags, protected system role, atomic audit, bilingual UI, and ADR 0005. |
| 7 | User Administration and secure provisioning | Complete | Suspended-first provisioning, complete role/branch replacement, activation safeguards, password setup/reset returning 204, session revocation, audit rollback, and ADR 0006. |
| 8 | Branch Administration | Complete | Authorized list/detail/create/update/activate/deactivate, dependency-blocked deactivation, concurrency-safe dependency creation, no cascade, and ADR 0008. |
| 9 | Device Administration and secure pairing | Complete | Registered device lifecycle, single-use hashed challenges, bounded plaintext response, rotating HttpOnly device cookie, revocation, and ADR 0007. |
| 10 | Employee PIN setup/login and lockout | Complete | Six-digit purpose-separated adaptive PIN hashing, generic failures, dummy verification, HTTP and persistent rate protection, concurrency-safe lockout, and ADR 0007. |
| 11 | Device-bound sessions, locking, and employee switching | Complete | Server-side device/user/branch/security/authorization freshness, workstation lock, preserved device pairing, and next-request revocation checks. |
| 12 | Immutable atomic audit writing | Complete | Append-only Audit module, PostgreSQL update/delete rejection, explicit cross-context local transactions, safe semantic payloads, rollback tests, and ADR 0003. |
| 13 | Minimal authorized Audit Viewer | Complete | Exact three GET endpoints, `management.access` plus `audit.view`, tenant/branch scope, 90-day bound, protected keyset cursors, allowlisted detail metadata, no-store responses, bilingual UI, and ADR 0009. |
| 14 | Migration immutability and exact upgrade paths | Complete | Released byte baselines, standard designers, separate histories, clean migration, exact Slice 7 → Slice 8 upgrade, and all-context no-pending-model checks. |
| 15 | Arabic RTL and English LTR management experiences | Complete | Standalone localized management/login/roles/users/devices/branches/audit flows, semantic layouts, direction switching, keyboard navigation, visible focus, and Playwright coverage. |
| 16 | CI, security scans, accessibility, and operational documentation | Complete | GitHub Actions gates, fail-closed package audits, Gitleaks, Trivy policy, Playwright accessibility coverage, Docker/local-development guidance, ADRs, OpenAPI snapshot, and this closure review. |
| 17 | Explicit Milestone 1A exclusions remain absent | Deferred by approved scope | MFA, catalog/POS, printers, retention cleanup, audit export/search/streaming/analytics, recovery/bypass/fixture APIs, and destructive audit operations are not part of Milestone 1A and were not introduced. |

## Final gate evidence

The completion report for Slice 8 records the exact formatter, build, backend, PostgreSQL, migration, OpenAPI, Angular, Playwright, NuGet, npm, Trivy, Gitleaks, and diff-check results. A failing mandatory gate changes the affected row to `Partial` or `Blocked` and prevents closure.

## Milestone boundary

Milestone 1A closure does not close Milestone 1. Milestone 1B retains catalog, pricing, recipes, modifiers, availability, POS menu behavior, and the approved Kalm seed menu. Later Release 1 milestones retain POS transactions, shifts, payments, inventory, purchasing, expenses, reports, printing, and backup operations.
