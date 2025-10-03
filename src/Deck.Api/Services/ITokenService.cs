using System.Security.Claims;

namespace Deck.Api.Services;

public interface ITokenService
{
    string CreateToken(IEnumerable<Claim> claims, DateTime? expiresUtc = null);
}