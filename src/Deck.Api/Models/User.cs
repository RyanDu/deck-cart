namespace Deck.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDateTime { get; set; } = DateTime.UtcNow;
    public int CartVersion { get; set; } = 0;
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}