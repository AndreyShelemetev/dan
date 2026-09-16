using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PamyatRyadom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Disputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "disputes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    opened_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "open"),
                    resolution_type = table.Column<string>(type: "text", nullable: true),
                    resolution_text = table.Column<string>(type: "text", nullable: true),
                    refund_amount_rub = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    resolved_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disputes", x => x.id);
                    table.CheckConstraint("ck_disputes_reason", "length(btrim(reason)) > 0");
                    table.CheckConstraint("ck_disputes_refund_amount", "refund_amount_rub IS NULL OR refund_amount_rub >= 0");
                    table.CheckConstraint("ck_disputes_refund_amount_scope", "refund_amount_rub IS NULL OR resolution_type = 'partial_refund'");
                    table.CheckConstraint("ck_disputes_resolution_consistency", "(status IN ('resolved', 'rejected') AND resolution_type IS NOT NULL AND resolution_text IS NOT NULL AND length(btrim(resolution_text)) > 0 AND resolved_by_user_id IS NOT NULL AND resolved_at IS NOT NULL) OR (status NOT IN ('resolved', 'rejected') AND resolution_type IS NULL AND resolution_text IS NULL AND resolved_by_user_id IS NULL AND resolved_at IS NULL)");
                    table.CheckConstraint("ck_disputes_resolution_type", "resolution_type IS NULL OR resolution_type IN ('rework', 'partial_refund', 'full_refund', 'rejected')");
                    table.CheckConstraint("ck_disputes_status", "status IN ('open', 'in_review', 'resolved', 'rejected')");
                    table.ForeignKey(
                        name: "fk_disputes_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disputes_users_opened_by_user_id",
                        column: x => x.opened_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disputes_users_resolved_by_user_id",
                        column: x => x.resolved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_disputes_opened_by_user_id",
                table: "disputes",
                column: "opened_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_order_id_status",
                table: "disputes",
                columns: new[] { "order_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_disputes_resolved_by_user_id",
                table: "disputes",
                column: "resolved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_disputes_order_id_live",
                table: "disputes",
                column: "order_id",
                unique: true,
                filter: "status IN ('open', 'in_review')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "disputes");
        }
    }
}
