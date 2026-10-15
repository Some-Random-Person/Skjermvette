using Microsoft.EntityFrameworkCore;

namespace ScreenTime.Agent.Service;

public class OutboxDbContext(DbContextOptions<OutboxDbContext> options) : DbContext(options)
{
    public DbSet<OutboxEvent> Events => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxEvent>(e =>
        {
            e.Property(x => x.RawIdentifier).HasMaxLength(255);
            e.HasIndex(x => x.StartUtc);
        });
    }
}
