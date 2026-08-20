using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FamilyCloud.Contracts.Account;
using FamilyCloud.Core.Auth;
using FamilyCloud.Core.Data;
using FamilyCloud.Family.Domain;

namespace FamilyCloud.Family.Api;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/account").RequireAuthorization("MobileApi");

        group.MapGet("/", async (HttpContext http, UserManager<AppUser> userManager, DbContext db) =>
        {
            var userId = http.User.GetUserId();
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return Results.NotFound();
            }

            var membership = await db.Set<FamilyMember>().FirstOrDefaultAsync(m => m.UserId == userId);
            return Results.Ok(new AccountProfileDto(
                user.UserName ?? "", user.DisplayName, user.Email, membership?.Role.ToString() ?? ""));
        });

        // Strictly scoped to the calling user via GetUserId() — never accepts a target user id, so
        // there's no way to change anyone else's password or profile through this endpoint.
        group.MapPut("/password", async (ChangePasswordRequest request, HttpContext http, UserManager<AppUser> userManager) =>
        {
            var userId = http.User.GetUserId();
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return Results.NotFound();
            }

            var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                return Results.Problem(
                    string.Join(", ", result.Errors.Select(e => e.Description)), statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.NoContent();
        });

        group.MapPut("/profile", async (UpdateProfileRequest request, HttpContext http, UserManager<AppUser> userManager) =>
        {
            var userId = http.User.GetUserId();
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return Results.NotFound();
            }

            user.DisplayName = request.DisplayName;
            user.Email = request.Email;
            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Results.Problem(
                    string.Join(", ", result.Errors.Select(e => e.Description)), statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.NoContent();
        });

        return endpoints;
    }
}
