using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Models.Catalog;
using PamyatRyadom.Api.Models.Media;
using PamyatRyadom.Api.Models.Orders;

namespace PamyatRyadom.Api.Data;

/// <summary>
/// EF Core DbContext for PamyatRyadom, wired for Npgsql + snake_case naming
/// (see Program.cs: UseNpgsql(...).UseSnakeCaseNamingConvention()).
///
/// DbSets are added per-module — see Phase 2. Each module registers its own
/// DbSet(s) and IEntityTypeConfiguration classes picked up below via
/// ApplyConfigurationsFromAssembly.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Identity / Auth / Legal-consent core.
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthIdentity> AuthIdentities => Set<AuthIdentity>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<MfaSecret> MfaSecrets => Set<MfaSecret>();
    public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();
    public DbSet<ConsentLog> ConsentLogs => Set<ConsentLog>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<LegalAcceptance> LegalAcceptances => Set<LegalAcceptance>();

    // Burial sites: the digital record of a place of remembrance, plus family access.
    public DbSet<Cemetery> Cemeteries => Set<Cemetery>();
    public DbSet<BurialSite> BurialSites => Set<BurialSite>();
    public DbSet<BurialSiteMember> BurialSiteMembers => Set<BurialSiteMember>();

    // Media: metadata only — the bytes live in a private S3-compatible bucket.
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    // Catalog: what is sold, versioned so an order can bind to the exact terms it was sold under.
    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    // Orders: the spine. Status changes go through OrderStateMachine, never straight to the column.
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<Estimate> Estimates => Set<Estimate>();
    public DbSet<EstimateLine> EstimateLines => Set<EstimateLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Stamps created_at/updated_at (mapped from CreatedAt/UpdatedAt) on every mutable
    // entity that has them — see the repo convention: every mutable table carries
    // created_at/updated_at timestamptz columns.
    private void StampTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
            {
                continue;
            }

            var createdAt = entry.Metadata.FindProperty("CreatedAt");
            if (createdAt is not null && entry.State == EntityState.Added)
            {
                var current = entry.Property(createdAt.Name).CurrentValue as DateTimeOffset?;
                if (current is null || current == default(DateTimeOffset))
                {
                    entry.Property(createdAt.Name).CurrentValue = now;
                }
            }

            var updatedAt = entry.Metadata.FindProperty("UpdatedAt");
            if (updatedAt is not null)
            {
                entry.Property(updatedAt.Name).CurrentValue = now;
            }
        }
    }
}
