using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PamyatRyadom.Api.Data.Migrations
{
    /// <summary>
    /// Brings the status-like columns of the Identity schema in line with CONVENTIONS.md §3:
    /// every column whose allowed values are fixed by a named <c>ck_</c> CHECK is <c>text</c>,
    /// never <c>character varying(n)</c>. The CHECK stays the single authority on the value set.
    ///
    /// The <c>ck_</c> constraints are dropped and re-added around the type change on purpose.
    /// Postgres does preserve a CHECK across <c>ALTER COLUMN ... TYPE</c>, but it preserves the
    /// *frozen parse tree*, leaving definitions like
    /// <c>role = ANY (ARRAY[('client'::character varying)::text, ...])</c> behind on a text column.
    /// That is semantically identical but reads as though the migration only half-ran, so the
    /// constraints are recreated against the new type to keep <c>pg_get_constraintdef</c> honest.
    /// Same reasoning for the re-stated column defaults.
    /// </summary>
    public partial class StatusColumnsToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop the value-set CHECKs so they are not carried across the type change.
            migrationBuilder.DropCheckConstraint(name: "ck_users_role", table: "users");
            migrationBuilder.DropCheckConstraint(name: "ck_users_status", table: "users");
            migrationBuilder.DropCheckConstraint(name: "ck_legal_documents_type", table: "legal_documents");
            migrationBuilder.DropCheckConstraint(name: "ck_legal_documents_status", table: "legal_documents");
            migrationBuilder.DropCheckConstraint(name: "ck_otp_codes_channel", table: "otp_codes");
            migrationBuilder.DropCheckConstraint(name: "ck_otp_codes_purpose", table: "otp_codes");
            migrationBuilder.DropCheckConstraint(name: "ck_consent_logs_type", table: "consent_logs");
            migrationBuilder.DropCheckConstraint(name: "ck_auth_identities_provider", table: "auth_identities");
            migrationBuilder.DropCheckConstraint(name: "ck_security_audit_logs_event_type", table: "security_audit_logs");

            // 2. character varying(n) -> text.
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "active",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "active");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "client",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "client");

            migrationBuilder.AlterColumn<string>(
                name: "event_type",
                table: "security_audit_logs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "actor_role",
                table: "security_audit_logs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "purpose",
                table: "otp_codes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "channel",
                table: "otp_codes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "legal_documents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "legal_documents",
                type: "text",
                nullable: false,
                defaultValue: "draft",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldDefaultValue: "draft");

            migrationBuilder.AlterColumn<string>(
                name: "consent_type",
                table: "consent_logs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "provider",
                table: "auth_identities",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            // 3. Re-state the defaults so they are 'x'::text rather than 'x'::character varying.
            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "client",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "active",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "legal_documents",
                type: "text",
                nullable: false,
                defaultValue: "draft",
                oldClrType: typeof(string),
                oldType: "text");

            // 4. Re-add the value-set CHECKs, now compiled against text columns.
            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role",
                table: "users",
                sql: "role IN ('client', 'executor', 'dispatcher', 'qa', 'support', 'finance', 'admin', 'superadmin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_status",
                table: "users",
                sql: "status IN ('active', 'blocked', 'deleted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_legal_documents_type",
                table: "legal_documents",
                sql: "type IN ('oferta_client', 'oferta_executor', 'privacy', 'cookies', 'refund_policy')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_legal_documents_status",
                table: "legal_documents",
                sql: "status IN ('draft', 'published', 'archived')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_otp_codes_channel",
                table: "otp_codes",
                sql: "channel IN ('email', 'sms')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_otp_codes_purpose",
                table: "otp_codes",
                sql: "purpose IN ('login', 'verify_email')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consent_logs_type",
                table: "consent_logs",
                sql: "consent_type IN ('personal_data', 'marketing', 'cookies')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_auth_identities_provider",
                table: "auth_identities",
                sql: "provider IN ('email', 'phone')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_security_audit_logs_event_type",
                table: "security_audit_logs",
                sql: "event_type IN ('login_success', 'login_failed', 'logout', 'otp_requested', 'session_revoked', 'role_changed', 'mfa_enrolled', 'mfa_verified')");

            // 5. actor_role snapshots users.role but never had a CHECK of its own — it was a
            //    status-like column enforced only in C#. Add the missing one (nullable variant).
            migrationBuilder.AddCheckConstraint(
                name: "ck_security_audit_logs_actor_role",
                table: "security_audit_logs",
                sql: "actor_role IS NULL OR actor_role IN ('client', 'executor', 'dispatcher', 'qa', 'support', 'finance', 'admin', 'superadmin')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_security_audit_logs_actor_role",
                table: "security_audit_logs");

            migrationBuilder.DropCheckConstraint(name: "ck_users_role", table: "users");
            migrationBuilder.DropCheckConstraint(name: "ck_users_status", table: "users");
            migrationBuilder.DropCheckConstraint(name: "ck_legal_documents_type", table: "legal_documents");
            migrationBuilder.DropCheckConstraint(name: "ck_legal_documents_status", table: "legal_documents");
            migrationBuilder.DropCheckConstraint(name: "ck_otp_codes_channel", table: "otp_codes");
            migrationBuilder.DropCheckConstraint(name: "ck_otp_codes_purpose", table: "otp_codes");
            migrationBuilder.DropCheckConstraint(name: "ck_consent_logs_type", table: "consent_logs");
            migrationBuilder.DropCheckConstraint(name: "ck_auth_identities_provider", table: "auth_identities");
            migrationBuilder.DropCheckConstraint(name: "ck_security_audit_logs_event_type", table: "security_audit_logs");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "active",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "active");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "client",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "client");

            migrationBuilder.AlterColumn<string>(
                name: "event_type",
                table: "security_audit_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "actor_role",
                table: "security_audit_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "purpose",
                table: "otp_codes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "channel",
                table: "otp_codes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "legal_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "legal_documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "draft");

            migrationBuilder.AlterColumn<string>(
                name: "consent_type",
                table: "consent_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "provider",
                table: "auth_identities",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role",
                table: "users",
                sql: "role IN ('client', 'executor', 'dispatcher', 'qa', 'support', 'finance', 'admin', 'superadmin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_status",
                table: "users",
                sql: "status IN ('active', 'blocked', 'deleted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_legal_documents_type",
                table: "legal_documents",
                sql: "type IN ('oferta_client', 'oferta_executor', 'privacy', 'cookies', 'refund_policy')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_legal_documents_status",
                table: "legal_documents",
                sql: "status IN ('draft', 'published', 'archived')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_otp_codes_channel",
                table: "otp_codes",
                sql: "channel IN ('email', 'sms')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_otp_codes_purpose",
                table: "otp_codes",
                sql: "purpose IN ('login', 'verify_email')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consent_logs_type",
                table: "consent_logs",
                sql: "consent_type IN ('personal_data', 'marketing', 'cookies')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_auth_identities_provider",
                table: "auth_identities",
                sql: "provider IN ('email', 'phone')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_security_audit_logs_event_type",
                table: "security_audit_logs",
                sql: "event_type IN ('login_success', 'login_failed', 'logout', 'otp_requested', 'session_revoked', 'role_changed', 'mfa_enrolled', 'mfa_verified')");
        }
    }
}
