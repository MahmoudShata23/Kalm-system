# Kalm Cafe Management System

Kalm is a modular monolith cafe POS and operations system for Kalm Specialty Coffee.

This repository has completed the locally verified **Milestone 0 - Foundation** implementation. It contains the runnable foundation only: .NET API host, PostgreSQL wiring, initial platform migration, Problem Details, correlation IDs, health endpoints, authentication skeleton contract, Angular bilingual shell, Docker Compose, CI, contracts, and test projects.

Milestone 1 business features are intentionally not implemented yet.

## Required Toolchain

- .NET SDK 10.0.302.
- Node.js 24.18.0 LTS with its bundled npm 11.16.0.
- Docker with Docker Compose.

## Backend

```bash
docker compose up -d postgres
docker compose ps
docker compose exec -T postgres pg_isready -U kalm -d kalm
dotnet tool restore
dotnet restore Kalm.slnx
deploy\scripts\check-nuget-audit.cmd
dotnet format Kalm.slnx --verify-no-changes --no-restore
dotnet build Kalm.slnx --no-restore -m:1
dotnet test tests/Unit/Kalm.UnitTests/Kalm.UnitTests.csproj --no-build
dotnet test tests/Architecture/Kalm.ArchitectureTests/Kalm.ArchitectureTests.csproj --no-build
dotnet test tests/Integration/Kalm.Api.IntegrationTests/Kalm.Api.IntegrationTests.csproj --no-build
deploy\scripts\validate-migrations.cmd
dotnet tool run dotnet-ef database update --project src/Kalm.Api --startup-project src/Kalm.Api --context KalmDbContext
dotnet run --project src/Kalm.Api
```

Health endpoints:

- `GET /health/live`
- `GET /health/ready`

Authentication skeleton endpoints:

- `GET /api/v1/auth/me`
- `POST /api/v1/auth/login`

The login endpoint returns a stable `iam.not_configured` Problem Details response until Milestone 1 implements real credential validation.

For non-development environments, provide the database connection string through configuration such as the
`Database__ConnectionString` environment variable or a secret manager. Development defaults for the local
Docker database are kept in `appsettings.Development.json` only.

## Frontend

```bash
cd apps/web
npm.cmd ci
npm.cmd run lint
npm.cmd run test
npm.cmd run build
npm.cmd exec playwright install chromium
npm.cmd run e2e
npm.cmd start
```

The Angular shell is standalone, strict, zoneless, and supports English LTR and Arabic RTL. Its sole UI toolkit is PrimeNG 22.0.0 Styled Mode with `@primeuix/themes` 3.0.0, PrimeIcons 8.0.0, and an Aura-based `KalmPreset` mapped to Kalm design tokens. Angular Material and the direct CDK dependency are removed; npm resolves CDK only because PrimeNG 22 declares it as a required transitive dependency.

This machine's PowerShell policy blocks the unsigned `npm.ps1` shim. Use `npm.cmd` as shown; do not change the machine execution policy. Linux/macOS and CI use `npm` normally.

NuGet restore audits direct and transitive dependencies. High and critical findings (`NU1903` and `NU1904`) fail the
restore; lower-severity findings do not block Milestone 0. Run `deploy\scripts\check-nuget-audit.cmd` locally to
apply the same policy as CI.

## OpenAPI contract

The committed API snapshot is `contracts/openapi/kalm-api.v1.json`.

```text
deploy\scripts\openapi.cmd generate
deploy\scripts\openapi.cmd check
```

Generation is deterministic and the CI check fails when the runtime document differs from the committed snapshot.

## Development reset and seed

The guarded workflow below deletes and recreates only the local `kalm` database at `127.0.0.1:54329`, then applies all migrations:

```text
deploy\scripts\reset-development.cmd --force
```

Milestone 0 intentionally seeds no users, credentials, catalog, recipes, inventory, POS, payment, shift, supplier, or menu data. The login endpoint remains a development-safe contract skeleton and always returns `iam.not_configured` for a syntactically valid request.

## Documentation

- Requirements: `KALM_SRD.md`
- Contributor instructions: `AGENTS.md`
- Implementation status: `docs/product/implementation-status.md`
- Architecture decisions: `docs/adr/`
- PrimeNG foundation decision: `docs/adr/0002-primeng-ui-toolkit.md`
- Local development guide: `docs/operations/local-development.md`
