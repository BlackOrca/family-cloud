using Microsoft.EntityFrameworkCore;
using FamilyCloud.Contracts.Sync;
using FamilyCloud.Calendar.CalDav;
using FamilyCloud.Calendar.Domain;
using FamilyCloud.Calendar.Ics;
using FamilyCloud.Core.Sync;

namespace FamilyCloud.Calendar.Sync;

/// <summary>
/// Writes events through to the CalDAV server and keeps the local cache in sync with the result —
/// the counterpart to <see cref="CalendarSyncService"/>'s read-only pull. Takes the abstract EF Core
/// <see cref="DbContext"/> (not the concrete, composed FamilyCloudDbContext, which lives in
/// FamilyCloud.Server) since this feature project can't reference Server without creating a circular
/// project reference — see the Phase 1 architecture roadmap for why.
/// </summary>
public class CalendarWriteService(ICalDavClient calDavClient, DbContext db, SyncEventPublisher syncEvents)
{
    public async Task<CachedEvent> CreateEventAsync(
        CalendarAccount account, Domain.Calendar calendar, CalDavCredentials credentials,
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
        db.Set<CachedEvent>().Add(cached);
        syncEvents.Publish(SyncResourceType.Calendar, calendar.Id.ToString());
        await db.SaveChangesAsync(ct);
        return cached;
    }

    /// <summary>Updates an event, conditional on <paramref name="existing"/>'s cached ETag still matching
    /// the server (throws <see cref="CalDavException"/> with StatusCode 412 on a conflicting concurrent edit).</summary>
    public async Task<CachedEvent> UpdateEventAsync(
        CalendarAccount account, Domain.Calendar calendar, CachedEvent existing, CalDavCredentials credentials,
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
        CalendarAccount account, Domain.Calendar calendar, CachedEvent existing, CalDavCredentials credentials, CancellationToken ct = default)
    {
        var calendarUri = CalDavUris.CalendarUri(account, calendar);
        var eventUri = new Uri(calendarUri, $"{existing.UId}.ics");
        await calDavClient.DeleteEventAsync(eventUri, existing.ETag ?? "", credentials, ct);

        db.Set<CachedEvent>().Remove(existing);
        syncEvents.Publish(SyncResourceType.Calendar, calendar.Id.ToString());
        await db.SaveChangesAsync(ct);
    }
}
