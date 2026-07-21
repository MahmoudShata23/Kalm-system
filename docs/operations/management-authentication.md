# Management Authentication Operations

Milestone 1A Slice 2 supports only management password authentication and the trusted operational creation of the first management user. It does not expose a bootstrap, activation, reset, user-administration, role, permission, PIN, device, or MFA endpoint.

Milestone 1A Slice 3 adds trusted operational authorization provisioning. It still exposes no bootstrap, provisioning, user, role, permission, or branch administration HTTP endpoint.

## Runtime configuration

Supply production and staging values through deployment secrets or an approved secret manager:

- `Database__ConnectionString`: PostgreSQL connection string.
- `PasswordHashing__Iterations`: calibrated PBKDF2-HMAC-SHA512 work factor; never below 220,000.
- `SecurityFingerprint__ActiveKeyVersion`: positive active HMAC key version.
- `SecurityFingerprint__ActiveKeyBase64`: at least 32 random bytes encoded as Base64.
- `DataProtection__KeyRingPath`: persistent, shared, readable and writable key-ring directory.
- `DataProtection__CertificatePath`: deployment-mounted PKCS#12 certificate containing a private key.
- `DataProtection__CertificatePassword`: certificate password supplied as a secret when required.

Production and staging startup fail when the fingerprint key, persistent Data Protection path, or X.509 at-rest protection is missing or invalid. Do not place fingerprint keys, certificates, private keys, key-ring files, passwords, hashes, cookies, or CSRF tokens in the repository, image, logs, audit data, or command line.

Development stores Data Protection keys in the repo-external operating-system temporary directory under `Kalm/DataProtection-Keys` unless a path is configured. Deleting that directory invalidates local authentication cookies. Development generates an ephemeral fingerprint key when none is configured; use one explicit shared development key for both API and Bootstrap whenever operational bootstrap and subsequent login are being exercised.

## Password work factor

The encoded password format is `$kalm$pbkdf2-sha512$v=1$i=<iterations>$s=<base64-salt>$h=<base64-derived-key>`, with a 32-byte random salt and 64-byte derived key. Verification is fixed-time. Passwords are measured as Unicode scalars, must contain 15–128 scalars, and are neither trimmed nor normalized. Spaces, Unicode, and passphrases are accepted without composition rules.

Calibrate on production-class hardware using warm-up runs and repeated 15-, 64-, and 128-scalar measurements. Select a work factor targeting approximately 250 ms median without exceeding the 500 ms p95 ceiling, store it in deployment configuration, and do not calibrate on API startup. The 2026-07-21 local reference measurement selected 220,000 iterations at 265.38 ms median and 334.81 ms p95; deployment hardware must be measured independently.

Successful login upgrades an older supported hash or lower stored work factor. It never lowers a stored work factor.

## Operational bootstrap

Apply every committed migration before running bootstrap. Configure the CLI process with:

- `KALM_DATABASE_CONNECTION_STRING`
- `KALM_PASSWORD_HASH_ITERATIONS`
- `KALM_FINGERPRINT_KEY_VERSION`
- `KALM_FINGERPRINT_KEY_BASE64`

Interactive use prompts without echo:

```text
dotnet run --project src/Kalm.Bootstrap -- bootstrap-management --username <username> --display-name <display-name> --organization-name <organization-name> --branch-name <branch-name> --branch-code <branch-code> --currency <currency> --locale <locale> --time-zone <iana-time-zone> --rollover <HH:mm> --preferred-language <en-or-ar> [--email <email>]
```

Automation must append `--password-stdin` and provide one password line through redirected standard input. There is deliberately no `--password` option and no supported password environment variable. Shell history, process arguments, stdout, and stderr must never contain the password.

The command atomically creates or reuses the single Organization, creates the first Branch when needed, creates the initial Suspended user and PendingSetup credential, hashes the password, completes credential setup, activates the user, and appends both audit events on one local PostgreSQL transaction. It refuses to run when any Identity user already exists. Concurrent attempts produce exactly one success.

The same clean-install transaction now provisions the versioned first-administrator system role, the complete current IAM-004 permission set including `management.access`, the user-role assignment, and `AssignedBranches` access to only the initial branch. Append `--all-organization-branches` only when the trusted operator explicitly intends current and future organization-wide scope.

For a database that already completed Slice 2 bootstrap, apply every Slice 3 migration and run exactly one explicit scope form:

```text
dotnet run --project src/Kalm.Bootstrap -- provision-first-administrator --username <username> --branch-code <code> [--branch-code <code> ...]
dotnet run --project src/Kalm.Bootstrap -- provision-first-administrator --username <username> --all-organization-branches
```

The target user and password credential must both be active. Exact reruns are idempotent; a conflicting user, permission set, or scope is refused. Concurrent identical attempts converge through a transaction-scoped PostgreSQL advisory lock and uniqueness constraints. This command accepts no password.

Exit codes:

- `0`: completed successfully.
- `1`: operation failed; no secret detail is emitted.
- `2`: invalid command or input.
- `3`: Identity already initialized.
- `4`: required migrations are missing.
- `5`: required CLI configuration is invalid.
- `6`: first-administrator authorization conflicts with the requested target, permission set, or branch scope.

## Cookies, CSRF, and sessions

`__Host-Kalm.Management` and `__Host-Kalm.Antiforgery` are Secure, HttpOnly, host-only, `Path=/`, and `SameSite=Strict`. The management cookie contains only the opaque server-session ID and scheme-version marker. It is not authoritative for identity or authorization and does not slide.

The browser calls `GET /api/v1/auth/csrf`, keeps the returned request token only in memory, and sends it as `X-XSRF-TOKEN` on unsafe same-origin API requests. It refreshes the token at startup and after login, logout, or another identity transition. Login and logout require CSRF. `/auth/csrf` is non-cacheable.

Every authenticated request reloads the authoritative session, user, and credential state. Inactivity expires after 20 minutes, absolute lifetime after eight hours, and the reserved recent-reauthentication window is five minutes. Activity updates are concurrency-safe and cannot extend beyond absolute expiry. Logout revokes only the current session and clears its cookie only after the revocation and audit event commit together.

Every authenticated request also resolves active roles, grants, compiled permission codes, explicit branch scope, and Organization/Branch operational status from PostgreSQL. Authorization is not cached. Roles, permissions, branch data, account state, and authorization version are never stored in the cookie. Permission removal affects the next request without ending an otherwise valid authentication session.

Login allows five failures in 15 minutes, then locks for 15 minutes; attempts while locked do not extend the lock. The API permits ten login requests per minute per transport source address with no queue. Arbitrary `X-Forwarded-For` values are ignored. No forwarded headers are accepted by the application in this slice; any future trusted-proxy configuration requires explicit known proxy/network configuration and tests. Multi-instance production deployments also require ingress-level rate limiting.

SameSite Strict depends on same-origin deployment. Cross-origin hosting, external authentication redirects, or a different subdomain architecture requires a security review and ADR amendment.

## Fingerprint-key rotation

Increment `SecurityFingerprint__ActiveKeyVersion` and deploy a new random key to the API and Bootstrap together. New login-attempt and network fingerprints use the new version; existing rows retain their recorded version and remain immutable. Do not reuse a version for different key material or attempt to rewrite historical audit/login-attempt data.
