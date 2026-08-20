using Microsoft.EntityFrameworkCore;
using OurLive.Contracts.Sync;
using OurLive.Core.Data;

namespace OurLive.Server.Api;

internal static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/sync/changes", async (long? since, OurLiveDbContext db) =>
        {
            var latestCursor = await db.SyncEvents.Select(e => (long?)e.Id).MaxAsync() ?? 0;

            if (since is null)
            {
                // Bootstrap: the caller already has fresh state from its own initial fetches, it
                // only needs a starting cursor, not a replay of history.
                return Results.Ok(new SyncChangesResponse(latestCursor, [], FullResyncRequired: false));
            }

            var changes = await db.SyncEvents
                .Where(e => e.Id > since.Value)
                .OrderBy(e => e.Id)
                .Select(e => new SyncChangeDto(e.Id, e.ResourceType, e.ResourceId, e.ChangedAtUtc))
                .ToListAsync();

            var cursor = changes.Count > 0 ? changes[^1].Cursor : since.Value;
            return Results.Ok(new SyncChangesResponse(cursor, changes, FullResyncRequired: false));
        }).RequireAuthorization("MobileApi");

        return endpoints;
    }
}
