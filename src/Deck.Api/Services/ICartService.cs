using Deck.Api.DTOs;

namespace Deck.Api.Services;

public interface ICartService
{
    Task<GetCartResponse> GetAsync(int userId, CancellationToken ct);
    Task ReplaceAsync(int userId, IReadOnlyCollection<int> itemIds, string? ifMatch, CancellationToken ct);
    string BuildWeakETag(int cartVersion);
}