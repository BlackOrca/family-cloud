using FamilyCloud.Core.CalDav.Models;

namespace FamilyCloud.Core.CalDav;

public interface ICalDavClient
{
    /// <summary>Discovers the calendar-home-set collection for the given server and account, via current-user-principal.</summary>
    Task<Uri> DiscoverCalendarHomeAsync(Uri serverUrl, CalDavCredentials credentials, CancellationToken ct = default);

    /// <summary>Lists the calendar collections directly under a calendar-home-set.</summary>
    Task<IReadOnlyList<CalDavCalendarInfo>> ListCalendarsAsync(Uri calendarHomeUrl, CalDavCredentials credentials, CancellationToken ct = default);

    /// <summary>Fetches VEVENT resources whose time range overlaps [start, end).</summary>
    Task<IReadOnlyList<CalDavEventResource>> QueryEventsAsync(Uri calendarUrl, DateTimeOffset start, DateTimeOffset end, CalDavCredentials credentials, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates one event resource. Pass <paramref name="ifMatchEtag"/> to update an existing
    /// resource (conditional on it being unchanged); omit it to create a new resource (fails if one already exists).
    /// Returns the resource's new ETag.
    /// </summary>
    Task<string> PutEventAsync(Uri calendarUrl, string uid, string icsContent, CalDavCredentials credentials, string? ifMatchEtag = null, CancellationToken ct = default);

    /// <summary>Deletes an event resource, conditional on it still matching <paramref name="etag"/>.</summary>
    Task DeleteEventAsync(Uri eventUrl, string etag, CalDavCredentials credentials, CancellationToken ct = default);

    /// <summary>Creates a new calendar collection at the given (client-chosen) URI, which must not already exist.</summary>
    Task CreateCalendarAsync(Uri calendarUrl, string displayName, string? colorHex, CalDavCredentials credentials, CancellationToken ct = default);

    /// <summary>Updates a calendar collection's display name and color.</summary>
    Task UpdateCalendarAsync(Uri calendarUrl, string displayName, string? colorHex, CalDavCredentials credentials, CancellationToken ct = default);

    /// <summary>Deletes a calendar collection and everything in it.</summary>
    Task DeleteCalendarAsync(Uri calendarUrl, CalDavCredentials credentials, CancellationToken ct = default);
}
