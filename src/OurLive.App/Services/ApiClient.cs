using System.Net.Http.Json;
using OurLive.Contracts.Auth;
using OurLive.Contracts.Calendars;

namespace OurLive.App.Services;

internal sealed class ApiClient(HttpClient http)
{
    public async Task<LoginResponse?> LoginAsync(string userName, string password, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(userName, password), ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<LoginResponse>(ct);
    }

    public async Task<List<CalendarDto>> GetCalendarsAsync(CancellationToken ct = default)
    {
        var calendars = await http.GetFromJsonAsync<List<CalendarDto>>("api/calendars", ct);
        return calendars ?? [];
    }

    public async Task<List<EventDto>> GetEventsAsync(Guid calendarId, DateTimeOffset? start = null, DateTimeOffset? end = null, CancellationToken ct = default)
    {
        var query = $"api/calendars/{calendarId}/events";
        var parameters = new List<string>();
        if (start is { } s)
        {
            parameters.Add($"start={Uri.EscapeDataString(s.ToString("O"))}");
        }
        if (end is { } e)
        {
            parameters.Add($"end={Uri.EscapeDataString(e.ToString("O"))}");
        }
        if (parameters.Count > 0)
        {
            query += "?" + string.Join("&", parameters);
        }

        var events = await http.GetFromJsonAsync<List<EventDto>>(query, ct);
        return events ?? [];
    }
}
