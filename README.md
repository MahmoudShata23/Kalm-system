# Kalm Cafe Management System

Kalm is a modular monolith cafe POS and operations system for Kalm Specialty Coffee.

This repository is currently at **Milestone 0 - Foundation**. It contains the runnable foundation only: .NET API host, PostgreSQL wiring, initial platform migration, Problem Details, correlation IDs, health endpoints, authentication skeleton contract, Angular bilingual shell, Docker Compose, CI, and test projects.

Milestone 1 business features are intentionally not implemented yet.

## Required Toolchain

- .NET SDK 10.0.302, or a compatible 10.0.300 feature-band SDK.
- Node.js 24.18.0 LTS with npm 11.17.0.
- Docker with Docker Compose.

## Backend

```bash
docker compose up -d postgres
dotnet restore Kalm.slnx
dotnet format Kalm.slnx --verify-no-changes --no-restore
dotnet build Kalm.slnx --no-restore -m:1
dotnet test tests/Unit/Kalm.UnitTests/Kalm.UnitTests.csproj --no-build
dotnet test tests/Architecture/Kalm.ArchitectureTests/Kalm.ArchitectureTests.csproj --no-build
dotnet test tests/Integration/Kalm.Api.IntegrationTests/Kalm.Api.IntegrationTests.csproj --no-build
dotnet run --project src/Kalm.Api
```

Health endpoints:

- `GET /health/live`
- `GET /health/ready`

Authentication skeleton endpoints:

- `GET /api/v1/auth/me`
- `POST /api/v1/auth/login`

The login endpoint returns a stable `iam.not_configured` Problem Details response until Milestone 1 implements real credential validation.

## Frontend

```bash
cd apps/web
npm ci
npm run lint
npm run test
npm run build
npm start
```

The Angular shell is standalone, strict, zoneless, and supports English LTR and Arabic RTL.

## Documentation

- Requirements: `KALM_SRD.md`
- Contributor instructions: `AGENTS.md`
- Implementation status: `docs/product/implementation-status.md`
- Architecture decisions: `docs/adr/`
- Local development guide: `docs/operations/local-development.md`
