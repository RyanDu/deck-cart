using Deck.Api.DTOs;
using Deck.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Deck.Api.Controllers;

[Authorize]
[ApiController]
[Route("cart")]
public class CartController(ICartService cartService) : ControllerBase
{
    [HttpPost("get")]
    public async Task<IActionResult> Get([FromBody] GetCartRequest req, CancellationToken ct)
    {
        Activity.Current?.SetTag("req.user.id", req.UserId);
        Activity.Current?.AddEvent(new ActivityEvent("controller.get.start"));

        var data = await cartService.GetAsync(req.UserId, ct);
        var version = await cartService.GetCartVersionAsync(req.UserId, ct);
        Response.Headers.ETag = cartService.BuildWeakETag(version);

        Activity.Current?.AddEvent(new ActivityEvent("controller.get.finish",
            tags: new ActivityTagsCollection { { "etag", Response.Headers.ETag.ToString() } }));
        return Ok(data);
    }

    [HttpPost("replace")]
    public async Task<IActionResult> Replace([FromBody] ReplaceCartRequest req, CancellationToken ct)
    {
        Activity.Current?.SetTag("req.user.id", req.UserId);
        Activity.Current?.AddEvent(new ActivityEvent("controller.replace.start"));

        var ifMatch = Request.Headers.IfMatch.ToString();
        await cartService.ReplaceAsync(req.UserId, req.Cart.Select(x => x.ItemId).ToArray(), ifMatch, ct);

        Activity.Current?.AddEvent(new ActivityEvent("controller.replace.finish",
            tags: new ActivityTagsCollection { { "Ifmatch", Response.Headers.IfMatch.ToString() } }));
        return NoContent();
    }

    [HttpGet("history/{userId:int}")]
    public async Task<IActionResult> History([FromRoute] int userId, [FromQuery] int take = 5, CancellationToken ct = default)
    {
        Activity.Current?.SetTag("user id", userId);
        Activity.Current?.AddEvent(new ActivityEvent("controller.history.start"));

        var rows = await cartService.GetHistoryAsync(userId, take, ct);

        Activity.Current?.AddEvent(new ActivityEvent("controller.history.finish"));
        return Ok(rows);
    }
}
