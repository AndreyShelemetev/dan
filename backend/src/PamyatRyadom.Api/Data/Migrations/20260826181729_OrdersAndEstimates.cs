using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PamyatRyadom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrdersAndEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    customer_user_id = table.Column<long>(type: "bigint", nullable: false),
                    burial_site_id = table.Column<long>(type: "bigint", nullable: false),
                    service_package_id = table.Column<long>(type: "bigint", nullable: false),
                    package_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    package_version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    package_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    package_price_from_rub = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    warranty_days = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "draft"),
                    preferred_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    preferred_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    customer_comment = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.CheckConstraint("ck_orders_package_price", "package_price_from_rub > 0");
                    table.CheckConstraint("ck_orders_status", "status IN ('draft', 'submitted', 'location_review', 'estimate_ready', 'awaiting_payment', 'paid', 'assigning', 'assigned', 'in_progress', 'extra_approval', 'qa_review', 'customer_review', 'completed', 'disputed', 'cancelled', 'refunded')");
                    table.CheckConstraint("ck_orders_warranty", "warranty_days >= 0");
                    table.CheckConstraint("ck_orders_window", "preferred_from IS NULL OR preferred_to IS NULL OR preferred_from <= preferred_to");
                    table.ForeignKey(
                        name: "fk_orders_burial_sites_burial_site_id",
                        column: x => x.burial_site_id,
                        principalTable: "burial_sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_service_packages_service_package_id",
                        column: x => x.service_package_id,
                        principalTable: "service_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_users_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estimates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "draft"),
                    total_rub = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    published_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estimates", x => x.id);
                    table.CheckConstraint("ck_estimates_published_at", "status = 'draft' OR published_at IS NOT NULL");
                    table.CheckConstraint("ck_estimates_status", "status IN ('draft', 'published', 'accepted', 'rejected', 'superseded')");
                    table.CheckConstraint("ck_estimates_total", "total_rub >= 0");
                    table.CheckConstraint("ck_estimates_version", "version > 0");
                    table.ForeignKey(
                        name: "fk_estimates_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    actor_user_id = table.Column<long>(type: "bigint", nullable: true),
                    actor_role = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_status_history", x => x.id);
                    table.CheckConstraint("ck_order_status_history_from", "from_status IS NULL OR from_status IN ('draft', 'submitted', 'location_review', 'estimate_ready', 'awaiting_payment', 'paid', 'assigning', 'assigned', 'in_progress', 'extra_approval', 'qa_review', 'customer_review', 'completed', 'disputed', 'cancelled', 'refunded')");
                    table.CheckConstraint("ck_order_status_history_to", "to_status IN ('draft', 'submitted', 'location_review', 'estimate_ready', 'awaiting_payment', 'paid', 'assigning', 'assigned', 'in_progress', 'extra_approval', 'qa_review', 'customer_review', 'completed', 'disputed', 'cancelled', 'refunded')");
                    table.ForeignKey(
                        name: "fk_order_status_history_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "estimate_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    estimate_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 1m),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    unit_price_rub = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estimate_lines", x => x.id);
                    table.CheckConstraint("ck_estimate_lines_price_sign", "(type = 'discount' AND unit_price_rub <= 0) OR (type <> 'discount' AND unit_price_rub >= 0)");
                    table.CheckConstraint("ck_estimate_lines_quantity", "quantity > 0");
                    table.CheckConstraint("ck_estimate_lines_type", "type IN ('work', 'material', 'discount', 'delivery', 'extra')");
                    table.ForeignKey(
                        name: "fk_estimate_lines_estimates_estimate_id",
                        column: x => x.estimate_id,
                        principalTable: "estimates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_estimate_lines_estimate_id_sort_order",
                table: "estimate_lines",
                columns: new[] { "estimate_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ux_estimates_order_published",
                table: "estimates",
                column: "order_id",
                unique: true,
                filter: "status = 'published'");

            migrationBuilder.CreateIndex(
                name: "ux_estimates_order_version",
                table: "estimates",
                columns: new[] { "order_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_status_history_order_id_created_at",
                table: "order_status_history",
                columns: new[] { "order_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_burial_site_id",
                table: "orders",
                column: "burial_site_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_user_id_status",
                table: "orders",
                columns: new[] { "customer_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_service_package_id",
                table: "orders",
                column: "service_package_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_status",
                table: "orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_orders_number",
                table: "orders",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "estimate_lines");

            migrationBuilder.DropTable(
                name: "order_status_history");

            migrationBuilder.DropTable(
                name: "estimates");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
