# ADR 0003: Milestone 1A Security, Persistence, and Audit Foundation

Date: 2026-07-20

## Status

Accepted

## Decision

The original Milestone 1 delivery is subdivided without removing its catalog requirements:

- Milestone 1A delivers Identity, Organization, Branches, Devices, Authentication, Authorization, Sessions, PIN login, and immutable audit writing.
- Milestone 1B delivers categories, products, variants, pricing, modifiers, availability, the POS menu endpoint, catalog administration, and the Kalm menu seed.

Slice 1 delivers only Organization/Branch persistence and immutable audit writing. It exposes no Organization or Branch HTTP route and adds no Angular production UI.

Organization, Identity, and Audit own separate DbContexts and migration histories. The existing API-owned `KalmDbContext` continues to own only Milestone 0 platform tables. Existing migrations remain immutable and every future migration is additive.

For an audited Organization command, the API composition root opens one explicit `NpgsqlConnection` and one local `NpgsqlTransaction`, creates both module contexts against that connection, and enlists both using `UseTransactionAsync`. It saves and commits only after the Organization and Audit writes both succeed. No ambient or distributed transaction is used. Modules do not access one another's DbContext, DbSet, or persistence implementation.

Audit records are append-only. PostgreSQL rejects every update and delete through an `audit`-schema trigger. Audit requests contain only safe semantic fields; raw request bodies and credentials are prohibited.

The later operational CLI bootstrap uses non-public interactive input, environment variables, user secrets, or an approved secret manager. Production users and credentials are never committed. The approved password, PIN, session, CSRF, and MFA decisions are recorded for later slices; MFA remains deferred from Milestone 1A.

PrimeNG Styled Mode with the Aura-based KalmPreset remains mandatory. Slice 1 adds neither unauthenticated Organization/Branch UI nor a public component fixture.

## Consequences

- The complete original Milestone 1 exit criterion remains pending until Milestone 1B is delivered.
- The first Organization record is operationally created, never seeded by a migration.
- Release 1's single-organization rule is enforced with a database singleton key.
- Future sensitive module commands use the narrow composition-root transaction pattern when an immediate immutable audit record is required.
