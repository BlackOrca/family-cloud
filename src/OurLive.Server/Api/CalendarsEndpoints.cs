using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using OurLive.Contracts.Calendars;
using OurLive.Core.Data;

namespace OurLive.Server.Api;

internal static class CalendarsEndpoints
{
    public static IEndpointRouteBuilder MapCalendarsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/calendars").RequireAuthorization("MobileApi");

        // Only the calendars the caller has an explicit CalendarPermission for — never the full list.
        group.MapGet("/", async (HttpContext http, OurLiveDbContext db) =>
        {
            var userId = http.User.GetUserId();
            var calendars = await db.CalendarPermissions
                .Where(p => p.UserId == userId)
                .Select(p => new CalendarDto(p.Calendar!.Id, p.Calendar.DisplayName, p.Calendar.ColorHex, p.CanWrite))
                .ToListAsync();
            return Results.Ok(calendars);
        });

        group.MapGet("/{calendarId:guid}/events", async (
            Guid calendarId, HttpContext http, OurLiveDbContext db, DateTimeOffset? start, DateTimeOffset? end) =>
        {
            var userId = http.User.GetUserId();
            var hasAccess = await db.CalendarPermissions.AnyAsync(p => p.UserId == userId && p.CalendarId == calendarId);
            if (!hasAccess)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var rangeStart = start ?? DateTimeOffset.UtcNow.AddMonths(-3);
            var rangeEnd = end ?? DateTimeOffset.UtcNow.AddMonths(6);

            // Filtered by calendar in SQL, then the start/end overlap check runs in memory — a
            // household calendar's cached events are small enough that this is simpler than
            // fighting the SQLite provider's translation of a mixed nullable date comparison.
            var events = (await db.CachedEvents.Where(e => e.CalendarId == calendarId).ToListAsync())
                .Where(e => e.StartUtc < rangeEnd && (e.EndUtc is null || e.EndUtc > rangeStart))
                .OrderBy(e => e.StartUtc)
                .Select(e => new EventDto(e.Id, e.CalendarId, e.Summary, e.Location, e.Description, e.StartUtc, e.EndUtc, e.IsAllDay, e.RecurrenceRule))
                .ToList();

            return Results.Ok(events);
        });

        return endpoints;
    }
}
