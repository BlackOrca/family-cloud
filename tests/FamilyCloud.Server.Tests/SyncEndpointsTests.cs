using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using FamilyCloud.Contracts.Auth;
using FamilyCloud.Contracts.Sync;
using FamilyCloud.Core.Domain;
using FamilyCloud.Server.Data;

namespace FamilyCloud.Server.Tests;

public class SyncEndpointsTests(FamilyCloudWebApplicationFactory factory) : IClassFixture<FamilyCloudWebApplicationFactory>
{
    private async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(factory.SeedAdminUserName, factory.SeedAdminPassword));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    [Fact]
    public async Task GetChanges_without_a_token_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/sync/changes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetChanges_bootstrap_call_returns_no_changes_but_a_cursor()
    {
        var client = factory.CreateClient();
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/sync/changes");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SyncChangesResponse>();
        Assert.NotNull(body);
        Assert.Empty(body!.Changes);
        Assert.False(body.FullResyncRequired);
    }

    [Fact]
    public async Task GetChanges_reports_a_change_recorded_after_the_callers_cursor()
    {
        var client = factory.CreateClient();
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var bootstrap = await client.GetFromJsonAsync<SyncChangesResponse>("/api/sync/changes");
        var cursor = bootstrap!.Cursor;

        // Simulates what any write path (Settings save, CalendarWriteService, the calendar polling
        // job) does via SyncEventPublisher — recording a change without going through a live CalDAV
        // server, which this test host doesn't have.
        Guid calendarId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FamilyCloudDbContext>();
            db.SyncEvents.Add(new SyncEvent
            {
                ResourceType = SyncResourceType.Calendar,
                ResourceId = calendarId.ToString(),
                ChangedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var changesResponse = await client.GetAsync($"/api/sync/changes?since={cursor}");
        changesResponse.EnsureSuccessStatusCode();
        var body = await changesResponse.Content.ReadFromJsonAsync<SyncChangesResponse>();

        var change = Assert.Single(body!.Changes);
        Assert.Equal(SyncResourceType.Calendar, change.ResourceType);
        Assert.Equal(calendarId.ToString(), change.ResourceId);
        Assert.True(body.Cursor > cursor);

        // A repeat call with the new cursor sees nothing further.
        var followUp = await client.GetFromJsonAsync<SyncChangesResponse>($"/api/sync/changes?since={body.Cursor}");
        Assert.Empty(followUp!.Changes);
    }
}
