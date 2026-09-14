using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PamyatRyadom.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LegalConsentOfertaSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_legal_documents_type",
                table: "legal_documents");

            migrationBuilder.AddCheckConstraint(
                name: "ck_legal_documents_type",
                table: "legal_documents",
                sql: "type IN ('oferta_client', 'oferta_executor', 'privacy', 'cookies', 'refund_policy', 'consent')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_legal_documents_type",
                table: "legal_documents");

            migrationBuilder.AddCheckConstraint(
                name: "ck_legal_documents_type",
                table: "legal_documents",
                sql: "type IN ('oferta_client', 'oferta_executor', 'privacy', 'cookies', 'refund_policy')");
        }
    }
}
