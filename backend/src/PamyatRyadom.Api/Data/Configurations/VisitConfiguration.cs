using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.Dispatch;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> b)
    {
        b.ToTable("visits", t =>
        {
            t.HasCheckConstraint("ck_visits_status", DbConstraintHelpers.InListCheck("status", VisitStatuses.All));
            t.HasCheckConstraint("ck_visits_payout", "payout_rub IS NULL OR payout_rub >= 0");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasColumnType("text").IsRequired().HasDefaultValue(VisitStatuses.Offered);
        b.Property(x => x.PayoutRub).HasColumnType("numeric(12,2)");
        b.Property(x => x.ExecutorNote).HasColumnType("text");
        b.Property(x => x.ReviewNote).HasColumnType("text");
        b.Property(x => x.DeclineReason).HasColumnType("text");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.OrderId, x.Status });
        b.HasIndex(x => new { x.ExecutorUserId, x.Status });

        b.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ExecutorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.ChecklistItems)
            .WithOne(x => x.Visit)
            .HasForeignKey(x => x.VisitId)
            // The checklist is part of the visit, not a separate record: a visit without its
            // answers is not evidence of anything.
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class VisitChecklistItemConfiguration : IEntityTypeConfiguration<VisitChecklistItem>
{
    public void Configure(EntityTypeBuilder<VisitChecklistItem> b)
    {
        b.ToTable("visit_checklist_items", t =>
        {
            t.HasCheckConstraint(
                "ck_visit_checklist_items_result",
                DbConstraintHelpers.InListCheck("result", ChecklistResults.All));

            // Anything but "done" has to say why. Enforced here as well as in the service,
            // because an unexplained skip is exactly what a client discovers months later.
            t.HasCheckConstraint(
                "ck_visit_checklist_items_note",
                "result NOT IN ('impossible', 'not_required') OR (note IS NOT NULL AND length(btrim(note)) > 0)");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.Property(x => x.Title).HasMaxLength(255).IsRequired();
        b.Property(x => x.Result).HasColumnType("text").IsRequired().HasDefaultValue(ChecklistResults.Pending);
        b.Property(x => x.Note).HasColumnType("text");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.VisitId, x.Key }).IsUnique().HasDatabaseName("ux_visit_checklist_items_key");
    }
}
