using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.BurialSites;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class CemeteryConfiguration : IEntityTypeConfiguration<Cemetery>
{
    public void Configure(EntityTypeBuilder<Cemetery> b)
    {
        b.ToTable("cemeteries", t =>
        {
            t.HasCheckConstraint("ck_cemeteries_status", DbConstraintHelpers.InListCheck("status", CemeteryStatuses.All));
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(255).IsRequired();
        b.Property(x => x.Region).HasMaxLength(128);
        b.Property(x => x.Address).HasMaxLength(512);

        // Coordinates are decimal, not float: the repo bans binary floating point for values
        // that are compared and stored, and 7 decimal places is ~1cm of precision.
        b.Property(x => x.GeoLat).HasColumnType("numeric(9,7)");
        b.Property(x => x.GeoLng).HasColumnType("numeric(10,7)");

        b.Property(x => x.Hours).HasMaxLength(512);
        b.Property(x => x.Rules).HasColumnType("text");
        b.Property(x => x.Contacts).HasColumnType("jsonb");

        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(CemeteryStatuses.Active);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.Region);
    }
}
