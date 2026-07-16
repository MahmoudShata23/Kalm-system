# Local Development

## Prerequisites

- .NET SDK 10.0.302 or a compatible 10.0.300 feature-band SDK.
- Node.js 24.18.0 LTS with npm 11.17.0.
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
