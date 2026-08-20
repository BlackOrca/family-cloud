using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FamilyCloud.Core.Auth;

/// <summary>Lives in Core (not any one feature project) since every feature's API endpoints need
/// <see cref="GetUserId"/>, and family-scoping/admin checks are a cross-cutting concern too.</summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("The current principal has no 'sub' claim.");
        return Guid.Parse(value);
    }

    public static Guid GetFamilyId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(FamilyClaimTypes.FamilyId)
            ?? throw new InvalidOperationException("The current principal has no 'family_id' claim.");
        return Guid.Parse(value);
    }

    public static bool IsFamilyAdmin(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(FamilyClaimTypes.FamilyRole) == "Admin";
}
