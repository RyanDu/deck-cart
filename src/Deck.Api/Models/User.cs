namespace Deck.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}