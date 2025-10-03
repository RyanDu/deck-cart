using System.Text.Json;
using Deck.Api.Data;
using Deck.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Deck.Api.Services;

public class CartService(AppDbContext dbContext) : ICartService
{
    public async Task<GetCartResponse> GetAsync(int userId, CancellationToken ct)
    {
        var user = await dbContext.Users.Include(u => u.CartItems).ThenInclude(ci => ci.Item)
                            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) throw new KeyNotFoundException($"User {userId} not found");

        return new GetCartResponse
        {
            Name = user.Name,
            Cart = user.CartItems.OrderBy(ci => ci.Id).Select(ci => new CartLine
            {
                ItemId = ci.ItemId,
                Name = ci.Item.Name,
                Price = ci.Item.Price
            }).ToList()
        };
    }

    public async Task ReplaceAsync(int userId, IReadOnlyCollection<int> itemIds, string? ifMatch, CancellationToken ct)
    {
        var user = await dbContext.Users.Include(u => u.CartItems).ThenInclude(ci => ci.Item)
                            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) throw new KeyNotFoundException($"User {userId} not found");

        // Check cart version
        if (!string.IsNullOrEmpty(ifMatch))
        {
            var expected = ifMatch.Trim();
            var current = BuildWeakETag(user.CartVersion);
            if (!string.Equals(expected, current, StringComparison.Ordinal))
                throw new InvalidOperationException("ETag conflict");
        }

        var distinctIds = itemIds.Distinct().ToArray();
        var exists = await dbContext.Items.Where(i => distinctIds.Contains(i.Id) && i.IsActive)
                            .Select(i => i.Id)
                            .ToListAsync();

        // Check if items are available
        if (exists.Count != distinctIds.Length) throw new ArgumentException("One or more ItemId do not exist.");

        var curSet = user.CartItems.Select(ci => ci.ItemId).OrderBy(x => x).ToArray();
        var newSet = distinctIds.OrderBy(x => x).ToArray();
        var same = curSet.Length == newSet.Length && curSet.Zip(newSet, (a, b) => a == b).All(x => x);

        // Check if all items are the same to replace
        if (same) return;

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            // Making history json
            if (user.CartItems.Any())
            {
                var lines = await dbContext.CartItems.Where(ci => ci.UserId == userId)
                                  .Join(dbContext.Items, ci => ci.ItemId, it => it.Id, (ci, it) => new
                                  {
                                      itemId = it.Id,
                                      name = it.Name,
                                      priceAt = it.Price
                                  }).ToListAsync(ct);

                var snapshotObj = new
                {
                    userId,
                    snapshotObj = DateTime.UtcNow,
                    cartVersion = user.CartVersion,
                    items = lines
                };

                var json = JsonSerializer.Serialize(snapshotObj);

                dbContext.CartHistory.Add(new Models.CartHistory
                {
                    UserId = userId,
                    SnapshotAt = DateTime.UtcNow,
                    PayloadJson = json
                });

                await dbContext.SaveChangesAsync(ct);
            }

            // Replace cart
            dbContext.CartItems.RemoveRange(user.CartItems);
            await dbContext.SaveChangesAsync(ct);

            var rows = newSet.Select(id => new Models.CartItem { UserId = userId, ItemId = id }).ToList();
            dbContext.CartItems.AddRange(rows);

            user.ModifiedDateTime = DateTime.UtcNow;
            user.CartVersion++;

            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
    
    public string BuildWeakETag(int cartVersion) => $"W/\"{cartVersion}\"";
}