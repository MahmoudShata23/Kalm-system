# AGENTS.md - Kalm Cafe Management System

This file contains mandatory repository instructions for Codex and human contributors.

## 1. Source of Truth

Read in this order before implementation:

1. `KALM_SRD.md`
2. `AGENTS.md`
3. Relevant files in `docs/adr/`
4. Existing module tests and public contracts

When code conflicts with the SRD, stop and resolve the discrepancy. Do not silently invent business rules.

## 2. Mission

Build a production-grade cafe POS and operations system for Kalm Specialty Coffee. The core invariant is that completed sales, payments, recipe consumption, stock ledger, shift totals, and reports reconcile.

## 3. Approved Stack

- Angular 22.x stable, standalone, strict, zoneless.
- PrimeNG 22.x Styled Mode with `@primeuix/themes`, Aura-based `KalmPreset`, PrimeIcons 8.x, and Kalm design tokens. Angular Material and direct application CDK dependencies/imports are prohibited; PrimeNG's required transitive CDK dependency is documented in ADR 0002.
- Node.js 24 LTS for development/build.
- .NET 10 LTS / ASP.NET Core 10.
- EF Core 10 and PostgreSQL 18.x.
- Playwright for E2E.
- Docker Compose locally.

Use latest patched stable versions within these majors. Do not introduce preview/RC packages.

## 4. Architecture

- Modular monolith.
- Module internals are private.
- No cross-module DbContext/DbSet access.
- No generic repository over EF Core.
- Use vertical use-case slices and explicit mapping.
- Domain behavior protects invariants.
- Financial and stock posting uses transactions.
- External retries use idempotency.
- Async side effects use transactional outbox.
- Posted records are corrected by reversal, not deletion.

## 5. Implementation Workflow

For every task:

1. Identify SRD requirement IDs and acceptance scenario.
2. Inspect existing code/tests before editing.
3. Write or update tests first when practical.
4. Implement the smallest complete vertical slice.
5. Run formatter, build, unit, integration, and relevant E2E tests.
6. Update docs/OpenAPI/migration.
7. Report what changed, commands run, and remaining risks.

Do not produce a large set of disconnected scaffolds.

## 6. Backend Rules

- Nullable enabled.
- Warnings as errors in CI.
- `decimal` for money and quantity.
- UTC persisted times through injected clock.
- CancellationToken on I/O.
- Explicit request validation.
- RFC Problem Details and stable error codes.
- Optimistic concurrency for editable aggregates.
- Never return EF entities from API.
- Never log secrets, PINs, passwords, cookies, or full sensitive payloads.
- Avoid N+1 queries and unbounded list endpoints.
- Every database mutation that affects money or stock has an integration test.

## 7. Frontend Rules

- Standalone components only.
- Feature-first folders.
- Strict TypeScript/templates.
- Signals for state; RxJS for streams and cancellation.
- No direct HTTP from presentation components.
- All strings localizable in Arabic and English.
- Accessible semantic markup and visible focus.
- Touch targets at least 44px; POS targets normally larger.
- Use design tokens; do not scatter brand hex codes.
- Use PrimeNG Styled Mode with the Aura-based `KalmPreset`; do not use the Material preset, `::ng-deep`, PrimeFlex, Tailwind, Bootstrap, or another CSS framework.
- Client calculations are previews only; server validates authoritative totals.
- Offline UI always shows connectivity and pending sync state.

## 8. Data Rules

- UUID primary keys.
- Human numbers are branch sequences.
- Ledger, audit, payment, completed order, and posted document rows are immutable.
- Recipe and price history uses versions/effective dates.
- Historical order lines store display, pricing, recipe, and cost snapshots.
- Inventory balance is rebuildable from ledger.
- Soft archive master data rather than delete.

## 9. Testing Rules

Required test layers:

- Unit tests for calculations and state transitions.
- PostgreSQL integration tests for persistence/transactions/idempotency.
- Architecture tests for module boundaries.
- Playwright E2E for critical user journeys.

Critical flows must test authorization, validation, retry, concurrency, and rollback where relevant.

## 10. Dependency Rules

- Prefer .NET/Angular native capabilities.
- Add a package only with a documented need.
- Pin versions and commit lockfiles.
- Check license and maintenance status.
- Do not add a global state library, mediator, mapper, repository framework, or microservice framework by habit.

## 11. Commands

Use repository scripts when present. Expected baseline:

```bash
# Backend
dotnet format --verify-no-changes
dotnet build --no-restore
dotnet test --no-build

# Frontend
cd apps/web
npm ci
npm run lint
npm run test -- --watch=false
npm run build
npm run e2e

# Local infrastructure
docker compose up -d postgres
```

## 12. Prohibited Shortcuts

Do not:

- Hard-code Kalm prices, taxes, service charges, or final recipes.
- Trust client totals or permissions.
- Use float/double for money.
- Delete posted orders/payments/stock entries.
- Hide failed tests.
- mark TODO/stub behavior as complete.
- disable strict mode or analyzers to make code compile.
- store card PAN/CVV.
- expose an unauthenticated local print endpoint.
- build microservices or Kubernetes for Release 1.

## 13. Definition of Complete Work

A slice is complete only when it is usable end-to-end, persisted, authorized, audited where needed, localized, tested, documented, and builds cleanly.
