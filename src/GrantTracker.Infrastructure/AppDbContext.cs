using GrantTracker.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GrantTracker.Infrastructure;

/// <summary>
/// The database (DESIGN.md §3). Every save renews the concurrency tokens (§3.5), so a write based on data that
/// another request has changed fails with <see cref="DbUpdateConcurrencyException"/>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Grant> Grants => Set<Grant>();

    public DbSet<GrantTask> GrantTasks => Set<GrantTask>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RenewVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RenewVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    /// <summary>
    /// DESIGN.md §3.5:
    /// 1. Every modified grant or task gets a new <c>Version</c>.
    /// 2. When a task is added, its grant gets a new <c>Version</c> too, so adding a task conflicts with a close
    ///    saved at the same time. A grant that is being added already has a new version, so it's left alone
    ///    (this keeps the seed's fixed versions).
    /// </summary>
    private void RenewVersions()
    {
        ChangeTracker.DetectChanges();

        var toRenew = new HashSet<EntityEntry>();
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is not (Grant or GrantTask))
            {
                continue;
            }

            if (entry.State == EntityState.Modified)
            {
                toRenew.Add(entry);
            }

            if (entry.Entity is GrantTask && entry.State == EntityState.Added)
            {
                var grantEntry = entry.Reference(nameof(GrantTask.Grant)).TargetEntry;
                if (grantEntry is { State: EntityState.Unchanged or EntityState.Modified })
                {
                    toRenew.Add(grantEntry);
                }
            }
        }

        foreach (var entry in toRenew)
        {
            entry.Property(nameof(Grant.Version)).CurrentValue = Guid.NewGuid();
        }
    }
}
