# Kalm Implementation Status

Last updated: 2026-07-22

Milestone 1A Slice 6 implements organization-scoped device administration, secure one-time pairing, administrator-managed employee PINs, device-bound PIN login, workstation locking and employee switching, bilingual Angular flows, immutable audit events, and additive Organization/Identity/Audit migrations.

## Current Milestone

Milestone 1A - Identity, Organization, Branches, Devices, and Audit

## Requirement Checklist

| Requirement | Status | Notes |
|---|---|---|
| SRD 8.1 Approved stack | Verified | .NET SDK 10.0.302, Angular 22.0.7, PrimeNG 22.0.0, @primeuix/themes 3.0.0, PrimeIcons 8.0.0, Node 24.18.0 with npm 11.16.0, Docker 29.6.1, and Compose v5.3.0. Material/CDK removed by ADR 0002. |
| SRD 8.2 Modular monolith | Implemented | API composition root, shared kernel, building blocks, and Identity skeleton contracts created. |
| SRD 9 Repository structure | Implemented | Foundation folders created. Future modules are intentionally not generated as empty projects. |
| SRD 10.1 General coding rules | Implemented | Nullable, analyzers, strict TypeScript, central package pinning, and warnings-as-errors configured. |
| SRD 10.2 Backend rules | Verified | Problem Details, correlation IDs, injected clock, EF Core PostgreSQL context, immutable historical migration, and clean/upgrade migration tests pass. |
| SRD 10.3 Frontend rules | Verified | Standalone Angular shell, strict TypeScript, signals, zoneless configuration, PrimeNG Styled Mode with KalmPreset, and Arabic/English localized copy with LTR/RTL and keyboard-accessibility E2E checks pass. |
| SRD 17.8 Observability | Implemented for M0 | Correlation ID middleware and health endpoints added. Full OpenTelemetry is deferred beyond Milestone 0. |
| SRD 21 Testing strategy | Verified locally through Slice 5 | Domain, policy, migration, user/role transactions, provisioning, authentication, Angular guard/data-access, and Playwright coverage includes PostgreSQL transactions, rollback, deferred constraints, authorization freshness, and concurrency. |
| SRD 22.2 Local developer experience | Verified | Docker Compose, health checks, migration validation, OpenAPI snapshot commands, guarded development reset, README, and local development guide are present. The reset intentionally seeds no users, credentials, or cafe business data. |
| SRD 22.3 CI pipeline | Implemented; CI execution pending | GitHub Actions includes restore, fail-closed NuGet high/critical audit policy, format, build, tests, migration/OpenAPI checks, npm audit, Gitleaks, Trivy, and Playwright browser provisioning. |
| SRD 23 Milestone 0 | Verified locally; CI execution pending | Foundation implementation only. |
| SRD 23 Milestone 1A Slice 1 | Completed and merged | Organization/Branch persistence and immutable Audit foundation; no runtime administration route or Angular screen is exposed. |
| Milestone 1A Slice 2 | Implemented and verified locally | Operational bootstrap, management password login, server-maintained sessions, secure cookies, CSRF, logout, anonymous-safe `/auth/me`, and bilingual PrimeNG login only. |
| Milestone 1A Slice 3 | Implemented and verified locally | Versioned permissions, roles and grants, explicit branch access, trusted administrator authorization provisioning, database-authoritative policies, enriched `/auth/me`, and the protected bilingual management shell. No authorization CRUD or provisioning HTTP endpoint is exposed. |
| Milestone 1A Slice 4 | Implemented and verified locally | Organization-scoped role CRUD-without-delete, complete permission replacement, strong ETags, system-role protection, assigned-role archive blocking, database-safe active-role and last-management-access invariants, trusted recovery CLI, and the bilingual protected Roles experience. |
| Milestone 1A Slice 5 | Implemented and verified locally | Bounded user administration, suspended-first provisioning, complete historical role/branch replacement, activation safeguards, session-revoking suspension/password changes, strong ETags, atomic audit, and the bilingual protected Users experience. |
| IAM-001 User accounts | Implemented for management provisioning | Username/email normalization, safe profile editing, active/suspended lifecycle, password credential state, multiple roles, explicit branch scope, and soft historical assignments are persisted and protected. PIN credentials remain deferred. |
| IAM-002 Authentication modes | Partially implemented | Management password authentication is implemented. PIN and device authentication remain deferred. |
| IAM-004 Roles and permissions | Implemented through Slice 4 | Stable code-owned/database-materialized permission catalogue, organization-scoped multi-role grants, database-authoritative resolution, fixed server policies, protected role administration, and bilingual presentation metadata. |
| IAM-005 Branch scope | Implemented through Slice 5 | Explicit AssignedBranches and AllOrganizationBranches are Organization-owned; user administration performs complete replacement while completed-scope and same-organization invariants remain database enforced. |

## Milestone 1 Subdivision

- Milestone 1A delivers Identity, Organization, Branches, Devices, Authentication, Authorization, Sessions, PIN login, and immutable audit writing.
- Milestone 1B retains the original catalog scope: categories, products, variants, prices, modifiers, availability, POS menu endpoint, catalog screens, and the Kalm seed menu.
- This subdivision does not remove or weaken the original Milestone 1 catalog requirements or exit criterion; the complete original exit criterion is met only after Milestone 1B.

## Milestone 0 Implementation Plan

1. Create solution and repository structure.
2. Add API foundation: Problem Details, correlation IDs, health checks, auth skeleton, EF Core platform schema, and initial migration.
3. Add shared kernel/building blocks with framework-independent primitives.
4. Add tests for business-date calculation, result primitives, API shell behavior, persistence model mapping, and architecture boundaries.
5. Add Angular shell with Kalm design tokens and bilingual direction switching.
6. Add Docker Compose, CI, README, and setup documentation.
7. Run formatting, linting, builds, and tests; document blockers from missing local runtime dependencies.

## Foundation Stack Amendment

- ADR 0002 supersedes ADR 0001's Material/CDK toolkit decision.
- PrimeNG 22.0.0, @primeuix/themes 3.0.0, and PrimeIcons 8.0.0 are exact production pins.
- `KalmPreset` extends Aura and maps the Kalm palette into primitive, semantic, surface, form-field, focus-ring, radius, and selected component tokens.
- No direct Angular CDK package or application import is retained. PrimeNG 22 requires `@angular/cdk`, so npm resolves CDK 22.0.5 transitively; this is documented in ADR 0002.
- Test-only Playwright fixtures cover PrimeNG accessibility and keyboard behavior without exposing a production or normal-development application route.

## Validation Notes and Remaining Blockers

- PowerShell blocks the unsigned `npm.ps1` shim; use `npm.cmd` on this Windows machine. No execution-policy change is required.
- `npm audit --audit-level=high` passes after the lockfile-only transitive `fast-uri` 3.1.4 security patch. One low and six moderate development-tool findings remain; no high/critical frontend advisory is present, and their reported force-fix would downgrade the approved Angular major.
- The Slice 5 PrimeNG production build succeeds at 573.01 kB initial raw size (127.42 kB estimated transfer). The existing 500 kB warning fires by 73.01 kB and the 750 kB error budget passes.
- Gitleaks v8.30.1 reports no leaks in committed history or the current worktree. Trivy v0.70.0 reports no HIGH/CRITICAL findings for the pinned PostgreSQL image under the exact CI policy and approved `.trivyignore` file.
- NuGet restore audits direct and transitive packages with `NuGetAuditLevel=high`; `NU1903` and `NU1904` are errors. The local and CI command is `deploy\\scripts\\check-nuget-audit.cmd`, and the online audit passes.
- All previously released migrations remain immutable. Slice 5 adds only `20260722200000_ExtendUserAdministrationAuditActions` to Audit; no Identity, Organization, or Platform migration is required.
- PostgreSQL remains pinned to the official 18.4 Debian image (`postgres:18.4@sha256:32ca0af8e77bfb8c6610c488e4691f83f972a3e9e64d3b02facf3ab111ad5500`). Clean and exact upgrade paths, constraints, deferred triggers, concurrent provisioning, authorization freshness, and cross-context rollback pass against PostgreSQL 18.4.
- PostgreSQL-backed integration tests create and drop isolated test databases using the `KALM_TEST_POSTGRES_ADMIN` connection string when supplied, or the local Docker defaults otherwise.
