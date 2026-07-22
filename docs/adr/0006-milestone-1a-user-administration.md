# ADR 0006: Milestone 1A User Administration and Secure Provisioning

Date: 2026-07-22

## Status

Accepted

## Decision

- User administration exposes bounded list/search, editor options, detail, create, complete PUT, activate, suspend, and trusted administrator password actions under `/api/v1/management/users`.
- Viewing requires `management.access` and `users.view`; mutations require `management.access` and `users.manage`. Password changes also require recent authentication and rate limiting.
- Users are created suspended. Activation requires an active password credential, at least one active organization role, and valid active-branch access.
- Complete role and branch replacement revokes historical assignments rather than deleting them. A committed authorization diff increments the target user's `AuthorizationVersion` once; a no-op changes neither versions nor audit data.
- Strong user-version ETags protect every mutation after creation. Stale writes are never retried or forced.
- Creation and updates share one local PostgreSQL transaction across Identity, Organization, and Audit. Suspension and password changes revoke active sessions within the same Identity transaction.
- The Slice 4 `identity.effective_management_user_count(uuid)` function, advisory lock, and deferred triggers remain the sole database authority for final-management-access protection.
- Existing Identity and Organization schema is sufficient. Slice 5 adds only an Audit migration extending the immutable action constraint.

## Consequences

Authorization changes take effect on the next request because sessions contain no roles, permissions, branch scope, status, or authorization version. PINs, MFA, email reset links, deletion, impersonation, devices, and branch or role CRUD remain outside this slice.
