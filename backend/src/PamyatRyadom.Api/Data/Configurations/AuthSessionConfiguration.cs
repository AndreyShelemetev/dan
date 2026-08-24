using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> b)
    {
        b.ToTable("auth_sessions");

        b.HasKey(x => x.Id);
        b.Property(x => x.SessionTokenHash).HasMaxLength(255).IsRequired();
        b.Property(x => x.IpAddress).HasColumnType("inet");
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.IsPrivileged).HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.User)
            .WithMany(u => u.AuthSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.SessionTokenHash).IsUnique().HasDatabaseName("ux_auth_sessions_session_token_hash");
        b.HasIndex(x => new { x.UserId, x.RevokedAt, x.ExpiresAt });
    }
}
