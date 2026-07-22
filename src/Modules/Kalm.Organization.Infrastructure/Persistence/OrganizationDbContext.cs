using Kalm.Organization.Domain;
using Microsoft.EntityFrameworkCore;
using OrganizationAggregate = Kalm.Organization.Domain.Organization;

namespace Kalm.Organization.Infrastructure.Persistence;

public sealed class OrganizationDbContext : DbContext
{
    public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
        : base(options)
    {
    }

    public DbSet<OrganizationAggregate> Organizations => Set<OrganizationAggregate>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<UserBranchAccess> UserBranchAccesses => Set<UserBranchAccess>();
    public DbSet<UserBranchAssignment> UserBranchAssignments => Set<UserBranchAssignment>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DevicePairingChallenge> DevicePairingChallenges => Set<DevicePairingChallenge>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("organization");

        modelBuilder.Entity<OrganizationAggregate>(builder =>
        {
            builder.ToTable("organizations", table => table.HasCheckConstraint("ck_organizations_singleton_key", "singleton_key = 1"));
            builder.HasKey(organization => organization.Id);
            builder.Property(organization => organization.Id).HasColumnName("id");
            builder.Property<int>("SingletonKey").HasColumnName("singleton_key").HasDefaultValue(1).IsRequired();
            builder.HasIndex("SingletonKey").IsUnique().HasDatabaseName("ux_organizations_singleton_key");
            builder.Property(organization => organization.BrandName).HasColumnName("brand_name").HasMaxLength(120).IsRequired();
            builder.Property(organization => organization.LegalName).HasColumnName("legal_name").HasMaxLength(160);
            builder.Property(organization => organization.DefaultCurrencyCode).HasColumnName("default_currency_code").HasMaxLength(3).IsRequired();
            builder.Property(organization => organization.DefaultLocaleCode).HasColumnName("default_locale_code").HasMaxLength(20).IsRequired();
            builder.Property(organization => organization.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(organization => organization.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            builder.Property(organization => organization.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(organization => organization.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        });

        modelBuilder.Entity<Branch>(builder =>
        {
            builder.ToTable("branches");
            builder.HasKey(branch => branch.Id);
            builder.Property(branch => branch.Id).HasColumnName("id");
            builder.Property(branch => branch.OrganizationId).HasColumnName("organization_id").IsRequired();
            builder.Property(branch => branch.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            builder.Property(branch => branch.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
            builder.Property(branch => branch.LocaleCode).HasColumnName("locale_code").HasMaxLength(20).IsRequired();
            builder.Property(branch => branch.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(128).IsRequired();
            builder.Property(branch => branch.BusinessDayRollover).HasColumnName("business_day_rollover").HasColumnType("time without time zone").IsRequired();
            builder.Property(branch => branch.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(branch => branch.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            builder.Property(branch => branch.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(branch => branch.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.HasIndex(branch => new { branch.OrganizationId, branch.Code }).IsUnique().HasDatabaseName("ux_branches_organization_id_code");
            builder.HasAlternateKey(branch => new { branch.Id, branch.OrganizationId }).HasName("ak_branches_id_organization_id");
            builder.HasOne<OrganizationAggregate>().WithMany().HasForeignKey(branch => branch.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserBranchAccess>(builder =>
        {
            builder.ToTable("user_branch_access", table => table.HasCheckConstraint("ck_user_branch_access_scope", "scope in ('AssignedBranches', 'AllOrganizationBranches')"));
            builder.HasKey(access => access.Id);
            builder.Property(access => access.Id).HasColumnName("id");
            builder.Property(access => access.OrganizationId).HasColumnName("organization_id").IsRequired();
            builder.Property(access => access.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(access => access.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(access => access.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            builder.Property(access => access.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(access => access.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.HasIndex(access => access.UserId).IsUnique().HasDatabaseName("ux_user_branch_access_user_id");
            builder.HasAlternateKey(access => new { access.Id, access.OrganizationId }).HasName("ak_user_branch_access_id_organization_id");
            builder.HasOne<OrganizationAggregate>().WithMany().HasForeignKey(access => access.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserBranchAssignment>(builder =>
        {
            builder.ToTable("user_branch_assignments", table => table.HasCheckConstraint("ck_user_branch_assignments_revocation", "revoked_at_utc is null or revoked_at_utc >= assigned_at_utc"));
            builder.HasKey(assignment => assignment.Id);
            builder.Property(assignment => assignment.Id).HasColumnName("id");
            builder.Property(assignment => assignment.AccessId).HasColumnName("access_id").IsRequired();
            builder.Property(assignment => assignment.OrganizationId).HasColumnName("organization_id").IsRequired();
            builder.Property(assignment => assignment.BranchId).HasColumnName("branch_id").IsRequired();
            builder.Property(assignment => assignment.AssignedAtUtc).HasColumnName("assigned_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(assignment => assignment.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamptz");
            builder.Property(assignment => assignment.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            builder.HasIndex(assignment => new { assignment.AccessId, assignment.BranchId }).IsUnique().HasFilter("revoked_at_utc is null").HasDatabaseName("ux_user_branch_assignments_active");
            builder.HasOne<UserBranchAccess>().WithMany().HasForeignKey(assignment => new { assignment.AccessId, assignment.OrganizationId }).HasPrincipalKey(access => new { access.Id, access.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Branch>().WithMany().HasForeignKey(assignment => new { assignment.BranchId, assignment.OrganizationId }).HasPrincipalKey(branch => new { branch.Id, branch.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Device>(builder =>
        {
            builder.ToTable("devices", table => table.HasCheckConstraint("ck_devices_status", "status in ('PendingPairing', 'Active', 'Revoked')"));
            builder.HasKey(device => device.Id);
            builder.Property(device => device.Id).HasColumnName("id");
            builder.Property(device => device.OrganizationId).HasColumnName("organization_id").IsRequired();
            builder.Property(device => device.BranchId).HasColumnName("branch_id").IsRequired();
            builder.Property(device => device.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            builder.Property(device => device.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(device => device.Platform).HasColumnName("platform").HasMaxLength(120);
            builder.Property(device => device.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(device => device.SecurityVersion).HasColumnName("security_version").IsRequired();
            builder.Property(device => device.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            builder.Property(device => device.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(device => device.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(device => device.PairedAtUtc).HasColumnName("paired_at_utc").HasColumnType("timestamptz");
            builder.Property(device => device.LastSeenAtUtc).HasColumnName("last_seen_at_utc").HasColumnType("timestamptz");
            builder.Property(device => device.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamptz");
            builder.HasIndex(device => new { device.OrganizationId, device.Status, device.Name, device.Id }).HasDatabaseName("ix_devices_organization_status_name_id");
            builder.HasAlternateKey(device => new { device.Id, device.OrganizationId }).HasName("ak_devices_id_organization_id");
            builder.HasOne<Branch>().WithMany().HasForeignKey(device => new { device.BranchId, device.OrganizationId }).HasPrincipalKey(branch => new { branch.Id, branch.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DevicePairingChallenge>(builder =>
        {
            builder.ToTable("device_pairing_challenges", table => table.HasCheckConstraint("ck_device_pairing_challenges_expiry", "created_at_utc < expires_at_utc"));
            builder.HasKey(challenge => challenge.Id);
            builder.Property(challenge => challenge.Id).HasColumnName("id");
            builder.Property(challenge => challenge.DeviceId).HasColumnName("device_id").IsRequired();
            builder.Property(challenge => challenge.ChallengeHash).HasColumnName("challenge_hash").HasMaxLength(128).IsRequired();
            builder.Property(challenge => challenge.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(challenge => challenge.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(challenge => challenge.ConsumedAtUtc).HasColumnName("consumed_at_utc").HasColumnType("timestamptz");
            builder.Property(challenge => challenge.InvalidatedAtUtc).HasColumnName("invalidated_at_utc").HasColumnType("timestamptz");
            builder.HasIndex(challenge => challenge.ChallengeHash).IsUnique().HasDatabaseName("ux_device_pairing_challenges_hash");
            builder.HasIndex(challenge => new { challenge.DeviceId, challenge.ExpiresAtUtc }).HasDatabaseName("ix_device_pairing_challenges_device_expiry");
            builder.HasOne<Device>().WithMany().HasForeignKey(challenge => challenge.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeviceCredential>(builder =>
        {
            builder.ToTable("device_credentials");
            builder.HasKey(credential => credential.Id);
            builder.Property(credential => credential.Id).HasColumnName("id");
            builder.Property(credential => credential.DeviceId).HasColumnName("device_id").IsRequired();
            builder.Property(credential => credential.CredentialHash).HasColumnName("credential_hash").HasMaxLength(128).IsRequired();
            builder.Property(credential => credential.SecurityVersion).HasColumnName("security_version").IsRequired();
            builder.Property(credential => credential.IssuedAtUtc).HasColumnName("issued_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(credential => credential.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamptz");
            builder.HasIndex(credential => credential.CredentialHash).IsUnique().HasDatabaseName("ux_device_credentials_hash");
            builder.HasIndex(credential => credential.DeviceId).IsUnique().HasFilter("revoked_at_utc is null").HasDatabaseName("ux_device_credentials_active");
            builder.HasOne<Device>().WithMany().HasForeignKey(credential => credential.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
