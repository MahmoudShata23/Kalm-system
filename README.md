# Kalm Cafe Management System

Kalm is a modular monolith cafe POS and operations system for Kalm Specialty Coffee.

Milestone 0 and Milestone 1A Slices 1-4 are implemented. Slice 4 adds protected organization-scoped role administration, complete permission replacement, ETag concurrency, database-safe management-access protection, trusted CLI recovery, and a bilingual Roles experience. PIN login, devices, user administration/assignments, and later business modules remain deferred.

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
dotnet run --project src/Kalm.Api
```

Health endpoints:

- `GET /health/live`
- `GET /health/ready`

Management authentication endpoints:

- `GET /api/v1/auth/csrf`
- `GET /api/v1/auth/me`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/logout`

Role administration exposes only the six `/api/v1/management/roles` and `/api/v1/management/permissions` contracts documented in OpenAPI. Every one requires server-derived `management.access` and `roles.manage`. No user assignment, arbitrary permission, deletion, restore, or recovery HTTP endpoint is exposed.

Authentication uses an opaque server-session cookie. The Angular client keeps the CSRF request token only in memory and stores no authentication token in web storage. See `docs/operations/management-authentication.md` for deployment configuration and the non-public bootstrap procedure.

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

The reset intentionally seeds no users or credentials. Create the first management user only with the operational Bootstrap CLI after every migration has been applied; no public first-run endpoint exists.

## Documentation

- Requirements: `KALM_SRD.md`
- Contributor instructions: `AGENTS.md`
- Implementation status: `docs/product/implementation-status.md`
- Architecture decisions: `docs/adr/`
- PrimeNG foundation decision: `docs/adr/0002-primeng-ui-toolkit.md`
- Local development guide: `docs/operations/local-development.md`
- Management authentication and bootstrap: `docs/operations/management-authentication.md`
