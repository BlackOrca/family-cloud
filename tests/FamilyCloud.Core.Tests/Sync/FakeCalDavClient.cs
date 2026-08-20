using FamilyCloud.Core.CalDav;
using FamilyCloud.Core.CalDav.Models;

namespace FamilyCloud.Core.Tests.Sync;

/// <summary>Hand-rolled test double for <see cref="ICalDavClient"/> — each method is backed by an
/// optional delegate a test sets up; calling an unset one fails loudly instead of silently no-oping.</summary>
internal sealed class FakeCalDavClient : ICalDavClient
{
    public Func<Uri, CalDavCredentials, CancellationToken, Task<Uri>>? DiscoverCalendarHome { get; set; }

    public Func<Uri, CalDavCredentials, CancellationToken, Task<IReadOnlyList<CalDavCalendarInfo>>>? ListCalendars { get; set; }

    public Func<Uri, DateTimeOffset, DateTimeOffset, CalDavCredentials, CancellationToken, Task<IReadOnlyList<CalDavEventResource>>>? QueryEvents { get; set; }

    public Func<Uri, string, string, CalDavCredentials, string?, CancellationToken, Task<string>>? PutEvent { get; set; }

    public Func<Uri, string, CalDavCredentials, CancellationToken, Task>? DeleteEvent { get; set; }

    public Func<Uri, string, string?, CalDavCredentials, CancellationToken, Task>? CreateCalendar { get; set; }

    public Func<Uri, string, string?, CalDavCredentials, CancellationToken, Task>? UpdateCalendar { get; set; }

    public Func<Uri, CalDavCredentials, CancellationToken, Task>? DeleteCalendar { get; set; }

    public Task<Uri> DiscoverCalendarHomeAsync(Uri serverUrl, CalDavCredentials credentials, CancellationToken ct = default) =>
        (DiscoverCalendarHome ?? throw new NotImplementedException($"{nameof(DiscoverCalendarHome)} was not set up."))(serverUrl, credentials, ct);

    public Task<IReadOnlyList<CalDavCalendarInfo>> ListCalendarsAsync(Uri calendarHomeUrl, CalDavCredentials credentials, CancellationToken ct = default) =>
        (ListCalendars ?? throw new NotImplementedException($"{nameof(ListCalendars)} was not set up."))(calendarHomeUrl, credentials, ct);

    public Task<IReadOnlyList<CalDavEventResource>> QueryEventsAsync(Uri calendarUrl, DateTimeOffset start, DateTimeOffset end, CalDavCredentials credentials, CancellationToken ct = default) =>
        (QueryEvents ?? throw new NotImplementedException($"{nameof(QueryEvents)} was not set up."))(calendarUrl, start, end, credentials, ct);

    public Task<string> PutEventAsync(Uri calendarUrl, string uid, string icsContent, CalDavCredentials credentials, string? ifMatchEtag = null, CancellationToken ct = default) =>
        (PutEvent ?? throw new NotImplementedException($"{nameof(PutEvent)} was not set up."))(calendarUrl, uid, icsContent, credentials, ifMatchEtag, ct);

    public Task DeleteEventAsync(Uri eventUrl, string etag, CalDavCredentials credentials, CancellationToken ct = default) =>
        (DeleteEvent ?? throw new NotImplementedException($"{nameof(DeleteEvent)} was not set up."))(eventUrl, etag, credentials, ct);

    public Task CreateCalendarAsync(Uri calendarUrl, string displayName, string? colorHex, CalDavCredentials credentials, CancellationToken ct = default) =>
        (CreateCalendar ?? throw new NotImplementedException($"{nameof(CreateCalendar)} was not set up."))(calendarUrl, displayName, colorHex, credentials, ct);

    public Task UpdateCalendarAsync(Uri calendarUrl, string displayName, string? colorHex, CalDavCredentials credentials, CancellationToken ct = default) =>
        (UpdateCalendar ?? throw new NotImplementedException($"{nameof(UpdateCalendar)} was not set up."))(calendarUrl, displayName, colorHex, credentials, ct);

    public Task DeleteCalendarAsync(Uri calendarUrl, CalDavCredentials credentials, CancellationToken ct = default) =>
        (DeleteCalendar ?? throw new NotImplementedException($"{nameof(DeleteCalendar)} was not set up."))(calendarUrl, credentials, ct);
}
