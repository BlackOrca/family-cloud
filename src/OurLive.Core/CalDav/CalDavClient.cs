using System.Net;
using System.Net.Http.Headers;
using System.Text;
using OurLive.Core.CalDav.Models;

namespace OurLive.Core.CalDav;

public class CalDavClient(HttpClient httpClient) : ICalDavClient
{
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");
    private static readonly HttpMethod ReportMethod = new("REPORT");

    public async Task<Uri> DiscoverCalendarHomeAsync(Uri serverUrl, CalDavCredentials credentials, CancellationToken ct = default)
    {
        var principalXml = await SendXmlAsync(PropFindMethod, serverUrl, CalDavXmlRequests.CurrentUserPrincipal(), depth: 0, credentials, ct);
        var principalUri = CalDavXmlParser.ParseCurrentUserPrincipal(principalXml, serverUrl)
            ?? throw new CalDavException($"Server did not return a current-user-principal for '{serverUrl}'.");

        var homeSetXml = await SendXmlAsync(PropFindMethod, principalUri, CalDavXmlRequests.CalendarHomeSet(), depth: 0, credentials, ct);
        return CalDavXmlParser.ParseCalendarHomeSet(homeSetXml, principalUri)
            ?? throw new CalDavException($"Server did not return a calendar-home-set for '{principalUri}'.");
    }

    public async Task<IReadOnlyList<CalDavCalendarInfo>> ListCalendarsAsync(Uri calendarHomeUrl, CalDavCredentials credentials, CancellationToken ct = default)
    {
        var xml = await SendXmlAsync(PropFindMethod, calendarHomeUrl, CalDavXmlRequests.ListCalendars(), depth: 1, credentials, ct);
        return CalDavXmlParser.ParseCalendarList(xml, calendarHomeUrl);
    }

    public async Task<IReadOnlyList<CalDavEventResource>> QueryEventsAsync(Uri calendarUrl, DateTimeOffset start, DateTimeOffset end, CalDavCredentials credentials, CancellationToken ct = default)
    {
        var xml = await SendXmlAsync(ReportMethod, calendarUrl, CalDavXmlRequests.CalendarQuery(start, end), depth: 1, credentials, ct);
        return CalDavXmlParser.ParseEventResources(xml, calendarUrl);
    }

    public async Task<string> PutEventAsync(Uri calendarUrl, string uid, string icsContent, CalDavCredentials credentials, string? ifMatchEtag = null, CancellationToken ct = default)
    {
        var eventUri = new Uri(calendarUrl, $"{uid}.ics");
        using var request = new HttpRequestMessage(HttpMethod.Put, eventUri)
        {
            Content = new StringContent(icsContent, Encoding.UTF8, "text/calendar"),
        };
        SetAuth(request, credentials);
        if (ifMatchEtag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatchEtag);
        }
        else
        {
            // Fail instead of silently overwriting if a resource with this UID already exists.
            request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        }

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new CalDavException($"PUT {eventUri} failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}", response.StatusCode);
        }

        return response.Headers.ETag?.Tag
            ?? throw new CalDavException($"PUT {eventUri} succeeded but the server did not return an ETag.");
    }

    public async Task DeleteEventAsync(Uri eventUrl, string etag, CalDavCredentials credentials, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, eventUrl);
        SetAuth(request, credentials);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new CalDavException($"DELETE {eventUrl} failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}", response.StatusCode);
        }
    }

    private async Task<string> SendXmlAsync(HttpMethod method, Uri uri, string xmlBody, int depth, CalDavCredentials credentials, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = new StringContent(xmlBody, Encoding.UTF8, "application/xml"),
        };
        SetAuth(request, credentials);
        request.Headers.TryAddWithoutValidation("Depth", depth.ToString());

        using var response = await httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new CalDavException($"{method} {uri} failed with {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}", response.StatusCode);
        }

        return responseBody;
    }

    private static void SetAuth(HttpRequestMessage request, CalDavCredentials credentials)
    {
        var raw = $"{credentials.Username}:{credentials.Password}";
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
    }
}
