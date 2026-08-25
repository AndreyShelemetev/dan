using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> b)
    {
        b.ToTable("otp_codes", t =>
        {
            t.HasCheckConstraint("ck_otp_codes_channel", DbConstraintHelpers.InListCheck("channel", OtpChannels.All));
            t.HasCheckConstraint("ck_otp_codes_purpose", DbConstraintHelpers.InListCheck("purpose", OtpPurposes.All));
        });

        b.HasKey(x => x.Id);
        // Channel/Purpose are status-like: `text` + a named CHECK (CONVENTIONS.md §3).
        b.Property(x => x.Channel).HasColumnType("text").IsRequired();
        b.Property(x => x.Destination).HasMaxLength(320).IsRequired();
        b.Property(x => x.CodeHash).HasMaxLength(255).IsRequired();
        b.Property(x => x.Purpose).HasColumnType("text").IsRequired();
        b.Property(x => x.AttemptCount).HasDefaultValue(0);
        b.Property(x => x.IpAddress).HasColumnType("inet");
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.Destination, x.Purpose, x.ConsumedAt });
        b.HasIndex(x => x.ExpiresAt);
    }
}
