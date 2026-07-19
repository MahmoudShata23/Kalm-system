@echo off
setlocal

dotnet restore Kalm.slnx --force-evaluate -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=high
exit /b %ERRORLEVEL%
