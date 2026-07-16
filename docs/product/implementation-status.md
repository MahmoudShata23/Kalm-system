# Kalm Implementation Status

Last updated: 2026-07-15

## Current Milestone

Milestone 0 - Foundation

## Requirement Checklist

| Requirement | Status | Notes |
|---|---|---|
| SRD 8.1 Approved stack | Implemented with local blockers | Exact dependency versions are pinned or documented. Local Node/.NET/Docker mismatch noted. |
| SRD 8.2 Modular monolith | Implemented | API composition root, shared kernel, building blocks, and Identity skeleton contracts created. |
| SRD 9 Repository structure | Implemented | Foundation folders created. Future modules are intentionally not generated as empty projects. |
| SRD 10.1 General coding rules | Implemented | Nullable, analyzers, strict TypeScript, central package pinning, and warnings-as-errors configured. |
| SRD 10.2 Backend rules | Implemented | Problem Details, correlation IDs, injected clock, EF Core PostgreSQL context, and migration added. |
| SRD 10.3 Frontend rules | Implemented, not locally runnable | Standalone Angular shell, strict TypeScript, signals, zoneless configuration, and localized EN/AR copy added. Local Node 20 blocks execution. |
| SRD 17.8 Observability | Implemented for M0 | Correlation ID middleware and health endpoints added. Full OpenTelemetry is deferred beyond Milestone 0. |
| SRD 21 Testing strategy | Implemented with local blockers | Unit, integration, architecture, and E2E smoke projects added. Backend tests passed. Frontend/E2E require Node 24; PostgreSQL container requires Docker. |
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

- Installed Node.js is 20.20.0; Angular 22 requires Node 24.18.0 LTS for this repository.
- Installed .NET SDK is 10.0.301; official latest .NET 10 SDK is 10.0.302. The repo accepts the 10.0.300 feature band with roll-forward.
- Docker is not installed, so local PostgreSQL container startup and readiness validation cannot run on this machine until Docker is installed.
- On this Windows machine, solution-level `dotnet test Kalm.slnx --no-build` returned code 1 without diagnostics. Project-level test runs passed and are documented as the reliable local/CI command shape.
