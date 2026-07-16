# ADR 0001: Foundation Stack and Structure

Date: 2026-07-15

## Status

Accepted

## Context

The SRD requires a modular monolith using Angular 22, Node.js 24 LTS, .NET 10 LTS, EF Core 10, PostgreSQL 18, Playwright, Docker Compose, strict TypeScript, nullable C#, warnings as errors where practical, and module-boundary tests.

Milestone 0 must deliver the foundation only and must not generate disconnected scaffolds for all future business modules.

## Decision

- Use the SRD repository layout with `src/`, `apps/`, `tests/`, `docs/`, `deploy/`, and `.github/workflows/`.
- Create only the active backend module needed by Milestone 0: `Kalm.Identity` for authentication skeleton contracts.
- Keep shared domain primitives in `Kalm.SharedKernel`, framework-free.
- Keep cross-cutting implementation helpers in `Kalm.BuildingBlocks`.
- Use `Kalm.Api` as the composition root and API host.
- Use central NuGet package management in `Directory.Packages.props`.
- Use exact npm package versions in `apps/web/package.json`.
- Use PostgreSQL 18.4 in Docker Compose and CI.

## Confirmed Stable Versions

- .NET SDK 10.0.302, .NET runtime 10.0.10.
- EF Core 10.0.10.
- Microsoft.OpenApi 2.10.0, pinned to avoid vulnerable transitive 2.0.0 while remaining compatible with ASP.NET Core OpenAPI 10.
- Npgsql EF Core provider 10.0.3.
- PostgreSQL 18.4.
- Node.js 24.18.0 LTS with bundled npm 11.16.0.
- Angular 22.0.7.
- Angular Material/CDK 22.0.5.
- TypeScript 6.0.3, the latest stable version in Angular 22.0.7's supported `>=6.0 <6.1` peer range.
- RxJS 7.8.2.
- Playwright 1.61.1.

## Consequences

- Local development requires .NET SDK 10.0.302, Node 24.18.0, and Docker. The current machine has .NET SDK 10.0.301, Node 20.20.0, and no Docker, so backend, frontend, and PostgreSQL runtime validation require environment updates.
- Future modules are added milestone-by-milestone with tests rather than as empty projects.
- Authentication endpoints exist as a skeleton contract in Milestone 0, but real credential validation remains Milestone 1.
