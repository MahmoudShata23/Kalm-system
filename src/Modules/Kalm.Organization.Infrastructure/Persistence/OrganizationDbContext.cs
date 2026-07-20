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
            builder.HasOne<OrganizationAggregate>().WithMany().HasForeignKey(branch => branch.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
