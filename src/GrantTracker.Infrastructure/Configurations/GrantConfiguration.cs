using GrantTracker.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrantTracker.Infrastructure.Configurations;

/// <summary>DESIGN.md §3.1 and §3.4.</summary>
public sealed class GrantConfiguration : IEntityTypeConfiguration<Grant>
{
    public void Configure(EntityTypeBuilder<Grant> builder)
    {
        builder.ToTable("Grants");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.Reference).IsRequired().HasMaxLength(20);
        builder.HasIndex(g => g.Reference).IsUnique();

        builder.Property(g => g.OrganisationName).IsRequired().HasMaxLength(200);
        builder.Property(g => g.ApplicationDate).IsRequired();

        builder.Property(g => g.Status).IsRequired().HasConversion<int>();
        builder.HasIndex(g => g.Status);

        builder.Property(g => g.ClosedNote).HasMaxLength(1000);
        builder.Property(g => g.ClosedAtUtc);

        builder.Property(g => g.Version).IsConcurrencyToken();

        builder.HasMany(g => g.Tasks)
            .WithOne(t => t.Grant)
            .HasForeignKey(t => t.GrantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(g => g.Tasks).HasField("_tasks").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
