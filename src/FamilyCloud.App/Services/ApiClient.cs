using System.Net;
using System.Net.Http.Json;
using FamilyCloud.Contracts.Account;
using FamilyCloud.Contracts.Auth;
using FamilyCloud.Contracts.Calendars;
using FamilyCloud.Contracts.Settings;
using FamilyCloud.Contracts.Sync;

namespace FamilyCloud.App.Services;

internal sealed class ApiClient(HttpClient http)
{
    /// <summary>Redirects this client's already-created <see cref="HttpClient"/> immediately, so a server
    /// address just entered on the setup screen takes effect without waiting for the app to restart.</summary>
    public void SetBaseAddress(Uri baseAddress) => http.BaseAddress = baseAddress;

    public async Task<AppSettingsDto?> GetSettingsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<AppSettingsDto>("api/settings", ct);

    public async Task<SyncChangesResponse?> GetSyncChangesAsync(long? since, CancellationToken ct = default)
    {
        var query = since is { } cursor ? $"api/sync/changes?since={cursor}" : "api/sync/changes";
        return await http.GetFromJsonAsync<SyncChangesResponse>(query, ct);
    }

    public async Task<LoginResponse?> LoginAsync(string userName, string password, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(userName, password), ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<LoginResponse>(ct);
    }

    public async Task<AccountProfileDto?> GetAccountProfileAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<AccountProfileDto>("api/account", ct);

    public async Task<bool> UpdateProfileAsync(string displayName, string? email, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync("api/account/profile", new UpdateProfileRequest(displayName, email), ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync("api/account/password", new ChangePasswordRequest(currentPassword, newPassword), ct);
        return response.IsSuccessStatusCode;
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

    public async Task<EventWriteResult> CreateEventAsync(Guid calendarId, EventWriteRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"api/calendars/{calendarId}/events", request, ct);
        return await ToWriteResultAsync(response, ct);
    }

    public async Task<EventWriteResult> UpdateEventAsync(Guid eventId, EventWriteRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"api/events/{eventId}", request, ct);
        return await ToWriteResultAsync(response, ct);
    }

    public async Task<EventWriteResult> DeleteEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"api/events/{eventId}", ct);
        return response.StatusCode == HttpStatusCode.NoContent
            ? new EventWriteResult(EventWriteOutcome.Success)
            : await ToWriteResultAsync(response, ct);
    }

    private static async Task<EventWriteResult> ToWriteResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<EventDto>(ct);
            return new EventWriteResult(EventWriteOutcome.Success, dto);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Forbidden => new EventWriteResult(EventWriteOutcome.Forbidden),
            HttpStatusCode.NotFound => new EventWriteResult(EventWriteOutcome.NotFound),
            HttpStatusCode.Conflict => new EventWriteResult(EventWriteOutcome.Conflict),
            _ => new EventWriteResult(EventWriteOutcome.Error, ErrorMessage: await response.Content.ReadAsStringAsync(ct)),
        };
    }
}
