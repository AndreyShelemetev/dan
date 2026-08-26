using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Catalog;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class ServicePackageConfiguration : IEntityTypeConfiguration<ServicePackage>
{
    public void Configure(EntityTypeBuilder<ServicePackage> b)
    {
        b.ToTable("service_packages", t =>
        {
            t.HasCheckConstraint(
                "ck_service_packages_status",
                DbConstraintHelpers.InListCheck("status", ServicePackageStatuses.All));

            // Money is never negative, and a package that costs nothing is a configuration
            // mistake rather than a free service.
            t.HasCheckConstraint("ck_service_packages_price", "price_from_rub > 0");
            t.HasCheckConstraint("ck_service_packages_warranty", "warranty_days >= 0");

            // A published version must record when it was published; the pair is what an order
            // snapshot and a warranty window are later reasoned about.
            t.HasCheckConstraint(
                "ck_service_packages_published_at",
                "(status = 'published') = (published_at IS NOT NULL)");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Version).HasMaxLength(16).IsRequired();
        b.Property(x => x.Locale).HasMaxLength(16).IsRequired().HasDefaultValue("ru");
        b.Property(x => x.Title).HasMaxLength(255).IsRequired();
        b.Property(x => x.Summary).HasColumnType("text").IsRequired();
        b.Property(x => x.VisitsLabel).HasMaxLength(64);

        b.Property(x => x.Includes).HasColumnType("jsonb");
        b.Property(x => x.Limits).HasColumnType("jsonb");

        // numeric(12,2), never float — the repo convention, and the reason is that a price is
        // compared and summed, not approximated.
        b.Property(x => x.PriceFromRub).HasColumnType("numeric(12,2)").IsRequired();

        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(ServicePackageStatuses.Draft);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        // One row per (code, version, locale): the identity an order binds to.
        b.HasIndex(x => new { x.Code, x.Version, x.Locale })
            .IsUnique()
            .HasDatabaseName("ux_service_packages_code_version_locale");

        b.HasIndex(x => x.Status);

        b.HasOne(x => x.ChecklistTemplate)
            .WithOne(x => x.ServicePackage)
            .HasForeignKey<ChecklistTemplate>(x => x.ServicePackageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ChecklistTemplateConfiguration : IEntityTypeConfiguration<ChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplate> b)
    {
        b.ToTable("checklist_templates");

        b.HasKey(x => x.Id);
        b.Property(x => x.Items).HasColumnType("jsonb");
        b.Property(x => x.RequiredMedia).HasColumnType("jsonb");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.ServicePackageId)
            .IsUnique()
            .HasDatabaseName("ux_checklist_templates_package");
    }
}

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.ToTable("subscription_plans", t =>
        {
            t.HasCheckConstraint(
                "ck_subscription_plans_status",
                DbConstraintHelpers.InListCheck("status", ServicePackageStatuses.All));

            t.HasCheckConstraint("ck_subscription_plans_price", "price_rub > 0");
            t.HasCheckConstraint("ck_subscription_plans_visits", "visits_total > 0");
            t.HasCheckConstraint("ck_subscription_plans_period", "period_months > 0");
            t.HasCheckConstraint(
                "ck_subscription_plans_published_at",
                "(status = 'published') = (published_at IS NOT NULL)");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Version).HasMaxLength(16).IsRequired();
        b.Property(x => x.Locale).HasMaxLength(16).IsRequired().HasDefaultValue("ru");
        b.Property(x => x.Title).HasMaxLength(255).IsRequired();
        b.Property(x => x.Summary).HasColumnType("text").IsRequired();
        b.Property(x => x.ServicePackageCode).HasMaxLength(64).IsRequired();
        b.Property(x => x.PriceRub).HasColumnType("numeric(12,2)").IsRequired();
        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(ServicePackageStatuses.Draft);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        // Computed from the stored fields; a persisted copy is one more value that can drift.
        b.Ignore(x => x.PricePerVisit);

        b.HasIndex(x => new { x.Code, x.Version, x.Locale })
            .IsUnique()
            .HasDatabaseName("ux_subscription_plans_code_version_locale");

        b.HasIndex(x => x.Status);
    }
}
