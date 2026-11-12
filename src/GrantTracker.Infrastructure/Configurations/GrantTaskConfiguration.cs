using GrantTracker.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrantTracker.Infrastructure.Configurations;

/// <summary>DESIGN.md §3.2, §3.3 and §3.4.</summary>
public sealed class GrantTaskConfiguration : IEntityTypeConfiguration<GrantTask>
{
    /// <summary>Same rule as <see cref="GrantTaskQueries.IsPending"/>, for the filtered indexes.</summary>
    private const string PendingFilter = "\"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL";

    public void Configure(EntityTypeBuilder<GrantTask> builder)
    {
        // A task can never be both completed and cancelled (§3.2). The domain prevents it; this is the second line of defence.
        builder.ToTable("GrantTasks", table =>
        {
            table.HasCheckConstraint("CK_GrantTask_CompletedXorCancelled", "\"CompletedAtUtc\" IS NULL OR \"CancelledAtUtc\" IS NULL");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        // SQLite doesn't enforce the maximum length; the domain does (§3.6).
        builder.Property(t => t.Title).IsRequired().HasMaxLength(GrantTask.TitleMaxLength);
        builder.Property(t => t.DueDate).IsRequired();

        // Stored as an integer, so ORDER BY sorts High > Medium > Low.
        builder.Property(t => t.Priority).IsRequired().HasConversion<int>();

        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.CompletedAtUtc);
        builder.Property(t => t.CancelledAtUtc);

        // Derived from the two timestamps (§3.3). Queries use GrantTaskQueries instead.
        builder.Ignore(t => t.Status);

        builder.Property(t => t.Version).IsConcurrencyToken();

        builder.HasIndex(t => t.GrantId);
        builder.HasIndex(t => new { t.GrantId, t.Priority })
            .HasDatabaseName("IX_GrantTasks_GrantId_Priority_Pending")
            .HasFilter(PendingFilter);
        builder.HasIndex(t => t.DueDate)
            .HasDatabaseName("IX_GrantTasks_DueDate_Pending")
            .HasFilter(PendingFilter);
    }
}
