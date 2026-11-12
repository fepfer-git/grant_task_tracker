using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GrantTracker.Infrastructure;

/// <summary>
/// Used only by the <c>dotnet ef</c> tool to create migrations, so it doesn't need to start the API.
/// The running app gets its connection string from configuration.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=granttracker.db").Options);
}
