namespace FamilyCloud.Core.Auth;

/// <summary>Custom JWT claim type names shared between the token issuer (FamilyCloud.Family's
/// AuthEndpoints) and every feature project's authorization checks (via ClaimsPrincipalExtensions).</summary>
public static class FamilyClaimTypes
{
    public const string FamilyId = "family_id";

    public const string FamilyRole = "family_role";
}
