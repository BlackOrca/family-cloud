using OurLive.Contracts.Sync;
using OurLive.Core.CalDav;
using OurLive.Core.Data;
using OurLive.Core.Domain;
using OurLive.Core.Ics;

namespace OurLive.Core.Sync;

/// <summary>
/// Writes events through to the CalDAV server and keeps the local cache in sync with the result —
/// the counterpart to <see cref="CalendarSyncService"/>'s read-only pull.
/// </summary>
public class CalendarWriteService(ICalDavClient calDavClient, OurLiveDbContext db, SyncEventPublisher syncEvents)
{
    public async Task<CachedEvent> CreateEventAsync(
        CalendarAccount account, Calendar calendar, CalDavCredentials credentials,
        string summary, string? location, string? description,
        DateTimeOffset startUtc, DateTimeOffset? endUtc, bool isAllDay, CancellationToken ct = default)
    {
        var uid = Guid.NewGuid().ToString();
        var ics = IcsMapper.BuildNewEventIcs(uid, summary, location, description, startUtc, endUtc, isAllDay);
        var calendarUri = CalDavUris.CalendarUri(account, calendar);
        var etag = await calDavClient.PutEventAsync(calendarUri, uid, ics, credentials, ifMatchEtag: null, ct);

        var cached = new CachedEvent
        {
            Id = Guid.NewGuid(),
            CalendarId = calendar.Id,
            UId = uid,
            Href = new Uri(calendarUri, $"{uid}.ics").AbsolutePath,
            ETag = etag,
            Summary = summary,
            Location = location,
            Description = description,
            StartUtc = startUtc,
            EndUtc = endUtc,
            IsAllDay = isAllDay,
            RawIcs = ics,
            LastSyncedUtc = DateTimeOffset.UtcNow,
        };
        db.CachedEvents.Add(cached);
        syncEvents.Publish(SyncResourceType.Calendar, calendar.Id.ToString());
        await db.SaveChangesAsync(ct);
        return cached;
    }

    /// <summary>Updates an event, conditional on <paramref name="existing"/>'s cached ETag still matching
    /// the server (throws <see cref="CalDavException"/> with StatusCode 412 on a conflicting concurrent edit).</summary>
    public async Task<CachedEvent> UpdateEventAsync(
        CalendarAccount account, Calendar calendar, CachedEvent existing, CalDavCredentials credentials,
        string summary, string? location, string? description,
        DateTimeOffset startUtc, DateTimeOffset? endUtc, bool isAllDay, CancellationToken ct = default)
    {
        var ics = IcsMapper.ApplyEventFields(existing.RawIcs, summary, location, description, startUtc, endUtc, isAllDay);
        var calendarUri = CalDavUris.CalendarUri(account, calendar);
        var etag = await calDavClient.PutEventAsync(calendarUri, existing.UId, ics, credentials, existing.ETag, ct);

        existing.ApplyContentFields(summary, location, description, startUtc, endUtc, isAllDay);
        existing.ETag = etag;
        existing.RawIcs = ics;
        existing.LastSyncedUtc = DateTimeOffset.UtcNow;
        syncEvents.Publish(SyncResourceType.Calendar, calendar.Id.ToString());
        await db.SaveChangesAsync(ct);
        return existing;
    }

    /// <summary>Deletes an event, conditional on <paramref name="existing"/>'s cached ETag still matching
    /// the server (throws <see cref="CalDavException"/> with StatusCode 412 on a conflicting concurrent edit).</summary>
    public async Task DeleteEventAsync(
        CalendarAccount account, Calendar calendar, CachedEvent existing, CalDavCredentials credentials, CancellationToken ct = default)
    {
        var calendarUri = CalDavUris.CalendarUri(account, calendar);
        var eventUri = new Uri(calendarUri, $"{existing.UId}.ics");
        await calDavClient.DeleteEventAsync(eventUri, existing.ETag ?? "", credentials, ct);

        db.CachedEvents.Remove(existing);
        syncEvents.Publish(SyncResourceType.Calendar, calendar.Id.ToString());
        await db.SaveChangesAsync(ct);
    }
}
