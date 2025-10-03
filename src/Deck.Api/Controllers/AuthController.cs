using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Deck.Api.Data;
using Deck.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Deck.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AppDbContext db, ITokenService tokenSvc) : ControllerBase
{
    public record TokenRequest(int UserId);

    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<IActionResult> Issue([FromBody] TokenRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId && u.IsActive, ct);
        if (user is null) return Unauthorized();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Name),
        };

        var jwt = tokenSvc.CreateToken(claims);
        return Ok(new { access_token = jwt, token_type = "Bearer" });
    }
}
