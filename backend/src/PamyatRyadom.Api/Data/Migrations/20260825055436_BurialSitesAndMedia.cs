using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PamyatRyadom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BurialSitesAndMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cemeteries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    region = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    geo_lat = table.Column<decimal>(type: "numeric(9,7)", nullable: true),
                    geo_lng = table.Column<decimal>(type: "numeric(10,7)", nullable: true),
                    hours = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    rules = table.Column<string>(type: "text", nullable: true),
                    contacts = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cemeteries", x => x.id);
                    table.CheckConstraint("ck_cemeteries_status", "status IN ('active', 'archived')");
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    owner_type = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false, defaultValue: "reference"),
                    storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    thumbnail_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "uploading"),
                    uploaded_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    moderation_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                    table.CheckConstraint("ck_media_assets_file_size", "file_size_bytes IS NULL OR file_size_bytes >= 0");
                    table.CheckConstraint("ck_media_assets_owner_type", "owner_type IN ('burial_site', 'order', 'visit', 'dispute')");
                    table.CheckConstraint("ck_media_assets_phase", "phase IN ('reference', 'before', 'process', 'after', 'document')");
                    table.CheckConstraint("ck_media_assets_ready_at", "(status = 'ready') = (ready_at IS NOT NULL)");
                    table.CheckConstraint("ck_media_assets_status", "status IN ('uploading', 'scanning', 'ready', 'quarantined', 'deleted')");
                    table.ForeignKey(
                        name: "fk_media_assets_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "burial_sites",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cemetery_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_user_id = table.Column<long>(type: "bigint", nullable: false),
                    deceased_full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    birth_date_text = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    death_date_text = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    plot_section = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    landmarks = table.Column<string>(type: "text", nullable: true),
                    geo_lat = table.Column<decimal>(type: "numeric(9,7)", nullable: true),
                    geo_lng = table.Column<decimal>(type: "numeric(10,7)", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    location_quality = table.Column<string>(type: "text", nullable: false, defaultValue: "unverified"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_burial_sites", x => x.id);
                    table.CheckConstraint("ck_burial_sites_location_quality", "location_quality IN ('unverified', 'sufficient', 'insufficient')");
                    table.CheckConstraint("ck_burial_sites_status", "status IN ('active', 'deleted')");
                    table.ForeignKey(
                        name: "fk_burial_sites_cemeteries_cemetery_id",
                        column: x => x.cemetery_id,
                        principalTable: "cemeteries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_burial_sites_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "burial_site_members",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    burial_site_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    invited_contact = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    permission = table.Column<string>(type: "text", nullable: false, defaultValue: "view"),
                    invited_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    invitation_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    invitation_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_burial_site_members", x => x.id);
                    table.CheckConstraint("ck_burial_site_members_permission", "permission IN ('view', 'order', 'manage')");
                    table.CheckConstraint("ck_burial_site_members_subject", "user_id IS NOT NULL OR invited_contact IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_burial_site_members_burial_sites_burial_site_id",
                        column: x => x.burial_site_id,
                        principalTable: "burial_sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_burial_site_members_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_burial_site_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_burial_site_members_invitation_token_hash",
                table: "burial_site_members",
                column: "invitation_token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_burial_site_members_invited_by_user_id",
                table: "burial_site_members",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_burial_site_members_user_id",
                table: "burial_site_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_burial_site_members_site_user_active",
                table: "burial_site_members",
                columns: new[] { "burial_site_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL AND revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_burial_sites_cemetery_id",
                table: "burial_sites",
                column: "cemetery_id");

            migrationBuilder.CreateIndex(
                name: "ix_burial_sites_owner_user_id",
                table: "burial_sites",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_burial_sites_owner_user_id_status",
                table: "burial_sites",
                columns: new[] { "owner_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_cemeteries_region",
                table: "cemeteries",
                column: "region");

            migrationBuilder.CreateIndex(
                name: "ix_cemeteries_status",
                table: "cemeteries",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_owner_type_owner_id_phase",
                table: "media_assets",
                columns: new[] { "owner_type", "owner_id", "phase" });

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_status",
                table: "media_assets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_uploaded_by_user_id",
                table: "media_assets",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_media_assets_storage_key",
                table: "media_assets",
                column: "storage_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "burial_site_members");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "burial_sites");

            migrationBuilder.DropTable(
                name: "cemeteries");
        }
    }
}
