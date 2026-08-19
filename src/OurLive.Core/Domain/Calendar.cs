namespace OurLive.Core.Domain;

/// <summary>One calendar collection discovered within a <see cref="CalendarAccount"/>.</summary>
public class Calendar
{
    public Guid Id { get; set; }

    public Guid CalendarAccountId { get; set; }

    public CalendarAccount? CalendarAccount { get; set; }

    /// <summary>CalDAV collection path as returned by PROPFIND, e.g. /calendars/family/household/.</summary>
    public required string CalDavHref { get; set; }

    public required string DisplayName { get; set; }

    public string? ColorHex { get; set; }

    /// <summary>Collection sync token (getctag) from the last REPORT query, used to skip unchanged syncs.</summary>
    public string? CTag { get; set; }

    public DateTimeOffset? LastSyncedUtc { get; set; }

    public List<CalendarPermission> Permissions { get; set; } = [];

    public List<CachedEvent> Events { get; set; } = [];
}
