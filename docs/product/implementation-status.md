# Kalm Implementation Status

Last updated: 2026-07-23

Milestone 1B Slice 1 opens the Catalog bounded context with organization-scoped Categories, Products, and Variants; aggregate-safe lifecycle and ordering; bilingual management screens; and additive Catalog/Audit migrations. Milestone 1A remains complete and immutable.

## Current Milestone

Milestone 1B - Catalog

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
| SRD 21 Testing strategy | Verified locally through Slice 8 | Domain, policy, migration, user/role/device/branch/audit transactions, provisioning, authentication, Angular data access, and Playwright coverage includes PostgreSQL transactions, rollback, deferred constraints, authorization freshness, keyset pagination, accessibility, and concurrency. |
| SRD 22.2 Local developer experience | Verified | Docker Compose, health checks, migration validation, OpenAPI snapshot commands, guarded development reset, README, and local development guide are present. The reset intentionally seeds no users, credentials, or cafe business data. |
| SRD 22.3 CI pipeline | Implemented and locally mirrored | GitHub Actions includes restore, fail-closed NuGet high/critical audit policy, format, build, tests, migration/OpenAPI checks, npm audit, Gitleaks, Trivy, and Playwright browser provisioning. Slice 8 runs the equivalent full local gates before handoff. |
| SRD 23 Milestone 0 | Verified locally; CI execution pending | Foundation implementation only. |
| SRD 23 Milestone 1A Slice 1 | Completed and merged | Organization/Branch persistence and immutable Audit foundation; no runtime administration route or Angular screen is exposed. |
| Milestone 1A Slice 2 | Implemented and verified locally | Operational bootstrap, management password login, server-maintained sessions, secure cookies, CSRF, logout, anonymous-safe `/auth/me`, and bilingual PrimeNG login only. |
| Milestone 1A Slice 3 | Implemented and verified locally | Versioned permissions, roles and grants, explicit branch access, trusted administrator authorization provisioning, database-authoritative policies, enriched `/auth/me`, and the protected bilingual management shell. No authorization CRUD or provisioning HTTP endpoint is exposed. |
| Milestone 1A Slice 4 | Implemented and verified locally | Organization-scoped role CRUD-without-delete, complete permission replacement, strong ETags, system-role protection, assigned-role archive blocking, database-safe active-role and last-management-access invariants, trusted recovery CLI, and the bilingual protected Roles experience. |
| Milestone 1A Slice 5 | Implemented and verified locally | Bounded user administration, suspended-first provisioning, complete historical role/branch replacement, activation safeguards, session-revoking suspension/password changes, strong ETags, atomic audit, and the bilingual protected Users experience. |
| Milestone 1A Slice 6 | Implemented and verified locally | Device administration, secure one-time pairing, employee PIN setup/reset/login, lockout, device-bound sessions, locking, switching, and atomic audit. |
| Milestone 1A Slice 7 | Implemented and verified locally | Branch administration, strong ETags, dependency-safe activation/deactivation, concurrency locks, bilingual UI, and corrected additive audit migration ordering. |
| Milestone 1A Slice 8 | Implemented and verified locally | Exact read-only Audit Viewer API, protected tenant/branch scope, bounded keyset pagination, allowlisted detail metadata, bilingual UI, additive viewer indexes, OpenAPI/ADR/tests, and closure evidence. |
| Milestone 1B Slice 1 | Implemented and verified locally | Dedicated Catalog module; organization-scoped Category/Product/Variant persistence; strong ETags; deferred PostgreSQL active-category/active-variant invariants; aggregate Variant editing; atomic Audit; and bilingual protected Catalog screens. |
| IAM-001 User accounts | Implemented for Milestone 1A | Username/email normalization, safe profile editing, lifecycle, password and PIN credentials, multiple roles, explicit branch scope, soft historical assignments, and secure provisioning are persisted and protected. |
| IAM-002 Authentication modes | Implemented for Milestone 1A | Management password login, paired-device employee PIN login, workstation lock, and employee switching are implemented. Optional MFA is explicitly deferred by ADR 0003. |
| IAM-004 Roles and permissions | Implemented through Slice 4 | Stable code-owned/database-materialized permission catalogue, organization-scoped multi-role grants, database-authoritative resolution, fixed server policies, protected role administration, and bilingual presentation metadata. |
| IAM-005 Branch scope | Implemented through Slice 5 | Explicit AssignedBranches and AllOrganizationBranches are Organization-owned; user administration performs complete replacement while completed-scope and same-organization invariants remain database enforced. |
| AUD-001/002/003 Audit | Implemented through Slice 8 | Sensitive Slice 1A events use immutable atomic semantic audit writes; the viewer is authorized, bounded, branch-scoped, metadata-allowlisted, and returns no raw JSON or secrets. |
| CAT-001/002 Catalog foundation | Implemented through Milestone 1B Slice 1 | Organization-scoped bilingual Categories, Products, and Variants with reserved codes, lifecycle, deterministic ordering, bounded search/filtering, PostgreSQL invariants, and no prices or operational seed data. |

## Milestone 1 Subdivision

- Milestone 1A delivers Identity, Organization, Branches, Devices, Authentication, Authorization, Sessions, PIN login, and immutable audit writing.
- Milestone 1B retains the original catalog scope: categories, products, variants, prices, modifiers, availability, POS menu endpoint, catalog screens, and the Kalm seed menu.
- Milestone 1B Slice 1 completes only the shared Category/Product/Variant foundation. Prices, modifiers, availability, POS menu projection, and the Kalm seed menu remain open.
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
- The Slice 8 PrimeNG production build succeeds at 587.38 kB initial raw size (131.37 kB estimated transfer). The existing 500 kB warning fires by 87.38 kB and the 750 kB error budget passes.
- Gitleaks v8.30.1 reports no leaks in committed history or the current worktree. Trivy v0.70.0 reports no HIGH/CRITICAL findings for the pinned PostgreSQL image under the exact CI policy and approved `.trivyignore` file.
- NuGet restore audits direct and transitive packages with `NuGetAuditLevel=high`; `NU1903` and `NU1904` are errors. The local and CI command is `deploy\\scripts\\check-nuget-audit.cmd`, and the online audit passes.
- All previously released migrations remain immutable. Slice 8 adds only Audit migration `20260723010000_AddAuditViewerIndexes`, after `20260722212000_ExtendBranchAdministrationAuditActions`; no Identity, Organization, or Platform migration is required.
- PostgreSQL remains pinned to the official 18.4 Debian image (`postgres:18.4@sha256:32ca0af8e77bfb8c6610c488e4691f83f972a3e9e64d3b02facf3ab111ad5500`). Clean and exact upgrade paths, constraints, deferred triggers, concurrent provisioning, authorization freshness, and cross-context rollback pass against PostgreSQL 18.4.
- PostgreSQL-backed integration tests create and drop isolated test databases using the `KALM_TEST_POSTGRES_ADMIN` connection string when supplied, or the local Docker defaults otherwise.
- Slice 1 adds Catalog migration `20260723020000_InitialCatalogFoundation` and Audit migration `20260723020500_ExtendCatalogFoundationAuditActions`, both with generated designers and LF-normalized byte baselines. The exact Milestone 1A upgrade creates no operational catalogue data and preserves existing Organization and Audit records.
- Catalog definitions are shared at Organization scope. Branch prices and availability, Product images, Preparation Stations, Tax Profiles, Modifiers, POS menu projection, and operational Kalm menu seeding remain explicitly deferred.
