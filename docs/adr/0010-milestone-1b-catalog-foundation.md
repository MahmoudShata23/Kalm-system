# ADR 0010: Milestone 1B Catalog Foundation

Date: 2026-07-23

## Status

Accepted

## Decision

- Catalog definitions are organization-scoped and live in a dedicated `Kalm.Catalog` bounded context with a `CatalogDbContext` and `catalog` PostgreSQL schema. Categories, Products, and Variants use UUID identifiers and are shared by all branches in the organization.
- The API reuses the exact existing `catalog.view` and `catalog.manage` permissions together with `management.access`. Organization identity comes only from the authoritative management session and is not accepted from requests or returned unnecessarily.
- Category and Product lifecycle is Active or Archived. Physical deletion is unavailable. Archived category names, Product SKUs, Variant codes, and non-null barcodes remain reserved.
- A Product is the consistency boundary for its Variants. Product create/update is the only Variant write surface. Omitted existing Variants remain unchanged, status changes are explicit, optional Variant ordering must contain every existing Variant exactly once, and the Product strong ETag protects the complete aggregate.
- Active Products require an Active Category and at least one Active Variant. Products always require at least one Variant. PostgreSQL deferred constraint triggers protect these invariants at transaction commit. Organization-aware foreign and alternate keys prevent cross-organization Category/Product relationships and orphan Variants.
- Category ordering uses an exact organization collection ETag. Product and lifecycle mutations use strong entity ETags. Missing preconditions return `428 catalog.precondition_required`, invalid preconditions return `400 catalog.invalid_precondition`, and stale versions return `412 catalog.concurrency_conflict`. No-op writes retain versions and emit no mutation audit.
- Advisory transaction locks are scoped to the organization/category, organization/product, or organization code reservation set. They serialize only cross-row conflicts: Category archive versus Product create/activation, Product activation versus final Active Variant archive, exact ordering, and concurrent code reservation.
- Catalog and Audit changes use one Npgsql connection, one explicit local transaction, `CatalogDbContext`, `AuditDbContext`, and `UseTransactionAsync`. A failed Catalog or semantic Audit write rolls back both.
- Audit metadata contains only safe identifiers, status changes, code-oriented state, changed field names, counts, actor ID, and correlation ID. Raw descriptions, request bodies, credentials, authentication material, cookies, sessions, and secrets are excluded. The Audit Viewer continues to hide unknown metadata by default.
- The Angular management feature is standalone, lazy-loaded, signal-based, bilingual Arabic RTL/English LTR, and uses typed API services rather than direct component HTTP. Category/Product drafts remain in component memory and are preserved after stale-ETag conflicts without browser persistence.

## Deferred decisions

- Branch/channel price lists, scheduled prices, and availability.
- Modifier groups and selections.
- Recipes, costing, inventory, discounts, and POS menu projection.
- Kalm operational menu seed data.
- Product image upload or media references.
- Preparation Station and Tax Profile administration.

No production prices, placeholder zero prices, fake Station/Tax identifiers, or operational menu rows are introduced by either migration.

## Consequences

Milestone 1A remains closed and immutable. Milestone 1B is open with a safe organization catalogue foundation. Later slices can add branch-aware pricing and availability without duplicating Category, Product, or Variant definitions.
