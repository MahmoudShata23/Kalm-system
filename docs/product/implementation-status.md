# Kalm Implementation Status

Last updated: 2026-07-19

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
| SRD 21 Testing strategy | Verified locally; CI-only security gates pending | Unit, PostgreSQL-backed integration, architecture, and Playwright E2E smoke tests pass. Gitleaks and Trivy execute only on GitHub runners. |
| SRD 22.2 Local developer experience | Verified | Docker Compose, health checks, migration validation, OpenAPI snapshot commands, guarded development reset, README, and local development guide are present. The reset intentionally seeds no users, credentials, or cafe business data. |
| SRD 22.3 CI pipeline | Implemented; CI execution pending | GitHub Actions includes restore, fail-closed NuGet high/critical audit policy, format, build, tests, migration/OpenAPI checks, npm audit, Gitleaks, Trivy, and Playwright browser provisioning. |
| SRD 23 Milestone 0 | Verified locally; CI execution pending | Foundation implementation only. |
| SRD 23 Milestone 1A Slice 1 | Implemented; PostgreSQL validation blocked locally | Organization/Branch persistence and immutable Audit foundation only; no runtime administration route or Angular screen is exposed before authentication. Docker Desktop was unavailable for database-backed validation. |
| IAM-002 Authentication modes | Not started | Only skeleton endpoint contract exists. Real authentication belongs to Milestone 1. |
| IAM-004 Roles and permissions | Not started | Not in Milestone 0. |

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
- `npm audit --audit-level=high` passes with one reported low-severity esbuild advisory (GHSA-g7r4-m6w7-qqqr); no high/critical frontend advisory is present.
- The PrimeNG production build succeeds at 502.38 kB initial raw size (114.14 kB estimated transfer). The existing 500 kB warning fires by 2.38 kB, the 750 kB error budget passes, and test-only accessibility fixtures produce no production chunk.
- Gitleaks and Trivy are configured in CI but were not run locally because they are runner-based checks.
- NuGet restore audits direct and transitive packages with `NuGetAuditLevel=high`; `NU1903` and `NU1904` are errors. The local and CI command is `deploy\\scripts\\check-nuget-audit.cmd`.
- The committed `20260715140000_InitialFoundation` migration is immutable. No additive database migration was required; the current model snapshot has no pending schema operations. Clean and previously-released-database upgrade tests pass.
- The PostgreSQL 18.4 image and local container are healthy and available on port 54329.
- PostgreSQL-backed integration tests create and drop isolated test databases using the `KALM_TEST_POSTGRES_ADMIN` connection string when supplied, or the local Docker defaults otherwise.
