namespace Deck.Api.DTOs;

public class ReplaceCartRequest
{
    public int UserId { get; set; }
    public List<CartItemId> Cart { get; set; } = new();
}

public class CartItemId
{
    public int ItemId{ get; set; }
}