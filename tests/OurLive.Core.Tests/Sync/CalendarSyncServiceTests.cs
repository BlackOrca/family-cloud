using OurLive.Core.CalDav;
using OurLive.Core.CalDav.Models;
using OurLive.Core.Domain;
using OurLive.Core.Sync;

namespace OurLive.Core.Tests.Sync;

public class CalendarSyncServiceTests : IDisposable
{
    private static readonly CalDavCredentials Credentials = new("testuser", "app-password");

    private readonly SqliteTestDb db = new();
    private readonly FakeCalDavClient calDavClient = new();

    private CalendarSyncService Service => new(calDavClient, db.Context);

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

        await Service.DiscoverCalendarsAsync(account, Credentials);

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
        var existing = new Calendar
        {
            Id = Guid.NewGuid(),
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

        await Service.DiscoverCalendarsAsync(account, Credentials);

        var calendar = Assert.Single(db.Context.Calendars);
        Assert.Equal(existing.Id, calendar.Id);
        Assert.Equal("Neuer Name", calendar.DisplayName);
        Assert.Equal("#00ff00", calendar.ColorHex);
        Assert.Equal("ctag-new", calendar.CTag);
    }

    private static string EventIcs(string uid, string summary) =>
        $"""
         BEGIN:VCALENDAR
         VERSION:2.0
         PRODID:-//OurLive//Test//EN
         BEGIN:VEVENT
         UID:{uid}
         DTSTAMP:20260819T120000Z
         DTSTART:20260825T090000Z
         DTEND:20260825T100000Z
         SUMMARY:{summary}
         END:VEVENT
         END:VCALENDAR
         """;

    private (CalendarAccount Account, Calendar Calendar) NewAccountWithCalendar()
    {
        var account = NewAccount();
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
    public async Task SyncEventsAsync_adds_a_new_cached_event_when_none_exists_yet()
    {
        var (account, calendar) = NewAccountWithCalendar();
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        await db.Context.SaveChangesAsync();

        var resource = new CalDavEventResource(
            new Uri("http://localhost:5232/testuser/household/event-1.ics"), "\"etag-1\"", EventIcs("event-1@ourlive", "Zahnarzt"));
        calDavClient.QueryEvents = (_, _, _, _, _) => Task.FromResult<IReadOnlyList<CalDavEventResource>>([resource]);

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow.AddDays(1);
        await Service.SyncEventsAsync(account, calendar, Credentials, start, end);

        var cached = Assert.Single(db.Context.CachedEvents);
        Assert.Equal("event-1@ourlive", cached.UId);
        Assert.Equal("Zahnarzt", cached.Summary);
        Assert.Equal("\"etag-1\"", cached.ETag);
        Assert.NotNull(calendar.LastSyncedUtc);
    }

    [Fact]
    public async Task SyncEventsAsync_updates_an_existing_event_when_the_ETag_changed()
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
            RawIcs = EventIcs("event-1@ourlive", "Alter Titel"),
            LastSyncedUtc = DateTimeOffset.UtcNow.AddDays(-1),
        };
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        db.Context.CachedEvents.Add(existing);
        await db.Context.SaveChangesAsync();

        var resource = new CalDavEventResource(
            new Uri("http://localhost:5232/testuser/household/event-1.ics"), "\"etag-new\"", EventIcs("event-1@ourlive", "Neuer Titel"));
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
            UId = "event-1@ourlive",
            Href = "/testuser/household/event-1.ics",
            ETag = "\"etag-1\"",
            Summary = "Unveränderter Titel",
            RawIcs = EventIcs("event-1@ourlive", "Unveränderter Titel"),
            LastSyncedUtc = lastSynced,
        };
        db.Context.CalendarAccounts.Add(account);
        db.Context.Calendars.Add(calendar);
        db.Context.CachedEvents.Add(existing);
        await db.Context.SaveChangesAsync();

        // Same ETag as cached — the server-side content is irrelevant here since the sync should never
        // even look at it once it decides nothing changed.
        var resource = new CalDavEventResource(
            new Uri("http://localhost:5232/testuser/household/event-1.ics"), "\"etag-1\"", EventIcs("event-1@ourlive", "Würde nie übernommen"));
        calDavClient.QueryEvents = (_, _, _, _, _) => Task.FromResult<IReadOnlyList<CalDavEventResource>>([resource]);

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow.AddDays(1);
        await Service.SyncEventsAsync(account, calendar, Credentials, start, end);

        var cached = Assert.Single(db.Context.CachedEvents);
        Assert.Equal("Unveränderter Titel", cached.Summary);
        Assert.Equal(lastSynced, cached.LastSyncedUtc);
    }

    public void Dispose() => db.Dispose();
}
