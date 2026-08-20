using System.Net;
using OurLive.Core.CalDav;
using OurLive.Core.Domain;
using OurLive.Core.Sync;

namespace OurLive.Core.Tests.Sync;

public class CalendarWriteServiceTests : IDisposable
{
    private static readonly CalDavCredentials Credentials = new("testuser", "app-password");

    private readonly SqliteTestDb db = new();
    private readonly FakeCalDavClient calDavClient = new();

    private CalendarWriteService Service => new(calDavClient, db.Context);

    private static (CalendarAccount Account, Calendar Calendar) NewAccountWithCalendar()
    {
        var account = new CalendarAccount
        {
            Id = Guid.NewGuid(),
            DisplayName = "Radicale",
            BaseUrl = "http://localhost:5232/",
            Username = Credentials.Username,
            EncryptedAppPassword = "irrelevant-for-this-test",
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        var calendar = new Calendar
        {
            Id = Guid.NewGuid(),
            CalendarAccountId = account.Id,
            CalDavHref = "/testuser/household/",
            DisplayName = "Haushalt",
        };
        return (account, calendar);
    }

    [Fact]
    public async Task CreateEventAsync_puts_the_new_event_to_the_calendar_and_caches_it()
    {
        var (account, calendar) = NewAccountWithCalendar();
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        await db.Context.SaveChangesAsync();

        Uri? putCalendarUrl = null;
        string? putUid = null;
        calDavClient.PutEvent = (calendarUrl, uid, ics, _, ifMatchEtag, _) =>
        {
            putCalendarUrl = calendarUrl;
            putUid = uid;
            Assert.Null(ifMatchEtag); // creation is unconditional
            Assert.Contains("Zahnarzttermin", ics);
            return Task.FromResult("\"etag-created\"");
        };

        var start = new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

        var created = await Service.CreateEventAsync(
            account, calendar, Credentials, "Zahnarzttermin", "Praxis", "Kontrolle", start, end, isAllDay: false);

        Assert.Equal(new Uri("http://localhost:5232/testuser/household/"), putCalendarUrl);
        Assert.Equal(created.UId, putUid);
        Assert.Equal("\"etag-created\"", created.ETag);
        Assert.Equal("Zahnarzttermin", created.Summary);
        Assert.Equal($"/testuser/household/{created.UId}.ics", created.Href);
        Assert.Same(created, Assert.Single(db.Context.CachedEvents));
    }

    [Fact]
    public async Task UpdateEventAsync_puts_conditionally_on_the_cached_ETag_and_updates_the_cache()
    {
        var (account, calendar) = NewAccountWithCalendar();
        var existing = new CachedEvent
        {
            Id = Guid.NewGuid(),
            CalendarId = calendar.Id,
            UId = "event-1@ourlive",
            Href = "/testuser/household/event-1.ics",
            ETag = "\"etag-old\"",
            Summary = "Alter Titel",
            RawIcs =
                """
                BEGIN:VCALENDAR
                VERSION:2.0
                PRODID:-//OurLive//Test//EN
                BEGIN:VEVENT
                UID:event-1@ourlive
                DTSTAMP:20260819T120000Z
                DTSTART:20260825T090000Z
                DTEND:20260825T100000Z
                SUMMARY:Alter Titel
                END:VEVENT
                END:VCALENDAR
                """,
            LastSyncedUtc = DateTimeOffset.UtcNow.AddDays(-1),
        };
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        db.Context.CachedEvents.Add(existing);
        await db.Context.SaveChangesAsync();

        string? seenIfMatch = null;
        calDavClient.PutEvent = (_, _, _, _, ifMatchEtag, _) =>
        {
            seenIfMatch = ifMatchEtag;
            return Task.FromResult("\"etag-new\"");
        };

        var start = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

        var updated = await Service.UpdateEventAsync(
            account, calendar, existing, Credentials, "Neuer Titel", null, null, start, end, isAllDay: false);

        Assert.Equal("\"etag-old\"", seenIfMatch);
        Assert.Equal("\"etag-new\"", updated.ETag);
        Assert.Equal("Neuer Titel", updated.Summary);
        Assert.Same(existing, updated);
    }

    [Fact]
    public async Task UpdateEventAsync_propagates_a_conflict_from_the_server_without_touching_the_cache()
    {
        var (account, calendar) = NewAccountWithCalendar();
        var existing = new CachedEvent
        {
            Id = Guid.NewGuid(),
            CalendarId = calendar.Id,
            UId = "event-1@ourlive",
            Href = "/testuser/household/event-1.ics",
            ETag = "\"etag-old\"",
            Summary = "Alter Titel",
            RawIcs =
                """
                BEGIN:VCALENDAR
                VERSION:2.0
                BEGIN:VEVENT
                UID:event-1@ourlive
                DTSTAMP:20260819T120000Z
                DTSTART:20260825T090000Z
                SUMMARY:Alter Titel
                END:VEVENT
                END:VCALENDAR
                """,
            LastSyncedUtc = DateTimeOffset.UtcNow,
        };
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        db.Context.CachedEvents.Add(existing);
        await db.Context.SaveChangesAsync();

        calDavClient.PutEvent = (_, _, _, _, _, _) =>
            throw new CalDavException("Conflict", HttpStatusCode.PreconditionFailed);

        var start = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<CalDavException>(() => Service.UpdateEventAsync(
            account, calendar, existing, Credentials, "Neuer Titel", null, null, start, null, isAllDay: false));

        Assert.Equal("Alter Titel", existing.Summary); // untouched: the exception short-circuits before any field is applied
    }

    [Fact]
    public async Task DeleteEventAsync_deletes_via_CalDAV_and_removes_the_cached_row()
    {
        var (account, calendar) = NewAccountWithCalendar();
        var existing = new CachedEvent
        {
            Id = Guid.NewGuid(),
            CalendarId = calendar.Id,
            UId = "event-1@ourlive",
            Href = "/testuser/household/event-1.ics",
            ETag = "\"etag-1\"",
            Summary = "Zu löschen",
            RawIcs = "irrelevant",
            LastSyncedUtc = DateTimeOffset.UtcNow,
        };
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        db.Context.CachedEvents.Add(existing);
        await db.Context.SaveChangesAsync();

        Uri? deletedUri = null;
        string? deletedEtag = null;
        calDavClient.DeleteEvent = (eventUrl, etag, _, _) =>
        {
            deletedUri = eventUrl;
            deletedEtag = etag;
            return Task.CompletedTask;
        };

        await Service.DeleteEventAsync(account, calendar, existing, Credentials);

        Assert.Equal(new Uri("http://localhost:5232/testuser/household/event-1@ourlive.ics"), deletedUri);
        Assert.Equal("\"etag-1\"", deletedEtag);
        Assert.Empty(db.Context.CachedEvents);
    }

    public void Dispose() => db.Dispose();
}
