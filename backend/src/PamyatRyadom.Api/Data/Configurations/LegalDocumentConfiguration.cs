using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> b)
    {
        b.ToTable("legal_documents", t =>
        {
            t.HasCheckConstraint("ck_legal_documents_type", DbConstraintHelpers.InListCheck("type", LegalDocumentTypes.All));
            t.HasCheckConstraint("ck_legal_documents_status", DbConstraintHelpers.InListCheck("status", LegalDocumentStatuses.All));
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(32).IsRequired();
        b.Property(x => x.Version).HasMaxLength(64).IsRequired();
        b.Property(x => x.Locale).HasMaxLength(16).IsRequired();
        b.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired().HasDefaultValue(LegalDocumentStatuses.Draft);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.Type, x.Version, x.Locale })
            .IsUnique()
            .HasDatabaseName("ux_legal_documents_type_version_locale");
        b.HasIndex(x => new { x.Type, x.Status });
    }
}
