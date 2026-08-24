using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class MfaSecretConfiguration : IEntityTypeConfiguration<MfaSecret>
{
    public void Configure(EntityTypeBuilder<MfaSecret> b)
    {
        b.ToTable("mfa_secrets");

        b.HasKey(x => x.Id);
        b.Property(x => x.SecretEncrypted).HasMaxLength(1024).IsRequired();
        b.Property(x => x.RecoveryCodesHash).HasColumnType("jsonb");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.User)
            .WithOne(u => u.MfaSecret)
            .HasForeignKey<MfaSecret>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ux_mfa_secrets_user_id");
    }
}
