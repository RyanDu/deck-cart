using Deck.Api.DTOs;
using Deck.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Deck.Api.Controllers;

[ApiController]
[Route("cart")]
public class CartController(ICartService cartService) : ControllerBase
{
    [HttpPost("get")]
    public async Task<IActionResult> Get([FromBody] GetCartRequest req, CancellationToken ct)
    {
        var data = await cartService.GetAsync(req.UserId, ct);
        var version = await cartService.GetCartVersionAsync(req.UserId, ct);
        Response.Headers.ETag = cartService.BuildWeakETag(version);
        return Ok(data);
    }

    [HttpPost("replace")]
    public async Task<IActionResult> Replace([FromBody] ReplaceCartRequest req, CancellationToken ct)
    {
        var ifMatch = Request.Headers.IfMatch.ToString();
        await cartService.ReplaceAsync(req.UserId, req.Cart.Select(x => x.ItemId).ToArray(), ifMatch, ct);
        return NoContent();
    }

    [HttpGet("history/{userId:int}")]
    public async Task<IActionResult> History([FromRoute] int userId, [FromQuery] int take = 5, CancellationToken ct = default)
    {
        var rows = await cartService.GetHistoryAsync(userId, take, ct);
        return Ok(rows);
    }
}
