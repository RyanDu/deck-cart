using Deck.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Deck.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<CartHistory> CartHistory => Set<CartHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // time
        var seedAtUtc = new DateTime(2025, 10, 03, 0, 0, 0, DateTimeKind.Utc);

        // Seed initial data
        modelBuilder.Entity<Item>().HasData(
            new Item { Id = 1, Name = "Item 1", Price = 1.11m, CreatedDateTime = seedAtUtc, ModifiedDateTime = seedAtUtc },
            new Item { Id = 2, Name = "Item 2", Price = 2.22m, CreatedDateTime = seedAtUtc, ModifiedDateTime = seedAtUtc }
        );

        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Name = "User 1", CartVersion = 0, CreatedDateTime = seedAtUtc, ModifiedDateTime = seedAtUtc },
            new User { Id = 2, Name = "User 2", CartVersion = 0, CreatedDateTime = seedAtUtc, ModifiedDateTime = seedAtUtc }
        );

        modelBuilder.Entity<CartHistory>()
            .HasIndex(h => new { h.UserId, h.SnapshotAt });
    }


    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is User || e.Entity is Item || e.Entity is CartItem || e.Entity is CartHistory);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property("CreatedDateTime").CurrentValue = DateTime.UtcNow;
                entry.Property("ModifiedDateTime").CurrentValue = DateTime.UtcNow;
            }
            if (entry.State == EntityState.Modified)
            {
                entry.Property("ModifiedDateTime").CurrentValue = DateTime.UtcNow;
            }
        }
    }

}