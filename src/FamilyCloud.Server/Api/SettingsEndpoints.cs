using Microsoft.EntityFrameworkCore;
using FamilyCloud.Contracts.Settings;
using FamilyCloud.Core.Data;

namespace FamilyCloud.Server.Api;

internal static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Anonymous: the login screen (both admin UI and the MAUI app) needs the configured
        // app name before the user has authenticated.
        endpoints.MapGet("/api/settings", async (FamilyCloudDbContext db) =>
        {
            var title = await db.AppSettings.Select(s => s.Title).FirstOrDefaultAsync() ?? "FamilyCloud";
            return Results.Ok(new AppSettingsDto(title));
        }).AllowAnonymous();

        return endpoints;
    }
}
