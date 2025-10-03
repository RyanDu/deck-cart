using System.Text.Json;
using Deck.Api.Data;
using Deck.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Deck.Api.Telemetry;

namespace Deck.Api.Services;

public class CartService(AppDbContext dbContext) : ICartService
{
    public async Task<GetCartResponse> GetAsync(int userId, CancellationToken ct)
    {
        // Starting to get cart
        using var act = Tracing.Source.StartActivity("Cart.Get", ActivityKind.Internal);
        act?.SetTag("user.id", userId);

        var user = await dbContext.Users.Include(u => u.CartItems).ThenInclude(ci => ci.Item)
                            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            act?.SetStatus(ActivityStatusCode.Error, "user.not_found");
            throw new KeyNotFoundException($"User {userId} not found");
        }

        var res = new GetCartResponse
        {
            Name = user.Name,
            Cart = user.CartItems.OrderBy(ci => ci.Id).Select(ci => new CartLine
            {
                ItemId = ci.ItemId,
                Name = ci.Item.Name,
                Price = ci.Item.Price
            }).ToList()
        };

        act?.SetTag("cart.items.count", res.Cart.Count);
        act?.AddEvent(new ActivityEvent("cart.get.finish"));
        act?.SetStatus(ActivityStatusCode.Ok);
        return res;
    }

    public async Task ReplaceAsync(int userId, IReadOnlyCollection<int> itemIds, string? ifMatch, CancellationToken ct)
    {
        // Start Replaceing cart
        using var act = Tracing.Source.StartActivity("Cart.Replace", ActivityKind.Internal);
        act?.SetTag("user.id", userId);
        act?.SetTag("request.items.count", itemIds?.Count ?? 0);

        var user = await dbContext.Users.Include(u => u.CartItems).ThenInclude(ci => ci.Item)
                            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        
        if (!string.IsNullOrWhiteSpace(ifMatch)) act?.SetTag("if_match", ifMatch);

        if (user is null)
        {
            act?.SetStatus(ActivityStatusCode.Error, "user.not_found");
            throw new KeyNotFoundException($"User {userId} not found");
        }

        // Check cart version
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            var current = BuildWeakETag(user.CartVersion);
            var ok = string.Equals(ifMatch.Trim(), current, StringComparison.Ordinal);
            act?.SetTag("etag.current", current);
            act?.SetTag("etag.ok", ok);
            if (!ok)
            {
                act?.SetStatus(ActivityStatusCode.Error, "etag.conflict");
                act?.AddEvent(new ActivityEvent("etag.conflict"));
                throw new InvalidOperationException("ETag conflict");
            }
        }

        var distinctIds = itemIds.Distinct().ToArray();
        var exists = await dbContext.Items.Where(i => distinctIds.Contains(i.Id) && i.IsActive)
                            .Select(i => i.Id)
                            .ToListAsync();

        // Check if items are available
        if (exists.Count != distinctIds.Length)
        {
            act?.SetStatus(ActivityStatusCode.Error, "item.invalid");
            throw new ArgumentException("One or more ItemId do not exist.");
        }

        var curSet = user.CartItems.Select(ci => ci.ItemId).OrderBy(x => x).ToArray();
        var newSet = distinctIds.OrderBy(x => x).ToArray();
        var same = curSet.Length == newSet.Length && curSet.Zip(newSet, (a, b) => a == b).All(x => x);
        act?.SetTag("idempotent.same_set", same);

        // Check if all items are the same to replace
        if (same)
        {
            act?.AddEvent(new ActivityEvent("replace.skip_same_set"));
            act?.SetStatus(ActivityStatusCode.Ok);
            return;
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            // Making history json
            if (user.CartItems.Any())
            {
                act?.AddEvent(new ActivityEvent("snapshot.write.start",
                    tags: new ActivityTagsCollection { { "prev.count", user.CartItems.Count } }));

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
                act?.AddEvent(new ActivityEvent("snapshot.write.finish",
                    tags: new ActivityTagsCollection { { "snapshot.items", lines.Count } }));
            }

            // Replace cart
            act?.AddEvent(new ActivityEvent("db.replace.start"));
            dbContext.CartItems.RemoveRange(user.CartItems);
            await dbContext.SaveChangesAsync(ct);

            var rows = newSet.Select(id => new Models.CartItem { UserId = userId, ItemId = id }).ToList();
            dbContext.CartItems.AddRange(rows);

            user.ModifiedDateTime = DateTime.UtcNow;
            user.CartVersion++;

            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            act?.AddEvent(new ActivityEvent("db.replace.finish",
                tags: new ActivityTagsCollection { { "new.count", rows.Count }, { "version.new", user.CartVersion } }));

            act?.SetStatus(ActivityStatusCode.Ok);
        }
        catch(Exception ex)
        {
            await tx.RollbackAsync(ct);
            act?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public string BuildWeakETag(int cartVersion) => $"W/\"{cartVersion}\"";

    public async Task<int> GetCartVersionAsync(int userId, CancellationToken ct)
    {
        var v = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => (int?)u.CartVersion)
            .FirstOrDefaultAsync(ct);
        if (v is null) throw new KeyNotFoundException($"User {userId} not found");
        return v.Value;
    }
    
    public async Task<IReadOnlyList<CartHistoryDto>> GetHistoryAsync(int userId, int take, CancellationToken ct)
    {
        if (take <= 0 || take > 100) take = 5;

        using var act = Tracing.Source.StartActivity("Cart.History", ActivityKind.Internal);
        act?.SetTag("user.id", userId);
        act?.SetTag("take", take);

        var exists = await dbContext.Users.AnyAsync(u => u.Id == userId, ct);
        if (!exists)
        {
            act?.SetStatus(ActivityStatusCode.Error, "user.not_found");
            throw new KeyNotFoundException($"User {userId} not found");
        }

        var rows = await dbContext.CartHistory
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.SnapshotAt)
            .Take(take)
            .Select(h => new CartHistoryDto
            {
                Id = h.Id,
                SnapshotAt = h.SnapshotAt,
                PayloadJson = h.PayloadJson
            })
            .ToListAsync(ct);

        act?.SetTag("history.count", rows.Count);
        act?.SetStatus(ActivityStatusCode.Ok);
        return rows;
    }
}