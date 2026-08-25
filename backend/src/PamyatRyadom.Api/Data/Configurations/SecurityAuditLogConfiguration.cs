using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Data.Configurations;

internal sealed class SecurityAuditLogConfiguration : IEntityTypeConfiguration<SecurityAuditLog>
{
    public void Configure(EntityTypeBuilder<SecurityAuditLog> b)
    {
        b.ToTable("security_audit_logs", t =>
        {
            t.HasCheckConstraint("ck_security_audit_logs_event_type",
                DbConstraintHelpers.InListCheck("event_type", SecurityAuditEventTypes.All));
            // actor_role snapshots a users.role value, so the same value set has to hold here;
            // it is nullable (events with no acting user), hence the "or null" variant.
            t.HasCheckConstraint("ck_security_audit_logs_actor_role",
                DbConstraintHelpers.InListOrNullCheck("actor_role", UserRoles.All));
        });

        b.HasKey(x => x.Id);
        // EventType/ActorRole are status-like: `text` + a named CHECK (CONVENTIONS.md §3).
        b.Property(x => x.EventType).HasColumnType("text").IsRequired();
        b.Property(x => x.ActorRole).HasColumnType("text");
        b.Property(x => x.IpAddress).HasColumnType("inet");
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.Metadata).HasColumnType("jsonb");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // (user_id, created_at desc): the hot query is "this user's recent events first".
        b.HasIndex(x => new { x.UserId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_security_audit_logs_user_id_created_at_desc");

        b.HasIndex(x => new { x.EventType, x.CreatedAt });
    }
}
