using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PamyatRyadom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DispatchVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "visits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    executor_user_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "offered"),
                    scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    offer_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    review_note = table.Column<string>(type: "text", nullable: true),
                    decline_reason = table.Column<string>(type: "text", nullable: true),
                    payout_rub = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    executor_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visits", x => x.id);
                    table.CheckConstraint("ck_visits_payout", "payout_rub IS NULL OR payout_rub >= 0");
                    table.CheckConstraint("ck_visits_status", "status IN ('offered', 'accepted', 'declined', 'in_progress', 'submitted', 'rework', 'approved', 'failed')");
                    table.ForeignKey(
                        name: "fk_visits_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_visits_users_executor_user_id",
                        column: x => x.executor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "visit_checklist_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    visit_id = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    optional = table.Column<bool>(type: "boolean", nullable: false),
                    result = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    note = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visit_checklist_items", x => x.id);
                    table.CheckConstraint("ck_visit_checklist_items_note", "result NOT IN ('impossible', 'not_required') OR (note IS NOT NULL AND length(btrim(note)) > 0)");
                    table.CheckConstraint("ck_visit_checklist_items_result", "result IN ('pending', 'done', 'impossible', 'not_required')");
                    table.ForeignKey(
                        name: "fk_visit_checklist_items_visits_visit_id",
                        column: x => x.visit_id,
                        principalTable: "visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_visit_checklist_items_key",
                table: "visit_checklist_items",
                columns: new[] { "visit_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visits_executor_user_id_status",
                table: "visits",
                columns: new[] { "executor_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_visits_order_id_status",
                table: "visits",
                columns: new[] { "order_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "visit_checklist_items");

            migrationBuilder.DropTable(
                name: "visits");
        }
    }
}
