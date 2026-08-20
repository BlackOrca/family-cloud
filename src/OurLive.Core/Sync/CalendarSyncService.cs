using Microsoft.EntityFrameworkCore;
using OurLive.Core.CalDav;
using OurLive.Core.Data;
using OurLive.Core.Domain;
using OurLive.Core.Ics;

namespace OurLive.Core.Sync;

/// <summary>
/// Discovers calendars for a <see cref="CalendarAccount"/> and syncs their events into the local cache.
/// Read path only for now — event resources removed upstream during the queried window aren't yet
/// reconciled away locally; that's deferred until it's actually needed.
/// </summary>
public class CalendarSyncService(ICalDavClient calDavClient, OurLiveDbContext db)
{
    public async Task DiscoverCalendarsAsync(CalendarAccount account, CalDavCredentials credentials, CancellationToken ct = default)
    {
        var homeUrl = await calDavClient.DiscoverCalendarHomeAsync(new Uri(account.BaseUrl), credentials, ct);
        var discovered = await calDavClient.ListCalendarsAsync(homeUrl, credentials, ct);

        foreach (var info in discovered)
        {
            var href = info.Href.AbsolutePath;
            var existing = await db.Calendars.FirstOrDefaultAsync(
                c => c.CalendarAccountId == account.Id && c.CalDavHref == href, ct);

            if (existing is null)
            {
                db.Calendars.Add(new Calendar
                {
                    Id = Guid.NewGuid(),
                    CalendarAccountId = account.Id,
                    CalDavHref = href,
                    DisplayName = info.DisplayName,
                    ColorHex = info.ColorHex,
                    CTag = info.CTag,
                });
            }
            else
            {
                existing.DisplayName = info.DisplayName;
                existing.ColorHex = info.ColorHex;
                existing.CTag = info.CTag;
            }
        }

        account.LastDiscoveredUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<Calendar> CreateCalendarAsync(CalendarAccount account, CalDavCredentials credentials, string displayName, string? colorHex, CancellationToken ct = default)
    {
        var homeUrl = await calDavClient.DiscoverCalendarHomeAsync(new Uri(account.BaseUrl), credentials, ct);
        var href = $"{homeUrl.AbsolutePath.TrimEnd('/')}/{Guid.NewGuid():N}/";
        var calendarUrl = new Uri(homeUrl, href);

        await calDavClient.CreateCalendarAsync(calendarUrl, displayName, colorHex, credentials, ct);

        var calendar = new Calendar
        {
            Id = Guid.NewGuid(),
            CalendarAccountId = account.Id,
            CalDavHref = href,
            DisplayName = displayName,
            ColorHex = colorHex,
        };
        db.Calendars.Add(calendar);
        await db.SaveChangesAsync(ct);
        return calendar;
    }

    public async Task UpdateCalendarAsync(CalendarAccount account, Calendar calendar, CalDavCredentials credentials, string displayName, string? colorHex, CancellationToken ct = default)
    {
        var calendarUrl = CalDavUris.CalendarUri(account, calendar);
        await calDavClient.UpdateCalendarAsync(calendarUrl, displayName, colorHex, credentials, ct);

        calendar.DisplayName = displayName;
        calendar.ColorHex = colorHex;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCalendarAsync(CalendarAccount account, Calendar calendar, CalDavCredentials credentials, CancellationToken ct = default)
    {
        var calendarUrl = CalDavUris.CalendarUri(account, calendar);
        await calDavClient.DeleteCalendarAsync(calendarUrl, credentials, ct);

        db.Calendars.Remove(calendar);
        await db.SaveChangesAsync(ct);
    }

    public async Task SyncEventsAsync(CalendarAccount account, Calendar calendar, CalDavCredentials credentials, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var calendarUri = CalDavUris.CalendarUri(account, calendar);
        var resources = await calDavClient.QueryEventsAsync(calendarUri, start, end, credentials, ct);

        foreach (var resource in resources)
        {
            var mapped = IcsMapper.ToCachedEvent(resource, calendar.Id);
            var existing = await db.CachedEvents.FirstOrDefaultAsync(
                e => e.CalendarId == calendar.Id && e.UId == mapped.UId, ct);

            if (existing is null)
            {
                db.CachedEvents.Add(mapped);
            }
            else if (existing.ETag != mapped.ETag)
            {
                existing.ApplyContentFields(mapped.Summary, mapped.Location, mapped.Description, mapped.StartUtc, mapped.EndUtc, mapped.IsAllDay);
                existing.Href = mapped.Href;
                existing.ETag = mapped.ETag;
                existing.RecurrenceRule = mapped.RecurrenceRule;
                existing.RawIcs = mapped.RawIcs;
                existing.LastSyncedUtc = mapped.LastSyncedUtc;
            }
        }

        calendar.LastSyncedUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
