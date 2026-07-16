# Kalm Implementation Status

Last updated: 2026-07-16

## Current Milestone

Milestone 0 - Foundation

## Requirement Checklist

| Requirement | Status | Notes |
|---|---|---|
| SRD 8.1 Approved stack | Implemented with local blockers | Exact dependency versions are pinned or documented: .NET SDK 10.0.302, Angular 22.0.7, Material/CDK 22.0.5, Node 24.18.0 with npm 11.16.0. Local Node/.NET/Docker mismatch noted. |
| SRD 8.2 Modular monolith | Implemented | API composition root, shared kernel, building blocks, and Identity skeleton contracts created. |
| SRD 9 Repository structure | Implemented | Foundation folders created. Future modules are intentionally not generated as empty projects. |
| SRD 10.1 General coding rules | Implemented | Nullable, analyzers, strict TypeScript, central package pinning, and warnings-as-errors configured. |
| SRD 10.2 Backend rules | Implemented | Problem Details, correlation IDs, injected clock, EF Core PostgreSQL context, and migration added. |
| SRD 10.3 Frontend rules | Implemented, not locally runnable | Standalone Angular shell, strict TypeScript, signals, zoneless configuration, and localized EN/AR copy added. Local Node 20 blocks execution against the approved engine. |
| SRD 17.8 Observability | Implemented for M0 | Correlation ID middleware and health endpoints added. Full OpenTelemetry is deferred beyond Milestone 0. |
| SRD 21 Testing strategy | Implemented with local blockers | Unit, PostgreSQL-backed integration, architecture, and E2E smoke projects added. Local execution requires .NET SDK 10.0.302, Node 24.18.0, and PostgreSQL/Docker. |
| SRD 22.2 Local developer experience | Implemented with local blockers | Docker Compose, README, and local development guide added. Docker is not installed locally. |
| SRD 22.3 CI pipeline | Implemented | GitHub Actions foundation quality gates added. |
| SRD 23 Milestone 0 | Implemented with local validation blockers | Foundation implementation only. No Milestone 1 features implemented. |
| IAM-002 Authentication modes | Not started | Only skeleton endpoint contract exists. Real authentication belongs to Milestone 1. |
| IAM-004 Roles and permissions | Not started | Not in Milestone 0. |

## Milestone 0 Implementation Plan

1. Create solution and repository structure.
2. Add API foundation: Problem Details, correlation IDs, health checks, auth skeleton, EF Core platform schema, and initial migration.
3. Add shared kernel/building blocks with framework-independent primitives.
4. Add tests for business-date calculation, result primitives, API shell behavior, persistence model mapping, and architecture boundaries.
5. Add Angular shell with Kalm design tokens and bilingual direction switching.
6. Add Docker Compose, CI, README, and setup documentation.
7. Run formatting, linting, builds, and tests; document blockers from missing local runtime dependencies.

## Known Local Environment Issues

- Installed Node.js is 20.20.0; this repository requires Node 24.18.0 LTS.
- Installed .NET SDK is 10.0.301; this repository requires .NET SDK 10.0.302.
- Docker is not installed, so local PostgreSQL container startup and readiness validation cannot run on this machine until Docker is installed.
- PostgreSQL-backed integration tests create and drop isolated test databases using the `KALM_TEST_POSTGRES_ADMIN` connection string when supplied, or the local Docker defaults otherwise.
