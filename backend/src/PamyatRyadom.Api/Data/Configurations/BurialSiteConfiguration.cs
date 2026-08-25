using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.BurialSites;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class BurialSiteConfiguration : IEntityTypeConfiguration<BurialSite>
{
    public void Configure(EntityTypeBuilder<BurialSite> b)
    {
        b.ToTable("burial_sites", t =>
        {
            t.HasCheckConstraint("ck_burial_sites_status", DbConstraintHelpers.InListCheck("status", BurialSiteStatuses.All));
            t.HasCheckConstraint("ck_burial_sites_location_quality", DbConstraintHelpers.InListCheck("location_quality", LocationQualities.All));
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.DeceasedFullName).HasMaxLength(255).IsRequired();

        // Life dates are free text — see the note on the entity. Widths are generous because
        // people write things like "около 1943" or "март 1998 (по документам 1997)".
        b.Property(x => x.BirthDateText).HasMaxLength(64);
        b.Property(x => x.DeathDateText).HasMaxLength(64);

        b.Property(x => x.PlotSection).HasMaxLength(128);
        b.Property(x => x.Landmarks).HasColumnType("text");
        b.Property(x => x.Notes).HasColumnType("text");

        b.Property(x => x.GeoLat).HasColumnType("numeric(9,7)");
        b.Property(x => x.GeoLng).HasColumnType("numeric(10,7)");

        b.Property(x => x.LocationQuality).HasColumnType("text").IsRequired().HasDefaultValue(LocationQualities.Unverified);
        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(BurialSiteStatuses.Active);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Cemetery)
            .WithMany(x => x.BurialSites)
            .HasForeignKey(x => x.CemeteryId)
            // A cemetery that still has records must not be deletable out from under them.
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            // Accounts are soft-deleted (status='deleted'), so a real cascade should never
            // fire; Restrict makes an accidental hard delete fail loudly instead of quietly
            // destroying a family's records.
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.OwnerUserId);
        b.HasIndex(x => x.CemeteryId);
        b.HasIndex(x => new { x.OwnerUserId, x.Status });
    }
}
