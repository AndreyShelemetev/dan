using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class LegalAcceptanceConfiguration : IEntityTypeConfiguration<LegalAcceptance>
{
    public void Configure(EntityTypeBuilder<LegalAcceptance> b)
    {
        b.ToTable("legal_acceptances");

        b.HasKey(x => x.Id);
        b.Property(x => x.IpAddress).HasColumnType("inet").IsRequired();
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.AcceptedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Document)
            .WithMany(d => d.Acceptances)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.UserId, x.DocumentId });
        b.HasIndex(x => x.DocumentId);
    }
}
