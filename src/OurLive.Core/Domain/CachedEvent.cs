namespace OurLive.Core.Domain;

/// <summary>Local cache of one CalDAV VEVENT, synced from the server's calendar.</summary>
public class CachedEvent
{
    public Guid Id { get; set; }

    public Guid CalendarId { get; set; }

    public Calendar? Calendar { get; set; }

    /// <summary>iCal UID — stable across syncs, used to match cached events to CalDAV resources.</summary>
    public required string UId { get; set; }

    /// <summary>CalDAV resource path, needed for PUT/DELETE.</summary>
    public required string Href { get; set; }

    /// <summary>CalDAV ETag, for conditional updates and conflict detection.</summary>
    public string? ETag { get; set; }

    public required string Summary { get; set; }

    public string? Location { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset? EndUtc { get; set; }

    public bool IsAllDay { get; set; }

    /// <summary>Raw RRULE string, kept opaque server-side (Ical.Net owns interpretation).</summary>
    public string? RecurrenceRule { get; set; }

    /// <summary>Full VEVENT text — source of truth for round-tripping to/from the CalDAV server.</summary>
    public required string RawIcs { get; set; }

    public DateTimeOffset LastSyncedUtc { get; set; }
}
