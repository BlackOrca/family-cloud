using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OurLive.Core.Data;

namespace Microsoft.AspNetCore.Routing;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    // Required by the Login Razor component in /Components/Account/Pages.
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var accountGroup = endpoints.MapGroup("/Account");

        accountGroup.MapPost("/Logout", async (
            ClaimsPrincipal user,
            [FromServices] SignInManager<AppUser> signInManager,
            [FromForm] string returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect($"~/{returnUrl}");
        });

        // Refreshing the auth cookie (e.g. after a password change rotates the security stamp)
        // has to happen on a plain HTTP request: from an interactive Blazor Server component the
        // response has already started streaming, so writing a Set-Cookie header there throws.
        accountGroup.MapGet("/RefreshSignIn", async (
            ClaimsPrincipal principal,
            [FromServices] UserManager<AppUser> userManager,
            [FromServices] SignInManager<AppUser> signInManager,
            [FromQuery] string returnUrl) =>
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is not null)
            {
                await signInManager.RefreshSignInAsync(user);
            }
            return TypedResults.LocalRedirect($"~/{returnUrl}");
        }).RequireAuthorization();

        return accountGroup;
    }
}
