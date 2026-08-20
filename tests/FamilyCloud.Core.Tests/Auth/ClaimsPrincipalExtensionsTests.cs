using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FamilyCloud.Core.Auth;

namespace FamilyCloud.Core.Tests.Auth;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuth"));

    [Fact]
    public void GetUserId_parses_the_sub_claim()
    {
        var userId = Guid.NewGuid();
        var principal = PrincipalWith(new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()));

        Assert.Equal(userId, principal.GetUserId());
    }

    [Fact]
    public void GetUserId_throws_when_the_sub_claim_is_missing()
    {
        var principal = PrincipalWith();

        Assert.Throws<InvalidOperationException>(() => principal.GetUserId());
    }

    [Fact]
    public void GetFamilyId_parses_the_family_id_claim()
    {
        var familyId = Guid.NewGuid();
        var principal = PrincipalWith(new Claim(FamilyClaimTypes.FamilyId, familyId.ToString()));

        Assert.Equal(familyId, principal.GetFamilyId());
    }

    [Fact]
    public void IsFamilyAdmin_is_true_only_for_the_Admin_role_claim()
    {
        var admin = PrincipalWith(new Claim(FamilyClaimTypes.FamilyRole, "Admin"));
        var member = PrincipalWith(new Claim(FamilyClaimTypes.FamilyRole, "Member"));
        var none = PrincipalWith();

        Assert.True(admin.IsFamilyAdmin());
        Assert.False(member.IsFamilyAdmin());
        Assert.False(none.IsFamilyAdmin());
    }
}
