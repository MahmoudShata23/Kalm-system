using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Kalm.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arabic_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    english_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_arabic_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_english_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pos_color_token = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    icon_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.UniqueConstraint("ak_catalog_categories_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_catalog_categories_display_order", "display_order >= 0");
                    table.CheckConstraint("ck_catalog_categories_normalized_names", "length(normalized_arabic_name) > 0 and length(normalized_english_name) > 0");
                    table.CheckConstraint("ck_catalog_categories_status", "status in ('Active', 'Archived')");
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arabic_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    english_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_arabic_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_english_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    arabic_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    english_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sku = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    product_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.UniqueConstraint("ak_catalog_products_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_catalog_products_display_order", "display_order >= 0");
                    table.CheckConstraint("ck_catalog_products_normalized_values", "length(normalized_arabic_name) > 0 and length(normalized_english_name) > 0 and length(sku) > 0");
                    table.CheckConstraint("ck_catalog_products_status", "status in ('Active', 'Archived')");
                    table.CheckConstraint("ck_catalog_products_type", "product_type in ('MadeToOrder', 'PurchasedFinishedGood', 'ServiceNonStock')");
                    table.ForeignKey(
                        name: "FK_products_categories_category_id_organization_id",
                        columns: x => new { x.category_id, x.organization_id },
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arabic_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    english_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_arabic_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_english_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    size_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    temperature_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    serving_format_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.id);
                    table.CheckConstraint("ck_catalog_product_variants_display_order", "display_order >= 0");
                    table.CheckConstraint("ck_catalog_product_variants_normalized_values", "length(normalized_arabic_name) > 0 and length(normalized_english_name) > 0 and length(code) > 0");
                    table.CheckConstraint("ck_catalog_product_variants_status", "status in ('Active', 'Archived')");
                    table.ForeignKey(
                        name: "FK_product_variants_products_product_id_organization_id",
                        columns: x => new { x.product_id, x.organization_id },
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_catalog_categories_organization_status_order_id",
                schema: "catalog",
                table: "categories",
                columns: new[] { "organization_id", "status", "display_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_catalog_categories_organization_arabic_name",
                schema: "catalog",
                table: "categories",
                columns: new[] { "organization_id", "normalized_arabic_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_catalog_categories_organization_english_name",
                schema: "catalog",
                table: "categories",
                columns: new[] { "organization_id", "normalized_english_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_product_variants_product_status_order_id",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "product_id", "status", "display_order", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_product_id_organization_id",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "product_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ux_catalog_product_variants_organization_barcode",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "organization_id", "barcode" },
                unique: true,
                filter: "barcode is not null");

            migrationBuilder.CreateIndex(
                name: "ux_catalog_product_variants_organization_code",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "organization_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_products_organization_category_status_order_id",
                schema: "catalog",
                table: "products",
                columns: new[] { "organization_id", "category_id", "status", "display_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_catalog_products_organization_status_order_id",
                schema: "catalog",
                table: "products",
                columns: new[] { "organization_id", "status", "display_order", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id_organization_id",
                schema: "catalog",
                table: "products",
                columns: new[] { "category_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ux_catalog_products_organization_sku",
                schema: "catalog",
                table: "products",
                columns: new[] { "organization_id", "sku" },
                unique: true);

            migrationBuilder.Sql(
                """
                create function catalog.assert_product_invariants(target_product_id uuid)
                returns void
                language plpgsql
                as $$
                declare
                    product_status varchar(20);
                    product_category_id uuid;
                    product_organization_id uuid;
                    category_status varchar(20);
                begin
                    select status, category_id, organization_id
                    into product_status, product_category_id, product_organization_id
                    from catalog.products
                    where id = target_product_id;

                    if not found then
                        return;
                    end if;

                    if not exists (
                        select 1 from catalog.product_variants
                        where product_id = target_product_id
                    ) then
                        raise exception using
                            errcode = '23514',
                            constraint = 'ck_catalog_product_has_variant',
                            message = 'A product must contain at least one variant.';
                    end if;

                    if product_status = 'Active' then
                        select status into category_status
                        from catalog.categories
                        where id = product_category_id
                          and organization_id = product_organization_id;

                        if category_status is distinct from 'Active' then
                            raise exception using
                                errcode = '23514',
                                constraint = 'ck_catalog_active_product_category',
                                message = 'An active product requires an active category.';
                        end if;

                        if not exists (
                            select 1 from catalog.product_variants
                            where product_id = target_product_id
                              and status = 'Active'
                        ) then
                            raise exception using
                                errcode = '23514',
                                constraint = 'ck_catalog_active_product_variant',
                                message = 'An active product requires at least one active variant.';
                        end if;
                    end if;
                end;
                $$;

                create function catalog.check_product_row_invariants()
                returns trigger
                language plpgsql
                as $$
                begin
                    perform catalog.assert_product_invariants(new.id);
                    return null;
                end;
                $$;

                create function catalog.check_variant_product_invariants()
                returns trigger
                language plpgsql
                as $$
                begin
                    if tg_op = 'DELETE' then
                        perform catalog.assert_product_invariants(old.product_id);
                    else
                        perform catalog.assert_product_invariants(new.product_id);
                        if tg_op = 'UPDATE' and old.product_id is distinct from new.product_id then
                            perform catalog.assert_product_invariants(old.product_id);
                        end if;
                    end if;
                    return null;
                end;
                $$;

                create function catalog.check_category_product_invariants()
                returns trigger
                language plpgsql
                as $$
                declare
                    product_id uuid;
                begin
                    for product_id in
                        select id from catalog.products
                        where category_id = new.id
                          and organization_id = new.organization_id
                          and status = 'Active'
                    loop
                        perform catalog.assert_product_invariants(product_id);
                    end loop;
                    return null;
                end;
                $$;

                create constraint trigger trg_catalog_products_invariants
                after insert or update on catalog.products
                deferrable initially deferred
                for each row execute function catalog.check_product_row_invariants();

                create constraint trigger trg_catalog_product_variants_invariants
                after insert or update or delete on catalog.product_variants
                deferrable initially deferred
                for each row execute function catalog.check_variant_product_invariants();

                create constraint trigger trg_catalog_categories_product_invariants
                after update on catalog.categories
                deferrable initially deferred
                for each row execute function catalog.check_category_product_invariants();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                drop trigger if exists trg_catalog_categories_product_invariants on catalog.categories;
                drop trigger if exists trg_catalog_product_variants_invariants on catalog.product_variants;
                drop trigger if exists trg_catalog_products_invariants on catalog.products;
                drop function if exists catalog.check_category_product_invariants();
                drop function if exists catalog.check_variant_product_invariants();
                drop function if exists catalog.check_product_row_invariants();
                drop function if exists catalog.assert_product_invariants(uuid);
                """);

            migrationBuilder.DropTable(
                name: "product_variants",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "products",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "catalog");
        }
    }
}
#pragma warning restore CA1861
