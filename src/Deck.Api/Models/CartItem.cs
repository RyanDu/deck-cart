using Microsoft.AspNetCore.Http.Features;

namespace Deck.Api.Models;

public class CartItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ItemId { get; set; }
    public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDateTime { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Item item { get; set; } = null!;
}