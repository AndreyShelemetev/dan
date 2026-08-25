using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.BurialSites;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class BurialSiteMemberConfiguration : IEntityTypeConfiguration<BurialSiteMember>
{
    public void Configure(EntityTypeBuilder<BurialSiteMember> b)
    {
        b.ToTable("burial_site_members", t =>
        {
            t.HasCheckConstraint(
                "ck_burial_site_members_permission",
                DbConstraintHelpers.InListCheck("permission", BurialSitePermissions.All));

            // An invitation is addressed either to an existing account or to a contact; a row
            // with neither identifies nobody and could never be accepted.
            t.HasCheckConstraint(
                "ck_burial_site_members_subject",
                "user_id IS NOT NULL OR invited_contact IS NOT NULL");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Permission).HasColumnType("text").IsRequired().HasDefaultValue(BurialSitePermissions.View);
        b.Property(x => x.InvitedContact).HasMaxLength(320);
        b.Property(x => x.InvitationTokenHash).HasMaxLength(128);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.Ignore(x => x.IsActive);

        b.HasOne(x => x.BurialSite)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.BurialSiteId)
            // Membership is meaningless without its site, so it may follow it.
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One live membership per (site, user). Revoked rows are kept for the audit trail,
        // so the uniqueness only covers rows that still grant something.
        b.HasIndex(x => new { x.BurialSiteId, x.UserId })
            .IsUnique()
            .HasDatabaseName("ux_burial_site_members_site_user_active")
            .HasFilter("user_id IS NOT NULL AND revoked_at IS NULL");

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.InvitationTokenHash);
    }
}
