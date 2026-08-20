using Microsoft.EntityFrameworkCore;
using FamilyCloud.Contracts.Families;
using FamilyCloud.Core.Auth;
using FamilyCloud.Core.Data;
using FamilyCloud.Family.Domain;

namespace FamilyCloud.Family.Api;

public static class FamilyEndpoints
{
    public static IEndpointRouteBuilder MapFamilyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/family/members", async (HttpContext http, DbContext db) =>
        {
            var familyId = http.User.GetFamilyId();
            var members = await (
                from member in db.Set<FamilyMember>()
                join user in db.Set<AppUser>() on member.UserId equals user.Id
                where member.FamilyId == familyId
                select new FamilyMemberDto(user.Id, user.DisplayName, member.Role.ToString())
            ).ToListAsync();
            return Results.Ok(members);
        }).RequireAuthorization("MobileApi");

        return endpoints;
    }
}
