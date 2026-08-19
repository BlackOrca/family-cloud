using OurLive.Core.CalDav.Models;
using OurLive.Core.Ics;

namespace OurLive.Core.Tests.Ics;

public class IcsMapperTests
{
    private static readonly Guid CalendarId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static CalDavEventResource Resource(string ics, string etag = "\"etag-1\"") =>
        new(new Uri("http://localhost:5232/testuser/household/event-1.ics"), etag, ics);

    [Fact]
    public void ToCachedEvent_maps_a_simple_timed_event()
    {
        const string ics =
            """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//OurLive//Test//EN
            BEGIN:VEVENT
            UID:simple-event@ourlive
            DTSTAMP:20260819T120000Z
            DTSTART:20260825T090000Z
            DTEND:20260825T100000Z
            SUMMARY:Zahnarzttermin
            LOCATION:Praxis Dr. Müller
            DESCRIPTION:Kontrolle
            END:VEVENT
            END:VCALENDAR
            """;

        var result = IcsMapper.ToCachedEvent(Resource(ics), CalendarId);

        Assert.Equal(CalendarId, result.CalendarId);
        Assert.Equal("simple-event@ourlive", result.UId);
        Assert.Equal("\"etag-1\"", result.ETag);
        Assert.Equal("Zahnarzttermin", result.Summary);
        Assert.Equal("Praxis Dr. Müller", result.Location);
        Assert.Equal("Kontrolle", result.Description);
        Assert.False(result.IsAllDay);
        Assert.Null(result.RecurrenceRule);
        Assert.Equal(new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.Zero), result.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero), result.EndUtc);
        Assert.Equal(ics, result.RawIcs);
    }

    [Fact]
    public void ToCachedEvent_maps_a_recurring_event_and_keeps_the_raw_RRULE()
    {
        const string ics =
            """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//OurLive//Test//EN
            BEGIN:VEVENT
            UID:weekly-event@ourlive
            DTSTAMP:20260819T120000Z
            DTSTART:20260825T180000Z
            DTEND:20260825T190000Z
            SUMMARY:Wöchentliches Meeting
            RRULE:FREQ=WEEKLY;BYDAY=TU
            END:VEVENT
            END:VCALENDAR
            """;

        var result = IcsMapper.ToCachedEvent(Resource(ics), CalendarId);

        Assert.Equal("weekly-event@ourlive", result.UId);
        Assert.NotNull(result.RecurrenceRule);
        Assert.Contains("WEEKLY", result.RecurrenceRule);
    }

    [Fact]
    public void ToCachedEvent_maps_an_all_day_event()
    {
        const string ics =
            """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//OurLive//Test//EN
            BEGIN:VEVENT
            UID:all-day-event@ourlive
            DTSTAMP:20260819T120000Z
            DTSTART;VALUE=DATE:20260901
            DTEND;VALUE=DATE:20260902
            SUMMARY:Geburtstag
            END:VEVENT
            END:VCALENDAR
            """;

        var result = IcsMapper.ToCachedEvent(Resource(ics), CalendarId);

        Assert.True(result.IsAllDay);
        Assert.Equal("Geburtstag", result.Summary);
    }

    [Fact]
    public void ToCachedEvent_throws_when_the_resource_has_no_VEVENT()
    {
        const string ics =
            """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//OurLive//Test//EN
            END:VCALENDAR
            """;

        Assert.Throws<InvalidOperationException>(() => IcsMapper.ToCachedEvent(Resource(ics), CalendarId));
    }
}
