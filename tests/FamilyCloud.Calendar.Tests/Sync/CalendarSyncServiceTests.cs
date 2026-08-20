using FamilyCloud.Contracts.Sync;
using FamilyCloud.Calendar.CalDav;
using FamilyCloud.Calendar.CalDav.Models;
using FamilyCloud.Calendar.Domain;
using FamilyCloud.Calendar.Sync;
using FamilyCloud.Core.Sync;

namespace FamilyCloud.Calendar.Tests.Sync;

public class CalendarSyncServiceTests : IDisposable
{
    private static readonly CalDavCredentials Credentials = new("testuser", "app-password");

    private readonly SqliteTestDb db = new();
    private readonly FakeCalDavClient calDavClient = new();

    private CalendarSyncService Service => new(calDavClient, db.Context, new SyncEventPublisher(db.Context));

    private CalendarAccount NewAccount() => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = "Radicale",
        BaseUrl = "http://localhost:5232/",
        Username = Credentials.Username,
        EncryptedAppPassword = "irrelevant-for-this-test",
        CreatedUtc = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task DiscoverCalendarsAsync_adds_a_new_calendar_when_none_exists_yet()
    {
        var account = NewAccount();
        db.Context.CalendarAccounts.Add(account);
        await db.Context.SaveChangesAsync();

        var homeUrl = new Uri("http://localhost:5232/testuser/");
        calDavClient.DiscoverCalendarHome = (_, _, _) => Task.FromResult(homeUrl);
        calDavClient.ListCalendars = (_, _, _) => Task.FromResult<IReadOnlyList<CalDavCalendarInfo>>(
            [new CalDavCalendarInfo(new Uri(homeUrl, "household/"), "Haushalt", "#ff0000", "ctag-1")]);

        await Service.DiscoverCalendarsAsync(account, SqliteTestDb.TestFamilyId, Credentials);

        var calendar = Assert.Single(db.Context.Calendars);
        Assert.Equal(account.Id, calendar.CalendarAccountId);
        Assert.Equal("/testuser/household/", calendar.CalDavHref);
        Assert.Equal("Haushalt", calendar.DisplayName);
        Assert.Equal("#ff0000", calendar.ColorHex);
        Assert.Equal("ctag-1", calendar.CTag);
        Assert.NotNull(account.LastDiscoveredUtc);
    }

    [Fact]
    public async Task DiscoverCalendarsAsync_updates_metadata_of_an_already_known_calendar()
    {
        var account = NewAccount();
        var existing = new Calendar.Domain.Calendar
        {
            Id = Guid.NewGuid(),
            FamilyId = SqliteTestDb.TestFamilyId,
            CalendarAccountId = account.Id,
            CalDavHref = "/testuser/household/",
            DisplayName = "Alter Name",
            ColorHex = "#000000",
            CTag = "ctag-old",
        };
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(existing);
        await db.Context.SaveChangesAsync();

        var homeUrl = new Uri("http://localhost:5232/testuser/");
        calDavClient.DiscoverCalendarHome = (_, _, _) => Task.FromResult(homeUrl);
        calDavClient.ListCalendars = (_, _, _) => Task.FromResult<IReadOnlyList<CalDavCalendarInfo>>(
            [new CalDavCalendarInfo(new Uri(homeUrl, "household/"), "Neuer Name", "#00ff00", "ctag-new")]);

        await Service.DiscoverCalendarsAsync(account, SqliteTestDb.TestFamilyId, Credentials);

        var calendar = Assert.Single(db.Context.Calendars);
        Assert.Equal(existing.Id, calendar.Id);
        Assert.Equal("Neuer Name", calendar.DisplayName);
        Assert.Equal("#00ff00", calendar.ColorHex);
        Assert.Equal("ctag-new", calendar.CTag);
    }

    [Fact]
    public async Task CreateCalendarAsync_creates_the_collection_and_adds_a_local_calendar_row()
    {
        var account = NewAccount();
        db.Context.CalendarAccounts.Add(account);
        await db.Context.SaveChangesAsync();

        var homeUrl = new Uri("http://localhost:5232/testuser/");
        calDavClient.DiscoverCalendarHome = (_, _, _) => Task.FromResult(homeUrl);

        Uri? createdUrl = null;
        calDavClient.CreateCalendar = (url, displayName, colorHex, _, _) =>
        {
            createdUrl = url;
            Assert.Equal("Kinder", displayName);
            Assert.Equal("#4287f5", colorHex);
            return Task.CompletedTask;
        };

        var calendar = await Service.CreateCalendarAsync(account, SqliteTestDb.TestFamilyId, Credentials, "Kinder", "#4287f5");

        var stored = Assert.Single(db.Context.Calendars);
        Assert.Equal(calendar.Id, stored.Id);
        Assert.Equal(account.Id, stored.CalendarAccountId);
        Assert.Equal("Kinder", stored.DisplayName);
        Assert.Equal("#4287f5", stored.ColorHex);
        Assert.StartsWith("/testuser/", stored.CalDavHref);
        Assert.NotNull(createdUrl);
        Assert.Equal(stored.CalDavHref, createdUrl!.AbsolutePath);
    }

    [Fact]
    public async Task UpdateCalendarAsync_updates_the_collection_and_the_local_row()
    {
        var (account, calendar) = NewAccountWithCalendar();
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        await db.Context.SaveChangesAsync();

        Uri? updatedUrl = null;
        calDavClient.UpdateCalendar = (url, displayName, colorHex, _, _) =>
        {
            updatedUrl = url;
            Assert.Equal("Neuer Name", displayName);
            Assert.Equal("#00ff00", colorHex);
            return Task.CompletedTask;
        };

        await Service.UpdateCalendarAsync(account, calendar, Credentials, "Neuer Name", "#00ff00");

        var stored = Assert.Single(db.Context.Calendars);
        Assert.Equal("Neuer Name", stored.DisplayName);
        Assert.Equal("#00ff00", stored.ColorHex);
        Assert.Equal(calendar.CalDavHref, updatedUrl!.AbsolutePath);
    }

    [Fact]
    public async Task DeleteCalendarAsync_deletes_the_collection_and_removes_the_local_row()
    {
        var (account, calendar) = NewAccountWithCalendar();
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        await db.Context.SaveChangesAsync();

        Uri? deletedUrl = null;
        calDavClient.DeleteCalendar = (url, _, _) =>
        {
            deletedUrl = url;
            return Task.CompletedTask;
        };

        await Service.DeleteCalendarAsync(account, calendar, Credentials);

        Assert.Empty(db.Context.Calendars);
        Assert.Equal(calendar.CalDavHref, deletedUrl!.AbsolutePath);
    }

    private static string EventIcs(string uid, string summary) =>
        $"""
         BEGIN:VCALENDAR
         VERSION:2.0
         PRODID:-//FamilyCloud//Test//EN
         BEGIN:VEVENT
         UID:{uid}
         DTSTAMP:20260819T120000Z
         DTSTART:20260825T090000Z
         DTEND:20260825T100000Z
         SUMMARY:{summary}
         END:VEVENT
         END:VCALENDAR
         """;

    private (CalendarAccount Account, Calendar.Domain.Calendar Calendar) NewAccountWithCalendar()
    {
        var account = NewAccount();
        var calendar = new Calendar.Domain.Calendar
        {
            Id = Guid.NewGuid(),
            FamilyId = SqliteTestDb.TestFamilyId,
            CalendarAccountId = account.Id,
            CalDavHref = "/testuser/household/",
            DisplayName = "Haushalt",
        };
        return (account, calendar);
    }

    [Fact]
    public async Task SyncEventsAsync_adds_a_new_cached_event_when_none_exists_yet()
    {
        var (account, calendar) = NewAccountWithCalendar();
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        await db.Context.SaveChangesAsync();

        var resource = new CalDavEventResource(
            new Uri("http://localhost:5232/testuser/household/event-1.ics"), "\"etag-1\"", EventIcs("event-1@familycloud", "Zahnarzt"));
        calDavClient.QueryEvents = (_, _, _, _, _) => Task.FromResult<IReadOnlyList<CalDavEventResource>>([resource]);

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow.AddDays(1);
        await Service.SyncEventsAsync(account, calendar, Credentials, start, end);

        var cached = Assert.Single(db.Context.CachedEvents);
        Assert.Equal("event-1@familycloud", cached.UId);
        Assert.Equal("Zahnarzt", cached.Summary);
        Assert.Equal("\"etag-1\"", cached.ETag);
        Assert.NotNull(calendar.LastSyncedUtc);

        var syncEvent = Assert.Single(db.Context.SyncEvents);
        Assert.Equal(SyncResourceType.Calendar, syncEvent.ResourceType);
        Assert.Equal(calendar.Id.ToString(), syncEvent.ResourceId);
    }

    [Fact]
    public async Task SyncEventsAsync_updates_an_existing_event_when_the_ETag_changed()
    {
        var (account, calendar) = NewAccountWithCalendar();
        var existing = new CachedEvent
        {
            Id = Guid.NewGuid(),
            CalendarId = calendar.Id,
            UId = "event-1@familycloud",
            Href = "/testuser/household/event-1.ics",
            ETag = "\"etag-old\"",
            Summary = "Alter Titel",
            RawIcs = EventIcs("event-1@familycloud", "Alter Titel"),
            LastSyncedUtc = DateTimeOffset.UtcNow.AddDays(-1),
        };
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        db.Context.CachedEvents.Add(existing);
        await db.Context.SaveChangesAsync();

        var resource = new CalDavEventResource(
            new Uri("http://localhost:5232/testuser/household/event-1.ics"), "\"etag-new\"", EventIcs("event-1@familycloud", "Neuer Titel"));
        calDavClient.QueryEvents = (_, _, _, _, _) => Task.FromResult<IReadOnlyList<CalDavEventResource>>([resource]);

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow.AddDays(1);
        await Service.SyncEventsAsync(account, calendar, Credentials, start, end);

        var cached = Assert.Single(db.Context.CachedEvents);
        Assert.Equal(existing.Id, cached.Id);
        Assert.Equal("Neuer Titel", cached.Summary);
        Assert.Equal("\"etag-new\"", cached.ETag);
    }

    [Fact]
    public async Task SyncEventsAsync_leaves_an_existing_event_untouched_when_the_ETag_is_unchanged()
    {
        var (account, calendar) = NewAccountWithCalendar();
        var lastSynced = DateTimeOffset.UtcNow.AddDays(-1);
        var existing = new CachedEvent
        {
            Id = Guid.NewGuid(),
            CalendarId = calendar.Id,
            UId = "event-1@familycloud",
            Href = "/testuser/household/event-1.ics",
            ETag = "\"etag-1\"",
            Summary = "Unveränderter Titel",
            RawIcs = EventIcs("event-1@familycloud", "Unveränderter Titel"),
            LastSyncedUtc = lastSynced,
        };
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        db.Context.CachedEvents.Add(existing);
        await db.Context.SaveChangesAsync();

        // Same ETag as cached — the server-side content is irrelevant here since the sync should never
        // even look at it once it decides nothing changed.
        var resource = new CalDavEventResource(
            new Uri("http://localhost:5232/testuser/household/event-1.ics"), "\"etag-1\"", EventIcs("event-1@familycloud", "Würde nie übernommen"));
        calDavClient.QueryEvents = (_, _, _, _, _) => Task.FromResult<IReadOnlyList<CalDavEventResource>>([resource]);

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow.AddDays(1);
        await Service.SyncEventsAsync(account, calendar, Credentials, start, end);

        var cached = Assert.Single(db.Context.CachedEvents);
        Assert.Equal("Unveränderter Titel", cached.Summary);
        Assert.Equal(lastSynced, cached.LastSyncedUtc);
        Assert.Empty(db.Context.SyncEvents);
    }

    [Fact]
    public async Task SyncEventsAsync_removes_a_cached_event_no_longer_present_upstream_within_the_window()
    {
        var (account, calendar) = NewAccountWithCalendar();
        var staleEvent = new CachedEvent
        {
            Id = Guid.NewGuid(),
            CalendarId = calendar.Id,
            UId = "deleted-event@familycloud",
            Href = "/testuser/household/deleted-event.ics",
            ETag = "\"etag-1\"",
            Summary = "Wurde extern gelöscht",
            StartUtc = DateTimeOffset.UtcNow,
            RawIcs = EventIcs("deleted-event@familycloud", "Wurde extern gelöscht"),
            LastSyncedUtc = DateTimeOffset.UtcNow.AddDays(-1),
        };
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        db.Context.CachedEvents.Add(staleEvent);
        await db.Context.SaveChangesAsync();

        // Radicale no longer returns this event at all — it was deleted upstream.
        calDavClient.QueryEvents = (_, _, _, _, _) => Task.FromResult<IReadOnlyList<CalDavEventResource>>([]);

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow.AddDays(1);
        await Service.SyncEventsAsync(account, calendar, Credentials, start, end);

        Assert.Empty(db.Context.CachedEvents);
        var syncEvent = Assert.Single(db.Context.SyncEvents);
        Assert.Equal(SyncResourceType.Calendar, syncEvent.ResourceType);
    }

    [Fact]
    public async Task SyncEventsAsync_does_not_remove_a_cached_event_outside_the_queried_window()
    {
        var (account, calendar) = NewAccountWithCalendar();
        var outOfWindowEvent = new CachedEvent
        {
            Id = Guid.NewGuid(),
            CalendarId = calendar.Id,
            UId = "far-future-event@familycloud",
            Href = "/testuser/household/far-future-event.ics",
            ETag = "\"etag-1\"",
            Summary = "Weit in der Zukunft",
            StartUtc = DateTimeOffset.UtcNow.AddYears(1),
            RawIcs = EventIcs("far-future-event@familycloud", "Weit in der Zukunft"),
            LastSyncedUtc = DateTimeOffset.UtcNow.AddDays(-1),
        };
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        db.Context.CachedEvents.Add(outOfWindowEvent);
        await db.Context.SaveChangesAsync();

        // Radicale is only queried for a near-term window and returns nothing — the far-future
        // cached event wasn't in scope for this pass, so it must not be treated as deleted.
        calDavClient.QueryEvents = (_, _, _, _, _) => Task.FromResult<IReadOnlyList<CalDavEventResource>>([]);

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow.AddDays(1);
        await Service.SyncEventsAsync(account, calendar, Credentials, start, end);

        Assert.Single(db.Context.CachedEvents);
        Assert.Empty(db.Context.SyncEvents);
    }

    public void Dispose() => db.Dispose();
}
