using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Models.Catalog;
using PamyatRyadom.Api.Models.Orders;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders", t =>
        {
            t.HasCheckConstraint("ck_orders_status", DbConstraintHelpers.InListCheck("status", OrderStatuses.All));

            // The package price shown at the point of sale; a zero would mean the snapshot never
            // happened rather than a free service.
            t.HasCheckConstraint("ck_orders_package_price", "package_price_from_rub > 0");
            t.HasCheckConstraint("ck_orders_warranty", "warranty_days >= 0");

            // A window is either open-ended or ordered; a "from" after its "to" is data that no
            // scheduling code can act on sensibly.
            t.HasCheckConstraint(
                "ck_orders_window",
                "preferred_from IS NULL OR preferred_to IS NULL OR preferred_from <= preferred_to");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Number).HasMaxLength(32).IsRequired();
        b.Property(x => x.PackageCode).HasMaxLength(64).IsRequired();
        b.Property(x => x.PackageVersion).HasMaxLength(16).IsRequired();
        b.Property(x => x.PackageTitle).HasMaxLength(255).IsRequired();
        b.Property(x => x.PackagePriceFromRub).HasColumnType("numeric(12,2)").IsRequired();
        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(OrderStatuses.Draft);
        b.Property(x => x.CustomerComment).HasColumnType("text");
        b.Property(x => x.Source).HasMaxLength(64);
        b.Property(x => x.CancellationReason).HasColumnType("text");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.Ignore(x => x.WarrantyUntil);

        b.HasIndex(x => x.Number).IsUnique().HasDatabaseName("ux_orders_number");
        b.HasIndex(x => new { x.CustomerUserId, x.Status });
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.BurialSiteId);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.BurialSite)
            .WithMany()
            .HasForeignKey(x => x.BurialSiteId)
            // A burial site with orders against it must not disappear from under them; the site
            // is soft-deleted by status anyway.
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<ServicePackage>()
            .WithMany()
            .HasForeignKey(x => x.ServicePackageId)
            // The catalogue row an order was sold under is evidence. Archiving takes a version
            // off sale; deleting one that an order points at must fail loudly.
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.ToTable("order_status_history", t =>
        {
            t.HasCheckConstraint(
                "ck_order_status_history_to",
                DbConstraintHelpers.InListCheck("to_status", OrderStatuses.All));
            t.HasCheckConstraint(
                "ck_order_status_history_from",
                DbConstraintHelpers.InListOrNullCheck("from_status", OrderStatuses.All));
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.FromStatus).HasColumnType("text");
        b.Property(x => x.ToStatus).HasColumnType("text").IsRequired();
        b.Property(x => x.ActorRole).HasColumnType("text");
        b.Property(x => x.Reason).HasColumnType("text");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Order)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.OrderId, x.CreatedAt });
    }
}

internal sealed class EstimateConfiguration : IEntityTypeConfiguration<Estimate>
{
    public void Configure(EntityTypeBuilder<Estimate> b)
    {
        b.ToTable("estimates", t =>
        {
            t.HasCheckConstraint(
                "ck_estimates_status",
                DbConstraintHelpers.InListCheck("status", EstimateStatuses.All));

            // BR-003: a quote never totals below zero, whatever the discount lines say.
            t.HasCheckConstraint("ck_estimates_total", "total_rub >= 0");

            t.HasCheckConstraint("ck_estimates_version", "version > 0");

            // Published is the point of no return; a published row without the moment it happened
            // cannot support "what was offered, and when".
            t.HasCheckConstraint(
                "ck_estimates_published_at",
                "status = 'draft' OR published_at IS NOT NULL");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(EstimateStatuses.Draft);
        b.Property(x => x.TotalRub).HasColumnType("numeric(12,2)").IsRequired();
        b.Property(x => x.Note).HasColumnType("text");
        b.Property(x => x.RejectionReason).HasColumnType("text");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Order)
            .WithMany(x => x.Estimates)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.OrderId, x.Version })
            .IsUnique()
            .HasDatabaseName("ux_estimates_order_version");

        // At most one estimate awaiting the client per order: two live quotes for the same work
        // is a question nobody can answer ("which one am I agreeing to?").
        b.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasDatabaseName("ux_estimates_order_published")
            .HasFilter("status = 'published'");
    }
}

internal sealed class EstimateLineConfiguration : IEntityTypeConfiguration<EstimateLine>
{
    public void Configure(EntityTypeBuilder<EstimateLine> b)
    {
        b.ToTable("estimate_lines", t =>
        {
            t.HasCheckConstraint(
                "ck_estimate_lines_type",
                DbConstraintHelpers.InListCheck("type", EstimateLineTypes.All));

            t.HasCheckConstraint("ck_estimate_lines_quantity", "quantity > 0");

            // Only a discount may be negative. Anything else with a negative price is a data
            // error that would quietly reduce what the client owes.
            t.HasCheckConstraint(
                "ck_estimate_lines_price_sign",
                "(type = 'discount' AND unit_price_rub <= 0) OR (type <> 'discount' AND unit_price_rub >= 0)");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasColumnType("text").IsRequired();
        b.Property(x => x.Title).HasMaxLength(255).IsRequired();
        b.Property(x => x.Quantity).HasColumnType("numeric(10,2)").IsRequired().HasDefaultValue(1m);
        b.Property(x => x.Unit).HasMaxLength(32);
        b.Property(x => x.UnitPriceRub).HasColumnType("numeric(12,2)").IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.Ignore(x => x.TotalRub);

        b.HasOne(x => x.Estimate)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.EstimateId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.EstimateId, x.SortOrder });
    }
}
