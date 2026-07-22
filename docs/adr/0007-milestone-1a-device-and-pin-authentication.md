# ADR 0007: Milestone 1A Device and PIN Authentication

Date: 2026-07-22

## Status

Accepted

## Decision

- Devices are Organization-owned, bound to one active branch, and transition from `PendingPairing` to `Active` to `Revoked`; physical deletion is not exposed.
- Pairing challenges contain 160 random bits, expire after ten minutes, are single-use, and are stored only as SHA-256 hashes. Device credentials contain 256 random bits, are stored only as hashes, and are delivered in a Secure, HttpOnly, SameSite=Strict cookie.
- A device security version binds credentials and user sessions. Re-pairing, security-sensitive updates, and revocation invalidate prior credentials and device-bound sessions in one local transaction.
- Employee PINs are exactly six digits and use purpose-separated PBKDF2-HMAC-SHA512 with a random 256-bit salt. PIN setup/reset is administrator-only, recently reauthenticated, CSRF- and rate-limited, and returns 204.
- PIN login requires an explicit target user plus an active paired device. Eligibility requires an active user, PIN, role, branch, and effective branch access. Generic failures and a per-device/user five-attempt, fifteen-minute lockout prevent disclosure and organization-wide denial of service.
- Authentication cookies contain only the opaque session identifier and scheme marker. Device, branch, PIN credential, device security, and authorization versions remain server-side and are validated on every request.
- Workstation lock revokes the current device-bound user session and clears only the user-authentication cookie; the device credential remains paired for employee switching.
- Organization, Identity, and Audit changes share one explicit local PostgreSQL transaction through `UseTransactionAsync`; no ambient or distributed transaction is used.

## Consequences

Slice 6 adds one Organization migration, one Identity migration, and one Audit migration. It adds no POS, shift, order, printer, offline-sync, MFA, deletion, diagnostic, fixture, bypass, or recovery behavior.
