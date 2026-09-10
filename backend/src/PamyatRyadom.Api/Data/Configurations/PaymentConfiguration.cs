using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Payments;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments", t =>
        {
            t.HasCheckConstraint("ck_payments_status", DbConstraintHelpers.InListCheck("status", PaymentStatuses.All));
            t.HasCheckConstraint("ck_payments_amount", "amount_rub > 0");

            // Refunding more than was taken is not an accounting error to be discovered later.
            t.HasCheckConstraint("ck_payments_refunded", "refunded_rub >= 0 AND refunded_rub <= amount_rub");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.OrderRef).HasMaxLength(64).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        b.Property(x => x.ProviderPaymentId).HasMaxLength(128);
        b.Property(x => x.IdempotenceKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(PaymentStatuses.Pending);
        b.Property(x => x.AmountRub).HasColumnType("numeric(12,2)").IsRequired();
        b.Property(x => x.RefundedRub).HasColumnType("numeric(12,2)").IsRequired().HasDefaultValue(0m);
        b.Property(x => x.ConfirmationUrl).HasColumnType("text");
        b.Property(x => x.FailureReason).HasColumnType("text");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        // The reference is what makes a retry idempotent; without the unique index it is only a
        // convention, and a convention cannot stop a double charge.
        b.HasIndex(x => x.OrderRef).IsUnique().HasDatabaseName("ux_payments_order_ref");

        // Filtered: the column is null until the provider accepts, and several nulls are not a
        // collision.
        b.HasIndex(x => x.ProviderPaymentId)
            .IsUnique()
            .HasFilter("provider_payment_id IS NOT NULL")
            .HasDatabaseName("ux_payments_provider_payment_id");

        b.HasIndex(x => new { x.OrderId, x.Status });

        b.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
