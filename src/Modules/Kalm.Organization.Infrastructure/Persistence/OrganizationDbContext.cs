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
    }
}
