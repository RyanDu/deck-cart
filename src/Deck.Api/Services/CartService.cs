using Deck.Api.Data;
using Deck.Api.DTOs;

namespace Deck.Api.Services;

public class CartService(AppDbContext dbContext) : ICartService
{
    public async Task<GetCartResponse> GetAsync(int userId, CancellationToken ct)
    {
        return new GetCartResponse { };
    }

    public async Task ReplaceAsync(int userId, IReadOnlyCollection<int> itemIds, string? ifMatch, CancellationToken ct)
    {

    }
    
    public string BuildWeakETag(int cartVersion) => $"W/\"{cartVersion}\"";
}