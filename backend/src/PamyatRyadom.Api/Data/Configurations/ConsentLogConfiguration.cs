using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class ConsentLogConfiguration : IEntityTypeConfiguration<ConsentLog>
{
    public void Configure(EntityTypeBuilder<ConsentLog> b)
    {
        b.ToTable("consent_logs", t =>
        {
            t.HasCheckConstraint("ck_consent_logs_type", DbConstraintHelpers.InListCheck("consent_type", ConsentTypes.All));
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.ConsentType).HasMaxLength(32).IsRequired();
        b.Property(x => x.DocumentVersion).HasMaxLength(64).IsRequired();
        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.Phone).HasMaxLength(32);
        b.Property(x => x.IpAddress).HasColumnType("inet").IsRequired();
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.AcceptedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.UserId, x.ConsentType, x.AcceptedAt });
        b.HasIndex(x => x.Email).HasDatabaseName("ix_consent_logs_email_guest").HasFilter("user_id IS NULL");
    }
}
