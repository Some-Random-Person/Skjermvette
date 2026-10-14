using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ParentUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Family> Families => Set<Family>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<AppRule> AppRules => Set<AppRule>();
    public DbSet<AppDefinition> AppDefinitions => Set<AppDefinition>();
    public DbSet<AppIdentifier> AppIdentifiers => Set<AppIdentifier>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<PairingCode> PairingCodes => Set<PairingCode>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<DeviceLease> DeviceLeases => Set<DeviceLease>();
    public DbSet<ExtraTimeGrant> ExtraTimeGrants => Set<ExtraTimeGrant>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ParentUser>(e =>
        {
            e.Property(p => p.DisplayName).HasMaxLength(100);
            e.HasOne(p => p.Family).WithMany(f => f.Parents).HasForeignKey(p => p.FamilyId);
        });

        builder.Entity<Child>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100);
            e.Property(c => c.TimeZoneId).HasMaxLength(64);
            e.HasOne(c => c.Family).WithMany(f => f.Children).HasForeignKey(c => c.FamilyId);
        });

        builder.Entity<AppDefinition>(e =>
        {
            e.Property(a => a.Name).HasMaxLength(100);
            e.HasOne(a => a.Family).WithMany(f => f.CustomApps).HasForeignKey(a => a.FamilyId);
        });

        builder.Entity<AppIdentifier>(e =>
        {
            e.Property(i => i.Value).HasMaxLength(255);
            e.HasOne(i => i.AppDefinition).WithMany(a => a.Identifiers)
                .HasForeignKey(i => i.AppDefinitionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Family>().WithMany().HasForeignKey(i => i.FamilyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(i => new { i.FamilyId, i.Kind, i.Value });
        });

        builder.Entity<AppRule>(e =>
        {
            e.HasOne(r => r.Child).WithMany(c => c.AppRules).HasForeignKey(r => r.ChildId);
            e.HasOne(r => r.AppDefinition).WithMany().HasForeignKey(r => r.AppDefinitionId);
            e.HasIndex(r => new { r.ChildId, r.AppDefinitionId }).IsUnique();
        });

        builder.Entity<Device>(e =>
        {
            e.Property(d => d.Name).HasMaxLength(100);
            e.Property(d => d.TokenHash).HasMaxLength(64);
            e.HasIndex(d => d.TokenHash).IsUnique();
            e.HasOne(d => d.Child).WithMany(c => c.Devices).HasForeignKey(d => d.ChildId);
            e.HasOne(d => d.Lease).WithOne(l => l.Device)
                .HasForeignKey<DeviceLease>(l => l.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DeviceLease>()
            .HasMany(l => l.Apps).WithOne(a => a.Lease).HasForeignKey(a => a.LeaseId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PairingCode>(e =>
        {
            e.Property(p => p.Code).HasMaxLength(6);
            e.HasIndex(p => p.Code).IsUnique();
            e.HasOne(p => p.Child).WithMany().HasForeignKey(p => p.ChildId);
        });

        builder.Entity<UsageRecord>(e =>
        {
            e.Property(u => u.Id).ValueGeneratedNever();
            e.Property(u => u.RawIdentifier).HasMaxLength(255);
            e.HasOne(u => u.Device).WithMany().HasForeignKey(u => u.DeviceId);
            e.HasOne<Child>().WithMany().HasForeignKey(u => u.ChildId);
            e.HasOne<AppDefinition>().WithMany().HasForeignKey(u => u.AppDefinitionId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(u => new { u.ChildId, u.StartUtc });
        });

        builder.Entity<ExtraTimeGrant>(e =>
        {
            e.HasOne(g => g.Child).WithMany().HasForeignKey(g => g.ChildId);
            e.HasIndex(g => new { g.ChildId, g.DayStartUtc });
        });

        SeedCatalog.Apply(builder);
    }
}
