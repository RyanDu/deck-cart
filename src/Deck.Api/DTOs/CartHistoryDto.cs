namespace Deck.Api.DTOs;

public class CartHistoryDto
{
    public int Id { get; set; }
    public DateTime SnapshotAt { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}
