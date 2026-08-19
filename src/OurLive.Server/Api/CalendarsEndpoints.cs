using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using OurLive.Contracts.Calendars;
using OurLive.Core.CalDav;
using OurLive.Core.Data;
using OurLive.Core.Security;
using OurLive.Core.Sync;

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
                .Select(ToDto)
                .ToList();

            return Results.Ok(events);
        });

        group.MapPost("/{calendarId:guid}/events", async (
            Guid calendarId, EventWriteRequest request, HttpContext http, OurLiveDbContext db,
            ICalDavPasswordProtector passwordProtector, CalendarWriteService writeService) =>
        {
            var userId = http.User.GetUserId();
            var permission = await db.CalendarPermissions
                .Include(p => p.Calendar!).ThenInclude(c => c!.CalendarAccount)
                .FirstOrDefaultAsync(p => p.UserId == userId && p.CalendarId == calendarId);
            if (permission is null || !permission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var calendar = permission.Calendar!;
            var account = calendar.CalendarAccount!;
            var credentials = new CalDavCredentials(account.Username, passwordProtector.Decrypt(account.EncryptedAppPassword));

            try
            {
                var created = await writeService.CreateEventAsync(
                    account, calendar, credentials,
                    request.Summary, request.Location, request.Description, request.StartUtc, request.EndUtc, request.IsAllDay);
                return Results.Created($"/api/events/{created.Id}", ToDto(created));
            }
            catch (CalDavException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        var eventsGroup = endpoints.MapGroup("/api/events").RequireAuthorization("MobileApi");

        eventsGroup.MapPut("/{eventId:guid}", async (
            Guid eventId, EventWriteRequest request, HttpContext http, OurLiveDbContext db,
            ICalDavPasswordProtector passwordProtector, CalendarWriteService writeService) =>
        {
            var userId = http.User.GetUserId();
            var existing = await db.CachedEvents
                .Include(e => e.Calendar!).ThenInclude(c => c!.CalendarAccount)
                .FirstOrDefaultAsync(e => e.Id == eventId);
            if (existing is null)
            {
                return Results.NotFound();
            }

            var permission = await db.CalendarPermissions.FirstOrDefaultAsync(p => p.UserId == userId && p.CalendarId == existing.CalendarId);
            if (permission is null || !permission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var calendar = existing.Calendar!;
            var account = calendar.CalendarAccount!;
            var credentials = new CalDavCredentials(account.Username, passwordProtector.Decrypt(account.EncryptedAppPassword));

            try
            {
                var updated = await writeService.UpdateEventAsync(
                    account, calendar, existing, credentials,
                    request.Summary, request.Location, request.Description, request.StartUtc, request.EndUtc, request.IsAllDay);
                return Results.Ok(ToDto(updated));
            }
            catch (CalDavException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            {
                // Someone else changed the event on the CalDAV server since we last synced it — refuse
                // to blindly overwrite; the caller should reload and retry.
                return Results.Problem(
                    "Der Termin wurde zwischenzeitlich anderweitig geändert.", statusCode: StatusCodes.Status409Conflict);
            }
            catch (CalDavException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        eventsGroup.MapDelete("/{eventId:guid}", async (
            Guid eventId, HttpContext http, OurLiveDbContext db,
            ICalDavPasswordProtector passwordProtector, CalendarWriteService writeService) =>
        {
            var userId = http.User.GetUserId();
            var existing = await db.CachedEvents
                .Include(e => e.Calendar!).ThenInclude(c => c!.CalendarAccount)
                .FirstOrDefaultAsync(e => e.Id == eventId);
            if (existing is null)
            {
                return Results.NotFound();
            }

            var permission = await db.CalendarPermissions.FirstOrDefaultAsync(p => p.UserId == userId && p.CalendarId == existing.CalendarId);
            if (permission is null || !permission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var calendar = existing.Calendar!;
            var account = calendar.CalendarAccount!;
            var credentials = new CalDavCredentials(account.Username, passwordProtector.Decrypt(account.EncryptedAppPassword));

            try
            {
                await writeService.DeleteEventAsync(account, calendar, existing, credentials);
                return Results.NoContent();
            }
            catch (CalDavException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            {
                return Results.Problem(
                    "Der Termin wurde zwischenzeitlich anderweitig geändert.", statusCode: StatusCodes.Status409Conflict);
            }
            catch (CalDavException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        return endpoints;
    }

    private static EventDto ToDto(OurLive.Core.Domain.CachedEvent e) =>
        new(e.Id, e.CalendarId, e.Summary, e.Location, e.Description, e.StartUtc, e.EndUtc, e.IsAllDay, e.RecurrenceRule);
}
