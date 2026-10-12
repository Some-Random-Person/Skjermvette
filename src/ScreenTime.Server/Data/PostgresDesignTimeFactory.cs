using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ScreenTime.Server.Data;

/// <summary>
/// Used only by <c>dotnet ef migrations add</c>: migrations are generated for Postgres (production).
/// No database connection is made while generating them.
/// </summary>
public class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=screentime").Options);
}
