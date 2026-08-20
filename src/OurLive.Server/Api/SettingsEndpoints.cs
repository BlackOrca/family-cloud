using Microsoft.EntityFrameworkCore;
using OurLive.Contracts.Settings;
using OurLive.Core.Data;

namespace OurLive.Server.Api;

internal static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Anonymous: the login screen (both admin UI and the MAUI app) needs the configured
        // app name before the user has authenticated.
        endpoints.MapGet("/api/settings", async (OurLiveDbContext db) =>
        {
            var title = await db.AppSettings.Select(s => s.Title).FirstOrDefaultAsync() ?? "OurLive";
            return Results.Ok(new AppSettingsDto(title));
        }).AllowAnonymous();

        return endpoints;
    }
}
