using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PamyatRyadom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminUsersAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_security_audit_logs_event_type",
                table: "security_audit_logs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_security_audit_logs_event_type",
                table: "security_audit_logs",
                sql: "event_type IN ('login_success', 'login_failed', 'logout', 'otp_requested', 'session_revoked', 'role_changed', 'mfa_enrolled', 'mfa_verified', 'catalog_changed', 'user_created', 'user_status_changed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_security_audit_logs_event_type",
                table: "security_audit_logs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_security_audit_logs_event_type",
                table: "security_audit_logs",
                sql: "event_type IN ('login_success', 'login_failed', 'logout', 'otp_requested', 'session_revoked', 'role_changed', 'mfa_enrolled', 'mfa_verified', 'catalog_changed')");
        }
    }
}
