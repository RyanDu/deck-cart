using Deck.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Deck.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed initial data
        modelBuilder.Entity<Item>().HasData(
            new Item { Id = 1, Name = "Item 1", Price = 1.11m },
            new Item { Id = 2, Name = "Item 2", Price = 2.22m }
        );

        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Name = "User 1" },
            new User { Id = 2, Name = "User 2" }
        );

        modelBuilder.Entity<CartHistory>()
        .HasIndex(h => new { h.UserId, h.SnapshotAt });
    }
}