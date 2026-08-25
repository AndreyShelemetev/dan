using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.Media;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> b)
    {
        b.ToTable("media_assets", t =>
        {
            t.HasCheckConstraint("ck_media_assets_owner_type", DbConstraintHelpers.InListCheck("owner_type", MediaOwnerTypes.All));
            t.HasCheckConstraint("ck_media_assets_phase", DbConstraintHelpers.InListCheck("phase", MediaPhases.All));
            t.HasCheckConstraint("ck_media_assets_status", DbConstraintHelpers.InListCheck("status", MediaStatuses.All));

            // A file may only be served once it is READY, and READY is meaningless without the
            // moment it was reached — keeping the two in step in the database means no code
            // path can invent a servable asset that never passed the scan pipeline.
            t.HasCheckConstraint(
                "ck_media_assets_ready_at",
                "(status = 'ready') = (ready_at IS NOT NULL)");

            t.HasCheckConstraint("ck_media_assets_file_size", "file_size_bytes IS NULL OR file_size_bytes >= 0");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.OwnerType).HasColumnType("text").IsRequired();
        b.Property(x => x.Phase).HasColumnType("text").IsRequired().HasDefaultValue(MediaPhases.Reference);
        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(MediaStatuses.Uploading);

        b.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        b.Property(x => x.ThumbnailKey).HasMaxLength(512);
        b.Property(x => x.ContentType).HasMaxLength(128);
        b.Property(x => x.ChecksumSha256).HasMaxLength(64);
        b.Property(x => x.ModerationNote).HasColumnType("text");

        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The storage key is the address of the object; a duplicate would mean two rows
        // disagreeing about the same bytes.
        b.HasIndex(x => x.StorageKey).IsUnique().HasDatabaseName("ux_media_assets_storage_key");

        // The hot path: "give me the ready photos for this object, in this phase".
        b.HasIndex(x => new { x.OwnerType, x.OwnerId, x.Phase });
        b.HasIndex(x => x.Status);
    }
}
