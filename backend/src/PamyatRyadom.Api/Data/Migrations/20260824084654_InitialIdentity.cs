using System;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PamyatRyadom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legal_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    effective_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "draft"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_documents", x => x.id);
                    table.CheckConstraint("ck_legal_documents_status", "status IN ('draft', 'published', 'archived')");
                    table.CheckConstraint("ck_legal_documents_type", "type IN ('oferta_client', 'oferta_executor', 'privacy', 'cookies', 'refund_policy')");
                });

            migrationBuilder.CreateTable(
                name: "otp_codes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    destination = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_otp_codes", x => x.id);
                    table.CheckConstraint("ck_otp_codes_channel", "channel IN ('email', 'sms')");
                    table.CheckConstraint("ck_otp_codes_purpose", "purpose IN ('login', 'verify_email')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "client"),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "active"),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "role IN ('client', 'executor', 'dispatcher', 'qa', 'support', 'finance', 'admin', 'superadmin')");
                    table.CheckConstraint("ck_users_status", "status IN ('active', 'blocked', 'deleted')");
                });

            migrationBuilder.CreateTable(
                name: "auth_identities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    email_normalized = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(email)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_identities", x => x.id);
                    table.CheckConstraint("ck_auth_identities_provider", "provider IN ('email', 'phone')");
                    table.CheckConstraint("ck_auth_identities_provider_contact", "(provider = 'email' AND email IS NOT NULL) OR (provider = 'phone' AND phone IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_auth_identities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "auth_sessions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    session_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rotated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_privileged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consent_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    consent_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    document_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: false),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_logs", x => x.id);
                    table.CheckConstraint("ck_consent_logs_type", "consent_type IN ('personal_data', 'marketing', 'cookies')");
                    table.ForeignKey(
                        name: "fk_consent_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "legal_acceptances",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: false),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_acceptances", x => x.id);
                    table.ForeignKey(
                        name: "fk_legal_acceptances_legal_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "legal_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_legal_acceptances_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "mfa_secrets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    secret_encrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    enabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recovery_codes_hash = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mfa_secrets", x => x.id);
                    table.ForeignKey(
                        name: "fk_mfa_secrets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actor_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_audit_logs", x => x.id);
                    table.CheckConstraint("ck_security_audit_logs_event_type", "event_type IN ('login_success', 'login_failed', 'logout', 'otp_requested', 'session_revoked', 'role_changed', 'mfa_enrolled', 'mfa_verified')");
                    table.ForeignKey(
                        name: "fk_security_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auth_identities_provider",
                table: "auth_identities",
                column: "provider");

            migrationBuilder.CreateIndex(
                name: "ix_auth_identities_user_id",
                table: "auth_identities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_auth_identities_email_lower_when_email_provider",
                table: "auth_identities",
                column: "email_normalized",
                unique: true,
                filter: "provider = 'email' AND email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_auth_identities_phone_when_phone_provider",
                table: "auth_identities",
                column: "phone",
                unique: true,
                filter: "provider = 'phone' AND phone IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_auth_identities_provider_provider_user_id",
                table: "auth_identities",
                columns: new[] { "provider", "provider_user_id" },
                unique: true,
                filter: "provider_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_auth_sessions_user_id_revoked_at_expires_at",
                table: "auth_sessions",
                columns: new[] { "user_id", "revoked_at", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_auth_sessions_session_token_hash",
                table: "auth_sessions",
                column: "session_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_consent_logs_email_guest",
                table: "consent_logs",
                column: "email",
                filter: "user_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_consent_logs_user_id_consent_type_accepted_at",
                table: "consent_logs",
                columns: new[] { "user_id", "consent_type", "accepted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_acceptances_document_id",
                table: "legal_acceptances",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_legal_acceptances_user_id_document_id",
                table: "legal_acceptances",
                columns: new[] { "user_id", "document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_documents_type_status",
                table: "legal_documents",
                columns: new[] { "type", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_legal_documents_type_version_locale",
                table: "legal_documents",
                columns: new[] { "type", "version", "locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_mfa_secrets_user_id",
                table: "mfa_secrets",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_otp_codes_destination_purpose_consumed_at",
                table: "otp_codes",
                columns: new[] { "destination", "purpose", "consumed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_otp_codes_expires_at",
                table: "otp_codes",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_logs_event_type_created_at",
                table: "security_audit_logs",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_logs_user_id_created_at_desc",
                table: "security_audit_logs",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_users_role",
                table: "users",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "ix_users_status",
                table: "users",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_identities");

            migrationBuilder.DropTable(
                name: "auth_sessions");

            migrationBuilder.DropTable(
                name: "consent_logs");

            migrationBuilder.DropTable(
                name: "legal_acceptances");

            migrationBuilder.DropTable(
                name: "mfa_secrets");

            migrationBuilder.DropTable(
                name: "otp_codes");

            migrationBuilder.DropTable(
                name: "security_audit_logs");

            migrationBuilder.DropTable(
                name: "legal_documents");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
