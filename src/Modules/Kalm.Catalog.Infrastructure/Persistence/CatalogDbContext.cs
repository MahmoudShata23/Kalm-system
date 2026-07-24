using Kalm.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");

        modelBuilder.Entity<Category>(builder =>
        {
            builder.ToTable("categories", table =>
            {
                table.HasCheckConstraint("ck_catalog_categories_status", "status in ('Active', 'Archived')");
                table.HasCheckConstraint("ck_catalog_categories_display_order", "display_order >= 0");
                table.HasCheckConstraint("ck_catalog_categories_normalized_names", "length(normalized_arabic_name) > 0 and length(normalized_english_name) > 0");
            });
            builder.HasKey(category => category.Id);
            builder.Property(category => category.Id).HasColumnName("id");
            builder.Property(category => category.OrganizationId).HasColumnName("organization_id").IsRequired();
            builder.Property(category => category.ArabicName).HasColumnName("arabic_name").HasMaxLength(120).IsRequired();
            builder.Property(category => category.EnglishName).HasColumnName("english_name").HasMaxLength(120).IsRequired();
            builder.Property(category => category.NormalizedArabicName).HasColumnName("normalized_arabic_name").HasMaxLength(120).IsRequired();
            builder.Property(category => category.NormalizedEnglishName).HasColumnName("normalized_english_name").HasMaxLength(120).IsRequired();
            builder.Property(category => category.DisplayOrder).HasColumnName("display_order").IsRequired();
            builder.Property(category => category.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(category => category.PosColorToken).HasColumnName("pos_color_token").HasMaxLength(30);
            builder.Property(category => category.IconCode).HasColumnName("icon_code").HasMaxLength(40);
            builder.Property(category => category.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            builder.Property(category => category.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(category => category.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.HasAlternateKey(category => new { category.Id, category.OrganizationId }).HasName("ak_catalog_categories_id_organization_id");
            builder.HasIndex(category => new { category.OrganizationId, category.NormalizedArabicName }).IsUnique().HasDatabaseName("ux_catalog_categories_organization_arabic_name");
            builder.HasIndex(category => new { category.OrganizationId, category.NormalizedEnglishName }).IsUnique().HasDatabaseName("ux_catalog_categories_organization_english_name");
            builder.HasIndex(category => new { category.OrganizationId, category.Status, category.DisplayOrder, category.Id }).HasDatabaseName("ix_catalog_categories_organization_status_order_id");
        });

        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("products", table =>
            {
                table.HasCheckConstraint("ck_catalog_products_status", "status in ('Active', 'Archived')");
                table.HasCheckConstraint("ck_catalog_products_type", "product_type in ('MadeToOrder', 'PurchasedFinishedGood', 'ServiceNonStock')");
                table.HasCheckConstraint("ck_catalog_products_display_order", "display_order >= 0");
                table.HasCheckConstraint("ck_catalog_products_normalized_values", "length(normalized_arabic_name) > 0 and length(normalized_english_name) > 0 and length(sku) > 0");
            });
            builder.HasKey(product => product.Id);
            builder.Property(product => product.Id).HasColumnName("id");
            builder.Property(product => product.OrganizationId).HasColumnName("organization_id").IsRequired();
            builder.Property(product => product.CategoryId).HasColumnName("category_id").IsRequired();
            builder.Property(product => product.ArabicName).HasColumnName("arabic_name").HasMaxLength(120).IsRequired();
            builder.Property(product => product.EnglishName).HasColumnName("english_name").HasMaxLength(120).IsRequired();
            builder.Property(product => product.NormalizedArabicName).HasColumnName("normalized_arabic_name").HasMaxLength(120).IsRequired();
            builder.Property(product => product.NormalizedEnglishName).HasColumnName("normalized_english_name").HasMaxLength(120).IsRequired();
            builder.Property(product => product.ArabicDescription).HasColumnName("arabic_description").HasMaxLength(1000);
            builder.Property(product => product.EnglishDescription).HasColumnName("english_description").HasMaxLength(1000);
            builder.Property(product => product.Sku).HasColumnName("sku").HasMaxLength(40).IsRequired();
            builder.Property(product => product.Type).HasColumnName("product_type").HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(product => product.DisplayOrder).HasColumnName("display_order").IsRequired();
            builder.Property(product => product.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(product => product.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            builder.Property(product => product.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(product => product.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.HasAlternateKey(product => new { product.Id, product.OrganizationId }).HasName("ak_catalog_products_id_organization_id");
            builder.HasIndex(product => new { product.OrganizationId, product.Sku }).IsUnique().HasDatabaseName("ux_catalog_products_organization_sku");
            builder.HasIndex(product => new { product.OrganizationId, product.Status, product.DisplayOrder, product.Id }).HasDatabaseName("ix_catalog_products_organization_status_order_id");
            builder.HasIndex(product => new { product.OrganizationId, product.CategoryId, product.Status, product.DisplayOrder, product.Id }).HasDatabaseName("ix_catalog_products_organization_category_status_order_id");
            builder.HasOne<Category>().WithMany()
                .HasForeignKey(product => new { product.CategoryId, product.OrganizationId })
                .HasPrincipalKey(category => new { category.Id, category.OrganizationId })
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(product => product.Variants).WithOne()
                .HasForeignKey(variant => new { variant.ProductId, variant.OrganizationId })
                .HasPrincipalKey(product => new { product.Id, product.OrganizationId })
                .OnDelete(DeleteBehavior.Restrict);
            builder.Navigation(product => product.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ProductVariant>(builder =>
        {
            builder.ToTable("product_variants", table =>
            {
                table.HasCheckConstraint("ck_catalog_product_variants_status", "status in ('Active', 'Archived')");
                table.HasCheckConstraint("ck_catalog_product_variants_display_order", "display_order >= 0");
                table.HasCheckConstraint("ck_catalog_product_variants_normalized_values", "length(normalized_arabic_name) > 0 and length(normalized_english_name) > 0 and length(code) > 0");
            });
            builder.HasKey(variant => variant.Id);
            builder.Property(variant => variant.Id).HasColumnName("id");
            builder.Property(variant => variant.ProductId).HasColumnName("product_id").IsRequired();
            builder.Property(variant => variant.OrganizationId).HasColumnName("organization_id").IsRequired();
            builder.Property(variant => variant.ArabicName).HasColumnName("arabic_name").HasMaxLength(120).IsRequired();
            builder.Property(variant => variant.EnglishName).HasColumnName("english_name").HasMaxLength(120).IsRequired();
            builder.Property(variant => variant.NormalizedArabicName).HasColumnName("normalized_arabic_name").HasMaxLength(120).IsRequired();
            builder.Property(variant => variant.NormalizedEnglishName).HasColumnName("normalized_english_name").HasMaxLength(120).IsRequired();
            builder.Property(variant => variant.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
            builder.Property(variant => variant.Barcode).HasColumnName("barcode").HasMaxLength(64);
            builder.Property(variant => variant.SizeCode).HasColumnName("size_code").HasMaxLength(30);
            builder.Property(variant => variant.TemperatureCode).HasColumnName("temperature_code").HasMaxLength(30);
            builder.Property(variant => variant.ServingFormatCode).HasColumnName("serving_format_code").HasMaxLength(30);
            builder.Property(variant => variant.DisplayOrder).HasColumnName("display_order").IsRequired();
            builder.Property(variant => variant.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(variant => variant.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.Property(variant => variant.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
            builder.HasIndex(variant => new { variant.OrganizationId, variant.Code }).IsUnique().HasDatabaseName("ux_catalog_product_variants_organization_code");
            builder.HasIndex(variant => new { variant.OrganizationId, variant.Barcode }).IsUnique().HasFilter("barcode is not null").HasDatabaseName("ux_catalog_product_variants_organization_barcode");
            builder.HasIndex(variant => new { variant.ProductId, variant.Status, variant.DisplayOrder, variant.Id }).HasDatabaseName("ix_catalog_product_variants_product_status_order_id");
        });
    }
}
