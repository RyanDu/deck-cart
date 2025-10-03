namespace Deck.Api.Models;

public class CartHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;
    public string PayloadJson { get; set; } = string.Empty;
}