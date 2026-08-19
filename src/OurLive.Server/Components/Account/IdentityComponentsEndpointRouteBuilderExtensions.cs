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

        return accountGroup;
    }
}
