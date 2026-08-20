using Microsoft.EntityFrameworkCore;
using FamilyCloud.Contracts.Sync;
using FamilyCloud.Core.CalDav;
using FamilyCloud.Core.Data;
using FamilyCloud.Core.Domain;
using FamilyCloud.Core.Ics;

namespace FamilyCloud.Core.Sync;

/// <summary>
/// Discovers calendars for a <see cref="CalendarAccount"/> and syncs their events into the local cache.
/// Read path only — writes go through <see cref="CalendarWriteService"/> instead.
/// </summary>
public class CalendarSyncService(ICalDavClient calDavClient, FamilyCloudDbContext db, SyncEventPublisher syncEvents)
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

        var seenUIds = new HashSet<string>();
        var hasChanges = false;

        foreach (var resource in resources)
        {
            var mapped = IcsMapper.ToCachedEvent(resource, calendar.Id);
            seenUIds.Add(mapped.UId);
            var existing = await db.CachedEvents.FirstOrDefaultAsync(
                e => e.CalendarId == calendar.Id && e.UId == mapped.UId, ct);

            if (existing is null)
            {
                db.CachedEvents.Add(mapped);
                hasChanges = true;
            }
            else if (existing.ETag != mapped.ETag)
            {
                existing.ApplyContentFields(mapped.Summary, mapped.Location, mapped.Description, mapped.StartUtc, mapped.EndUtc, mapped.IsAllDay);
                existing.Href = mapped.Href;
                existing.ETag = mapped.ETag;
                existing.RecurrenceRule = mapped.RecurrenceRule;
                existing.RawIcs = mapped.RawIcs;
                existing.LastSyncedUtc = mapped.LastSyncedUtc;
                hasChanges = true;
            }
        }

        // Anything cached in this calendar that overlaps the queried window but wasn't returned by
        // Radicale this pass was deleted upstream (e.g. by a third-party CalDAV client) — remove it.
        // Filtered by calendar in SQL, then the start/end overlap check runs in memory — same
        // reasoning as CalendarsEndpoints' event query: SQLite can't translate the mixed nullable
        // DateTimeOffset comparison, and a household calendar's cache is small enough not to care.
        var staleEvents = (await db.CachedEvents.Where(e => e.CalendarId == calendar.Id).ToListAsync(ct))
            .Where(e => e.StartUtc < end && (e.EndUtc is null || e.EndUtc > start))
            .Where(e => !seenUIds.Contains(e.UId))
            .ToList();
        if (staleEvents.Count > 0)
        {
            db.CachedEvents.RemoveRange(staleEvents);
            hasChanges = true;
        }

        if (hasChanges)
        {
            syncEvents.Publish(SyncResourceType.Calendar, calendar.Id.ToString());
        }

        calendar.LastSyncedUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
