using Kalm.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordCredential> PasswordCredentials => Set<PasswordCredential>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("users", table =>
            {
                table.HasCheckConstraint("ck_users_status", "status in ('Suspended', 'Active', 'Archived')");
                table.HasCheckConstraint("ck_users_preferred_language", "preferred_language in ('en', 'ar')");
            });
            builder.HasKey(user => user.Id);
            builder.Property(user => user.Id).HasColumnName("id");
            builder.Property(user => user.OrganizationId).HasColumnName("organization_id").IsRequired();
            builder.Property(user => user.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
            builder.Property(user => user.NormalizedUsername).HasColumnName("normalized_username").HasMaxLength(64).IsRequired();
            builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(254);
            builder.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(254);
            builder.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(120).IsRequired();
            builder.Property(user => user.PreferredLanguage).HasColumnName("preferred_language").HasMaxLength(2).IsRequired();
            builder.Property(user => user.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(user => user.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            builder.Property(user => user.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(user => user.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(user => user.ActivatedAtUtc).HasColumnName("activated_at_utc").HasColumnType("timestamptz");
            builder.Property(user => user.ArchivedAtUtc).HasColumnName("archived_at_utc").HasColumnType("timestamptz");
            builder.HasIndex(user => user.NormalizedUsername).IsUnique().HasDatabaseName("ux_users_normalized_username");
            builder.HasIndex(user => user.NormalizedEmail).IsUnique().HasFilter("normalized_email is not null").HasDatabaseName("ux_users_normalized_email");
        });

        modelBuilder.Entity<PasswordCredential>(builder =>
        {
            builder.ToTable("password_credentials", table =>
            {
                table.HasCheckConstraint("ck_password_credentials_status", "status in ('PendingSetup', 'Active', 'Disabled')");
                table.HasCheckConstraint("ck_password_credentials_failure_count", "failed_attempt_count >= 0");
                table.HasCheckConstraint("ck_password_credentials_hash_state", "(status = 'PendingSetup' and encoded_hash is null) or (status <> 'PendingSetup' and encoded_hash is not null)");
            });
            builder.HasKey(credential => credential.Id);
            builder.Property(credential => credential.Id).HasColumnName("id");
            builder.Property(credential => credential.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(credential => credential.EncodedHash).HasColumnName("encoded_hash").HasMaxLength(512);
            builder.Property(credential => credential.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(credential => credential.FailedAttemptCount).HasColumnName("failed_attempt_count").IsRequired();
            builder.Property(credential => credential.FailureWindowStartedAtUtc).HasColumnName("failure_window_started_at_utc").HasColumnType("timestamptz");
            builder.Property(credential => credential.LockedUntilUtc).HasColumnName("locked_until_utc").HasColumnType("timestamptz");
            builder.Property(credential => credential.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            builder.Property(credential => credential.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(credential => credential.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(credential => credential.PasswordChangedAtUtc).HasColumnName("password_changed_at_utc").HasColumnType("timestamptz");
            builder.HasIndex(credential => credential.UserId).IsUnique().HasDatabaseName("ux_password_credentials_user_id");
            builder.HasOne<User>().WithOne().HasForeignKey<PasswordCredential>(credential => credential.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserSession>(builder =>
        {
            builder.ToTable("user_sessions", table =>
            {
                table.HasCheckConstraint("ck_user_sessions_expiry", "created_at_utc <= last_activity_at_utc and last_activity_at_utc < inactivity_expires_at_utc and inactivity_expires_at_utc <= absolute_expires_at_utc");
                table.HasCheckConstraint("ck_user_sessions_revocation_reason", "(revoked_at_utc is null and revocation_reason is null) or (revoked_at_utc is not null and revocation_reason is not null)");
            });
            builder.HasKey(session => session.Id);
            builder.Property(session => session.Id).HasColumnName("id");
            builder.Property(session => session.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(session => session.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(session => session.LastActivityAtUtc).HasColumnName("last_activity_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(session => session.InactivityExpiresAtUtc).HasColumnName("inactivity_expires_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(session => session.AbsoluteExpiresAtUtc).HasColumnName("absolute_expires_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(session => session.LastReauthenticatedAtUtc).HasColumnName("last_reauthenticated_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(session => session.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamptz");
            builder.Property(session => session.RevocationReason).HasColumnName("revocation_reason").HasConversion<string>().HasMaxLength(30);
            builder.Property(session => session.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            builder.HasIndex(session => new { session.UserId, session.RevokedAtUtc, session.AbsoluteExpiresAtUtc }).HasDatabaseName("ix_user_sessions_user_id_revoked_at_absolute_expires_at");
            builder.HasIndex(session => session.InactivityExpiresAtUtc).HasDatabaseName("ix_user_sessions_inactivity_expires_at_utc");
            builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoginAttempt>(builder =>
        {
            builder.ToTable("login_attempts", table => table.HasCheckConstraint("ck_login_attempts_outcome", "outcome in ('Succeeded', 'InvalidCredentials', 'Locked', 'Ineligible')"));
            builder.HasKey(attempt => attempt.Id);
            builder.Property(attempt => attempt.Id).HasColumnName("id");
            builder.Property(attempt => attempt.UserId).HasColumnName("user_id");
            builder.Property(attempt => attempt.IdentifierFingerprint).HasColumnName("identifier_fingerprint").HasMaxLength(64).IsRequired();
            builder.Property(attempt => attempt.FingerprintKeyVersion).HasColumnName("fingerprint_key_version").IsRequired();
            builder.Property(attempt => attempt.NetworkIdentifier).HasColumnName("network_identifier").HasMaxLength(64);
            builder.Property(attempt => attempt.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(attempt => attempt.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(attempt => attempt.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
            builder.HasIndex(attempt => new { attempt.UserId, attempt.OccurredAtUtc }).HasDatabaseName("ix_login_attempts_user_id_occurred_at_utc");
            builder.HasIndex(attempt => new { attempt.IdentifierFingerprint, attempt.OccurredAtUtc }).HasDatabaseName("ix_login_attempts_identifier_fingerprint_occurred_at_utc");
            builder.HasIndex(attempt => new { attempt.NetworkIdentifier, attempt.OccurredAtUtc }).HasDatabaseName("ix_login_attempts_network_identifier_occurred_at_utc");
            builder.HasOne<User>().WithMany().HasForeignKey(attempt => attempt.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
