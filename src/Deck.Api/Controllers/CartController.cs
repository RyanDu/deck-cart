using Deck.Api.DTOs;
using Deck.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Deck.Api.Controllers;

[ApiController]
[Route("cart")]
public class CartController(ICartService cartService) : ControllerBase
{
    [HttpPost("get")]
    [ProducesResponseType(typeof(GetCartResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get([FromBody] GetCartRequest request, CancellationToken ct)
    {
        try
        {
            var data = await cartService.GetAsync(request.UserId, ct);

            return Ok(data);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("replace")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> Replace([FromBody] ReplaceCartRequest request, CancellationToken ct)
    {
        try
        {
            var ifMatch = Request.Headers.IfMatch.ToString();
            await cartService.ReplaceAsync(request.UserId, request.Cart.Select(c => c.ItemId).ToArray(), ifMatch, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { error = "ValidationFailed", details = ex.Message });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { error = "ETagConflict" });
        }
    }
}