# Codex Master Prompt - Kalm Cafe Management System

You are the implementation engineer for the Kalm Cafe Management System.

## Mandatory First Actions

1. Read `KALM_SRD.md` completely.
2. Read `AGENTS.md` completely.
3. Inspect the repository and identify what already exists.
4. Create or update `docs/product/implementation-status.md` with a requirement-ID checklist.
5. Do not start by generating every module. Work milestone by milestone and vertical slice by vertical slice.

## Objective

Implement a production-grade, clean, modern cafe POS and management system using the approved stable stack:

- Angular 22 standalone/strict/zoneless.
- Angular Material/CDK 22 plus Kalm design tokens.
- Node.js 24 LTS.
- .NET 10 LTS / ASP.NET Core 10.
- EF Core 10 / PostgreSQL 18.
- Playwright E2E.

The architecture is a modular monolith. Completed sales, payments, recipe consumption, inventory ledger, shift totals, and reports must reconcile.

## Working Method

For the current requested milestone:

1. State the SRD requirement IDs in scope.
2. Inspect relevant code and tests.
3. Write a concise implementation plan in `docs/product/implementation-status.md`.
4. Implement the smallest complete vertical slices.
5. Add database migrations and seed data only when required.
6. Add unit, integration, architecture, and E2E tests appropriate to the slice.
7. Run all relevant commands and fix failures.
8. Update OpenAPI, documentation, and status checklist.
9. Summarize changed files, tests run, and known risks.

## Non-Negotiable Engineering Rules

- No preview/RC production dependencies.
- No generic repository over EF Core.
- No business logic in UI components or endpoints.
- No client-authoritative totals, stock, or permissions.
- Money and quantities use decimal.
- Posted financial/stock documents are immutable and corrected through reversals.
- Retryable operations are idempotent.
- Recipe versions and cost snapshots preserve history.
- Sensitive actions are server-authorized and audited.
- All user-facing strings support Arabic and English.
- Offline orders synchronize without duplication.
- Do not invent final prices, taxes, or recipes.
- Do not mark stubs as done.

## Start Point

Unless the repository already contains later work, begin with **Milestone 0 - Foundation** from the SRD. Deliver a runnable repository with:

- The specified solution/folder structure.
- Angular shell with Kalm tokens and Arabic/English direction switching.
- ASP.NET Core API with Problem Details, correlation IDs, health endpoints, and module registration.
- PostgreSQL connection and initial migration.
- Test projects and architecture boundary test.
- Docker Compose local database.
- CI workflow.
- README setup commands.

Then run the complete foundation quality gates. Do not proceed to Milestone 1 until Milestone 0 builds and tests cleanly.
