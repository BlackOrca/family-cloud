using Ical.Net.DataTypes;
using OurLive.Core.CalDav.Models;
using OurLive.Core.Domain;
using IcsCalendar = Ical.Net.Calendar;

namespace OurLive.Core.Ics;

/// <summary>Maps between raw CalDAV VEVENT resources and the local <see cref="CachedEvent"/> cache.</summary>
public static class IcsMapper
{
    /// <summary>
    /// Maps one CalDAV resource to a cached event. A CalDAV resource holds exactly one VEVENT
    /// (or a recurring master plus its overridden occurrences, all sharing the same UID) — the
    /// master/first VEVENT's fields represent the cached row; <see cref="CachedEvent.RawIcs"/>
    /// keeps the full resource so nothing is lost even though it isn't individually modeled.
    /// </summary>
    public static CachedEvent ToCachedEvent(CalDavEventResource resource, Guid calendarId)
    {
        var calendar = IcsCalendar.Load(resource.IcsContent)
            ?? throw new InvalidOperationException($"CalDAV resource '{resource.Href}' could not be parsed as an iCalendar.");
        var vEvent = calendar.Events.FirstOrDefault()
            ?? throw new InvalidOperationException($"CalDAV resource '{resource.Href}' contains no VEVENT.");
        var uid = vEvent.Uid
            ?? throw new InvalidOperationException($"CalDAV resource '{resource.Href}' has a VEVENT with no UID.");
        var start = vEvent.Start
            ?? throw new InvalidOperationException($"CalDAV resource '{resource.Href}' has a VEVENT with no DTSTART.");

        return new CachedEvent
        {
            CalendarId = calendarId,
            UId = uid,
            Href = resource.Href.ToString(),
            ETag = resource.ETag,
            Summary = vEvent.Summary ?? "",
            Location = string.IsNullOrEmpty(vEvent.Location) ? null : vEvent.Location,
            Description = string.IsNullOrEmpty(vEvent.Description) ? null : vEvent.Description,
            StartUtc = ToUtcOffset(start),
            EndUtc = vEvent.End is null ? null : ToUtcOffset(vEvent.End),
            IsAllDay = vEvent.IsAllDay,
            RecurrenceRule = vEvent.RecurrenceRule?.ToString(),
            RawIcs = resource.IcsContent,
            LastSyncedUtc = DateTimeOffset.UtcNow,
        };
    }

    private static DateTimeOffset ToUtcOffset(CalDateTime value) => new(value.AsUtc, TimeSpan.Zero);
}
