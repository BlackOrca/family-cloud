using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
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

    /// <summary>
    /// Builds a brand-new single-occurrence VEVENT resource. Recurrence authoring isn't supported by the
    /// write path yet (out of scope for now) — <see cref="ApplyEventFields"/> below preserves any existing
    /// RRULE on edit instead of ever needing to construct one here.
    /// </summary>
    public static string BuildNewEventIcs(
        string uid, string summary, string? location, string? description,
        DateTimeOffset startUtc, DateTimeOffset? endUtc, bool isAllDay)
    {
        var calendar = new IcsCalendar();
        var vEvent = new CalendarEvent
        {
            Uid = uid,
            Summary = summary,
            Location = location,
            Description = description,
            DtStamp = new CalDateTime(DateTime.UtcNow, "UTC", true),
            Start = ToCalDateTime(startUtc, isAllDay),
            End = endUtc is null ? null : ToCalDateTime(endUtc.Value, isAllDay),
        };
        calendar.Events.Add(vEvent);
        return new CalendarSerializer().SerializeToString(calendar)
            ?? throw new InvalidOperationException("Failed to serialize the new event to ICS.");
    }

    /// <summary>
    /// Re-serializes an existing VEVENT resource with updated summary/location/description/time fields.
    /// Starting from the existing raw ICS (rather than building a fresh one, as <see cref="BuildNewEventIcs"/>
    /// does) preserves everything we don't explicitly touch — recurrence rules included.
    /// </summary>
    public static string ApplyEventFields(
        string existingRawIcs, string summary, string? location, string? description,
        DateTimeOffset startUtc, DateTimeOffset? endUtc, bool isAllDay)
    {
        var calendar = IcsCalendar.Load(existingRawIcs)
            ?? throw new InvalidOperationException("Existing event ICS could not be parsed.");
        var vEvent = calendar.Events.FirstOrDefault()
            ?? throw new InvalidOperationException("Existing event ICS contains no VEVENT.");

        vEvent.Summary = summary;
        vEvent.Location = location;
        vEvent.Description = description;
        vEvent.Start = ToCalDateTime(startUtc, isAllDay);
        vEvent.End = endUtc is null ? null : ToCalDateTime(endUtc.Value, isAllDay);
        vEvent.LastModified = new CalDateTime(DateTime.UtcNow, "UTC", true);

        return new CalendarSerializer().SerializeToString(calendar)
            ?? throw new InvalidOperationException("Failed to serialize the updated event to ICS.");
    }

    private static CalDateTime ToCalDateTime(DateTimeOffset value, bool isAllDay) =>
        isAllDay ? new CalDateTime(DateOnly.FromDateTime(value.UtcDateTime)) : new CalDateTime(value.UtcDateTime, "UTC", true);
}
