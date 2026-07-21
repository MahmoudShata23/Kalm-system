using System.Text;
using Kalm.Api.Configuration;
using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Authorization;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Application.ManagementAuthentication;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.Transactions;

/// <summary>Coordinates only management-authentication and audit writes on one local PostgreSQL transaction.</summary>
public sealed class ManagementAuthenticationAuditTransactionCoordinator
{
    private readonly string _connectionString;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecurityFingerprintProvider _fingerprints;
    private readonly DummyPasswordHash _dummyPasswordHash;
    private readonly IClock _clock;
    private readonly ManagementAuthenticationOptions _options;

    public ManagementAuthenticationAuditTransactionCoordinator(
        IOptions<DatabaseOptions> database,
        IPasswordHasher passwordHasher,
        ISecurityFingerprintProvider fingerprints,
        DummyPasswordHash dummyPasswordHash,
        IClock clock,
        IOptions<ManagementAuthenticationOptions> options)
    {
        _connectionString = database.Value.ConnectionString;
        _passwordHasher = passwordHasher;
        _fingerprints = fingerprints;
        _dummyPasswordHash = dummyPasswordHash;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<ManagementLoginResult> LoginAsync(
        string identifier,
        string password,
        string? networkAddress,
        string? userAgent,
        string correlationId,
        CancellationToken cancellationToken)
    {
        string normalizedIdentifier = identifier.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        string identifierFingerprint = _fingerprints.Fingerprint(normalizedIdentifier);
        string? networkIdentifier = string.IsNullOrWhiteSpace(networkAddress) ? null : _fingerprints.Fingerprint(networkAddress);
        DateTimeOffset now = _clock.UtcNow;

        return await ExecuteAsync(async (identity, audit, transactionCancellationToken) =>
        {
            User? user = await identity.Users
                .SingleOrDefaultAsync(candidate => candidate.NormalizedUsername == normalizedIdentifier || candidate.NormalizedEmail == normalizedIdentifier, transactionCancellationToken);
            PasswordCredential? credential = user is null
                ? null
                : await identity.PasswordCredentials
                    .FromSqlInterpolated($"select * from identity.password_credentials where user_id = {user.Id} for update")
                    .SingleOrDefaultAsync(transactionCancellationToken);

            bool eligible = user?.Status == UserStatus.Active
                && credential?.Status == PasswordCredentialStatus.Active
                && credential.EncodedHash is not null;
            bool locked = eligible && credential!.IsLocked(now);
            PasswordVerificationResult verification = eligible && !locked
                ? _passwordHasher.Verify(password, credential!.EncodedHash!)
                : _passwordHasher.Verify(password, _dummyPasswordHash.EncodedHash);

            LoginAttemptOutcome outcome;
            bool newlyLocked = false;
            if (!eligible)
            {
                outcome = LoginAttemptOutcome.Ineligible;
            }
            else if (locked)
            {
                outcome = LoginAttemptOutcome.Locked;
            }
            else if (!verification.Succeeded)
            {
                newlyLocked = credential!.RegisterFailure(
                    now,
                    _options.FailureThreshold,
                    TimeSpan.FromMinutes(_options.FailureWindowMinutes),
                    TimeSpan.FromMinutes(_options.LockoutMinutes));
                outcome = newlyLocked ? LoginAttemptOutcome.Locked : LoginAttemptOutcome.InvalidCredentials;
            }
            else
            {
                outcome = LoginAttemptOutcome.Succeeded;
            }

            identity.LoginAttempts.Add(LoginAttempt.Create(
                Guid.NewGuid(), user?.Id, identifierFingerprint, _fingerprints.ActiveKeyVersion,
                networkIdentifier, outcome, now, correlationId));

            if (outcome != LoginAttemptOutcome.Succeeded)
            {
                await AppendAuditAsync(
                    audit, now, user?.OrganizationId, user?.Id, AuditActorType.Anonymous,
                    newlyLocked ? AuditAction.ManagementAccountLocked : AuditAction.ManagementLoginFailed,
                    AuditResult.Denied, outcome.ToString(), correlationId, networkIdentifier, userAgent, transactionCancellationToken);
                return ManagementLoginResult.Failed;
            }

            credential!.ClearFailures(now);
            if (verification.RequiresRehash)
            {
                credential.ReplaceHash(_passwordHasher.Hash(password), now);
            }

            UserSession session = UserSession.Create(
                Guid.NewGuid(), user!.Id, now,
                TimeSpan.FromMinutes(_options.InactivityMinutes),
                TimeSpan.FromHours(_options.AbsoluteLifetimeHours));
            identity.UserSessions.Add(session);
            await AppendAuditAsync(
                audit, now, user.OrganizationId, user.Id, AuditActorType.User,
                AuditAction.ManagementLoginSucceeded, AuditResult.Succeeded, null,
                correlationId, networkIdentifier, userAgent, transactionCancellationToken);

            return new ManagementLoginResult(true, session.Id, new ManagementSessionSnapshot(
                session.Id, user.Id, user.OrganizationId, user.Username, user.DisplayName, user.PreferredLanguage,
                session.InactivityExpiresAtUtc, session.AbsoluteExpiresAtUtc, session.LastReauthenticatedAtUtc,
                EffectiveAuthorizationSnapshot.Empty(user.Id, user.OrganizationId)));
        }, cancellationToken);
    }

    public async Task<bool> LogoutAsync(
        Guid sessionId,
        string? networkAddress,
        string? userAgent,
        string correlationId,
        CancellationToken cancellationToken)
    {
        string? networkIdentifier = string.IsNullOrWhiteSpace(networkAddress) ? null : _fingerprints.Fingerprint(networkAddress);
        DateTimeOffset now = _clock.UtcNow;
        return await ExecuteAsync(async (identity, audit, transactionCancellationToken) =>
        {
            UserSession? session = await identity.UserSessions
                .FromSqlInterpolated($"select * from identity.user_sessions where id = {sessionId} for update")
                .SingleOrDefaultAsync(transactionCancellationToken);
            if (session is null || session.RevokedAtUtc is not null)
            {
                return false;
            }

            User user = await identity.Users.SingleAsync(candidate => candidate.Id == session.UserId, transactionCancellationToken);
            session.Revoke(SessionRevocationReason.Logout, now);
            await AppendAuditAsync(
                audit, now, user.OrganizationId, user.Id, AuditActorType.User,
                AuditAction.ManagementLogoutSucceeded, AuditResult.Succeeded, null,
                correlationId, networkIdentifier, userAgent, transactionCancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task<T> ExecuteAsync<T>(
        Func<IdentityDbContext, IAuditWriter, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options;
        var auditOptions = new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connection).Options;
        await using var identity = new IdentityDbContext(identityOptions);
        await using var auditContext = new AuditDbContext(auditOptions);
        await identity.Database.UseTransactionAsync(transaction, cancellationToken);
        await auditContext.Database.UseTransactionAsync(transaction, cancellationToken);

        try
        {
            T result = await operation(identity, new AuditWriter(auditContext), cancellationToken);
            await identity.SaveChangesAsync(cancellationToken);
            await auditContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static Task AppendAuditAsync(
        IAuditWriter audit,
        DateTimeOffset now,
        Guid? organizationId,
        Guid? actorId,
        AuditActorType actorType,
        AuditAction action,
        AuditResult result,
        string? reasonCode,
        string correlationId,
        string? networkIdentifier,
        string? userAgent,
        CancellationToken cancellationToken)
        => audit.AppendAsync(new AuditWriteRequest(
            Guid.NewGuid(), now, organizationId, null, null, actorId, actorType, null,
            action, "ManagementAuthentication", actorId, result, reasonCode, correlationId,
            null, null, networkIdentifier, Truncate(userAgent, 512)), cancellationToken);

    private static string? Truncate(string? value, int maximum)
        => string.IsNullOrWhiteSpace(value) ? null : value.Length <= maximum ? value : value[..maximum];
}

public sealed record ManagementLoginResult(bool Succeeded, Guid SessionId, ManagementSessionSnapshot? Session)
{
    public static ManagementLoginResult Failed { get; } = new(false, Guid.Empty, null);
}
