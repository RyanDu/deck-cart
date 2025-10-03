namespace Deck.Api.DTOs;

public class GetCartResponse
{
    public string Name { get; set; } = string.Empty;
    public List<CartLine> Cart { get; set; } = new();
}

public class CartLine
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price{ get; set; }
}