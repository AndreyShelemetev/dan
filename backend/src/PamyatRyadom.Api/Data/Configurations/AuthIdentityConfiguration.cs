using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class AuthIdentityConfiguration : IEntityTypeConfiguration<AuthIdentity>
{
    public void Configure(EntityTypeBuilder<AuthIdentity> b)
    {
        b.ToTable("auth_identities", t =>
        {
            t.HasCheckConstraint("ck_auth_identities_provider",
                DbConstraintHelpers.InListCheck("provider", AuthProviders.All));
            t.HasCheckConstraint("ck_auth_identities_provider_contact",
                "(provider = 'email' AND email IS NOT NULL) OR (provider = 'phone' AND phone IS NOT NULL)");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        b.Property(x => x.ProviderUserId).HasMaxLength(255);
        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.Phone).HasMaxLength(32);
        b.Property(x => x.IsVerified).HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.User)
            .WithMany(u => u.AuthIdentities)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.Provider);

        b.HasIndex(x => new { x.Provider, x.ProviderUserId })
            .HasDatabaseName("ux_auth_identities_provider_provider_user_id")
            .IsUnique()
            .HasFilter("provider_user_id IS NOT NULL");

        // Postgres can't index lower(email) via a plain column index, so the comparison key is
        // materialized as a stored generated column and that column is what's uniquely indexed —
        // this is what enforces "unique(lower(email)) where provider='email'" at the DB level,
        // independent of whether every code path remembers to normalize casing first.
        b.Property<string?>("EmailNormalized")
            .HasColumnName("email_normalized")
            .HasComputedColumnSql("lower(email)", stored: true);

        b.HasIndex("EmailNormalized")
            .HasDatabaseName("ux_auth_identities_email_lower_when_email_provider")
            .IsUnique()
            .HasFilter("provider = 'email' AND email IS NOT NULL");

        b.HasIndex(x => x.Phone)
            .HasDatabaseName("ux_auth_identities_phone_when_phone_provider")
            .IsUnique()
            .HasFilter("provider = 'phone' AND phone IS NOT NULL");
    }
}
