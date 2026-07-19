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

echo Development reset complete. Milestone 0 intentionally seeds no users, credentials, or cafe business data.
