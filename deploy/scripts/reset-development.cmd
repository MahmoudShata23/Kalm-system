@echo off
setlocal

if /I not "%~1"=="--force" (
  echo This command deletes and recreates the local 'kalm' development database.
  echo Re-run as: deploy\scripts\reset-development.cmd --force
  exit /b 2
)

set "KALM_DATABASE_CONNECTION_STRING=Host=127.0.0.1;Port=54329;Database=kalm;Username=kalm;Password=kalm_dev_password"

dotnet tool restore
if errorlevel 1 exit /b %ERRORLEVEL%

dotnet tool run dotnet-ef database drop --force --project src\Kalm.Api\Kalm.Api.csproj --startup-project src\Kalm.Api\Kalm.Api.csproj --context KalmDbContext
if errorlevel 1 exit /b %ERRORLEVEL%

dotnet tool run dotnet-ef database update --project src\Kalm.Api\Kalm.Api.csproj --startup-project src\Kalm.Api\Kalm.Api.csproj --context KalmDbContext
if errorlevel 1 exit /b %ERRORLEVEL%

dotnet tool run dotnet-ef database update --project src\Modules\Kalm.Organization.Infrastructure\Kalm.Organization.Infrastructure.csproj --startup-project src\Kalm.Api\Kalm.Api.csproj --context OrganizationDbContext
if errorlevel 1 exit /b %ERRORLEVEL%

dotnet tool run dotnet-ef database update --project src\Modules\Kalm.Identity.Infrastructure\Kalm.Identity.Infrastructure.csproj --startup-project src\Kalm.Api\Kalm.Api.csproj --context IdentityDbContext
if errorlevel 1 exit /b %ERRORLEVEL%

dotnet tool run dotnet-ef database update --project src\Modules\Kalm.Audit.Infrastructure\Kalm.Audit.Infrastructure.csproj --startup-project src\Kalm.Api\Kalm.Api.csproj --context AuditDbContext
if errorlevel 1 exit /b %ERRORLEVEL%

echo Development reset complete. No users, credentials, or cafe business data were seeded. Use the operational Bootstrap CLI to create the initial management user.
