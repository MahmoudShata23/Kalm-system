@echo off
setlocal

dotnet test tests\Integration\Kalm.Api.IntegrationTests\Kalm.Api.IntegrationTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PostgreSqlFoundationTests.InitialMigration_AppliesToCleanPostgreSqlDatabase|FullyQualifiedName~MilestoneOneAMigrationTests"
exit /b %ERRORLEVEL%
