using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_role", DbConstraintHelpers.InListCheck("role", UserRoles.All));
            t.HasCheckConstraint("ck_users_status", DbConstraintHelpers.InListCheck("status", UserStatuses.All));
        });

        b.HasKey(x => x.Id);
        // Status-like columns are `text` + a named CHECK (CONVENTIONS.md §3) — the CHECK is the
        // authority on the allowed values, so a varchar width would only add a second, weaker rule.
        b.Property(x => x.Role).HasColumnType("text").IsRequired().HasDefaultValue(UserRoles.Client);
        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(UserStatuses.Active);
        b.Property(x => x.DisplayName).HasMaxLength(255);
        b.Property(x => x.Locale).HasMaxLength(16);
        b.Property(x => x.Timezone).HasMaxLength(64);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.Role);
        b.HasIndex(x => x.Status);
    }
}
