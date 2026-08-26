using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PamyatRyadom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionPlansAndCatalogAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_security_audit_logs_event_type",
                table: "security_audit_logs");

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "ru"),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    service_package_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    visits_total = table.Column<int>(type: "integer", nullable: false),
                    period_months = table.Column<int>(type: "integer", nullable: false),
                    price_rub = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "draft"),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plans", x => x.id);
                    table.CheckConstraint("ck_subscription_plans_period", "period_months > 0");
                    table.CheckConstraint("ck_subscription_plans_price", "price_rub > 0");
                    table.CheckConstraint("ck_subscription_plans_published_at", "(status = 'published') = (published_at IS NOT NULL)");
                    table.CheckConstraint("ck_subscription_plans_status", "status IN ('draft', 'published', 'archived')");
                    table.CheckConstraint("ck_subscription_plans_visits", "visits_total > 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_security_audit_logs_event_type",
                table: "security_audit_logs",
                sql: "event_type IN ('login_success', 'login_failed', 'logout', 'otp_requested', 'session_revoked', 'role_changed', 'mfa_enrolled', 'mfa_verified', 'catalog_changed')");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plans_status",
                table: "subscription_plans",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_subscription_plans_code_version_locale",
                table: "subscription_plans",
                columns: new[] { "code", "version", "locale" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_security_audit_logs_event_type",
                table: "security_audit_logs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_security_audit_logs_event_type",
                table: "security_audit_logs",
                sql: "event_type IN ('login_success', 'login_failed', 'logout', 'otp_requested', 'session_revoked', 'role_changed', 'mfa_enrolled', 'mfa_verified')");
        }
    }
}
