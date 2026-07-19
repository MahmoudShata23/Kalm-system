@echo off
setlocal

if /I "%~1"=="generate" (
  set "KALM_UPDATE_OPENAPI=1"
) else if /I "%~1"=="check" (
  set "KALM_UPDATE_OPENAPI="
) else (
  echo Usage: deploy\scripts\openapi.cmd generate^|check
  exit /b 2
)

dotnet test tests\Integration\Kalm.Api.IntegrationTests\Kalm.Api.IntegrationTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~OpenApiContractTests.OpenApiDocument_MatchesCommittedSnapshot"
exit /b %ERRORLEVEL%
