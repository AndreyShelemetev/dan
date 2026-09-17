using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.Disputes;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> b)
    {
        b.ToTable("disputes", t =>
        {
            t.HasCheckConstraint("ck_disputes_status", DbConstraintHelpers.InListCheck("status", DisputeStatuses.All));
            t.HasCheckConstraint(
                "ck_disputes_resolution_type",
                DbConstraintHelpers.InListOrNullCheck("resolution_type", DisputeResolutionTypes.All));
            t.HasCheckConstraint("ck_disputes_reason", "length(btrim(reason)) > 0");

            // A decided dispute (resolved/rejected) always carries its resolution fields
            // together; a live one (open/in_review) carries none of them. Half a decision is
            // not a decision.
            t.HasCheckConstraint(
                "ck_disputes_resolution_consistency",
                "(status IN ('resolved', 'rejected') " +
                "AND resolution_type IS NOT NULL " +
                "AND resolution_text IS NOT NULL AND length(btrim(resolution_text)) > 0 " +
                "AND resolved_by_user_id IS NOT NULL AND resolved_at IS NOT NULL) " +
                "OR " +
                "(status NOT IN ('resolved', 'rejected') " +
                "AND resolution_type IS NULL AND resolution_text IS NULL " +
                "AND resolved_by_user_id IS NULL AND resolved_at IS NULL)");

            t.HasCheckConstraint("ck_disputes_refund_amount", "refund_amount_rub IS NULL OR refund_amount_rub >= 0");

            // The amount only ever belongs to a partial refund: a full refund's amount lives on
            // the payment record, and no other resolution type moves money at all.
            t.HasCheckConstraint(
                "ck_disputes_refund_amount_scope",
                "refund_amount_rub IS NULL OR resolution_type = 'partial_refund'");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Reason).HasColumnType("text").IsRequired();
        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(DisputeStatuses.Open);
        b.Property(x => x.ResolutionType).HasColumnType("text");
        b.Property(x => x.ResolutionText).HasColumnType("text");
        b.Property(x => x.RefundAmountRub).HasColumnType("numeric(12,2)");
        b.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        // Exactly one live dispute per order (BR from D13): a second open/in_review row on the
        // same order would mean two unresolved complaints about the same work.
        b.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasDatabaseName("ux_disputes_order_id_live")
            .HasFilter("status IN ('open', 'in_review')");

        b.HasIndex(x => new { x.OrderId, x.Status }).HasDatabaseName("ix_disputes_order_id_status");

        b.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OpenedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
