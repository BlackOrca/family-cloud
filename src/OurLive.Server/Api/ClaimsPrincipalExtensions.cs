using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace OurLive.Server.Api;

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("The current principal has no 'sub' claim.");
        return Guid.Parse(value);
    }
}
