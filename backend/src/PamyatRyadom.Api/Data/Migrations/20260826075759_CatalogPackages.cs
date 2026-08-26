using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PamyatRyadom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CatalogPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_packages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "ru"),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    includes = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    limits = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    price_from_rub = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    warranty_days = table.Column<int>(type: "integer", nullable: false),
                    visits_label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "draft"),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_packages", x => x.id);
                    table.CheckConstraint("ck_service_packages_price", "price_from_rub > 0");
                    table.CheckConstraint("ck_service_packages_published_at", "(status = 'published') = (published_at IS NOT NULL)");
                    table.CheckConstraint("ck_service_packages_status", "status IN ('draft', 'published', 'archived')");
                    table.CheckConstraint("ck_service_packages_warranty", "warranty_days >= 0");
                });

            migrationBuilder.CreateTable(
                name: "checklist_templates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_package_id = table.Column<long>(type: "bigint", nullable: false),
                    items = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    required_media = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_checklist_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_checklist_templates_service_packages_service_package_id",
                        column: x => x.service_package_id,
                        principalTable: "service_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_checklist_templates_package",
                table: "checklist_templates",
                column: "service_package_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_packages_status",
                table: "service_packages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_service_packages_code_version_locale",
                table: "service_packages",
                columns: new[] { "code", "version", "locale" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checklist_templates");

            migrationBuilder.DropTable(
                name: "service_packages");
        }
    }
}
