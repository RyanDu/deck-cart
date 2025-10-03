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

    }
    
    public string BuildWeakETag(int cartVersion) => $"W/\"{cartVersion}\"";
}