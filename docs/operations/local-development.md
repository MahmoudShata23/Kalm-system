# Local Development

## Prerequisites

- .NET SDK 10.0.302.
- Node.js 24.18.0 LTS with its bundled npm 11.16.0.
- Docker with Docker Compose.

## Backend

```bash
docker compose up -d postgres
dotnet restore Kalm.slnx
dotnet build Kalm.slnx -m:1
dotnet test tests/Unit/Kalm.UnitTests/Kalm.UnitTests.csproj --no-build
dotnet test tests/Architecture/Kalm.ArchitectureTests/Kalm.ArchitectureTests.csproj --no-build
dotnet test tests/Integration/Kalm.Api.IntegrationTests/Kalm.Api.IntegrationTests.csproj --no-build
dotnet run --project src/Kalm.Api
```

Health endpoints:

- `GET /health/live`
- `GET /health/ready`

The readiness endpoint checks the configured PostgreSQL connection.

## Frontend

```bash
cd apps/web
npm ci
npm run lint
npm run test
npm run build
npm start
```

Open `http://127.0.0.1:4200`.

## Development Database

The local PostgreSQL service uses:

- Host: `localhost`
- Port: `54329`
- Database: `kalm`
- User: `kalm`

The password is development-only and defined in `docker-compose.yml`.

Production and staging connection strings must be supplied through environment variables, user secrets, or a
secret manager. The root `appsettings.json` intentionally does not contain a usable database password.
