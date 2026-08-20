using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FamilyCloud.Contracts.Auth;
using FamilyCloud.Contracts.Calendars;
using FamilyCloud.Calendar.Domain;
using FamilyCloud.Server.Data;
// Aliased: FamilyCloud.Calendar.Domain.Calendar's bare name "Calendar" collides with the sibling
// FamilyCloud.Calendar project namespace once nested under FamilyCloud (see FamilyCloudDbContext.cs).
using CalendarEntity = FamilyCloud.Calendar.Domain.Calendar;

namespace FamilyCloud.Server.Tests;

public class CalendarsEndpointsTests(FamilyCloudWebApplicationFactory factory) : IClassFixture<FamilyCloudWebApplicationFactory>
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
    public async Task GetCalendars_without_a_token_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/calendars");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendars_returns_only_calendars_the_caller_has_a_permission_for()
    {
        var client = factory.CreateClient();
        var token = await LoginAsync(client);

        Guid calendarId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FamilyCloudDbContext>();
            var user = await db.Users.SingleAsync(u => u.UserName == factory.SeedAdminUserName);

            // The managed Radicale account and the seeded family (see Program.cs) — reused here
            // rather than creating second unrelated rows, since only their ids actually matter.
            var account = await db.CalendarAccounts.FirstAsync();
            var familyId = (await db.Families.FirstAsync()).Id;

            var visibleCalendar = new CalendarEntity
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId,
                CalendarAccountId = account.Id,
                CalDavHref = "/testuser/visible/",
                DisplayName = "Sichtbar",
            };
            var hiddenCalendar = new CalendarEntity
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId,
                CalendarAccountId = account.Id,
                CalDavHref = "/testuser/hidden/",
                DisplayName = "Ohne Berechtigung",
            };
            db.Calendars.AddRange(visibleCalendar, hiddenCalendar);
            db.CalendarPermissions.Add(new CalendarPermission
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CalendarId = visibleCalendar.Id,
                CanWrite = true,
                GrantedUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            calendarId = visibleCalendar.Id;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/calendars");

        response.EnsureSuccessStatusCode();
        var calendars = await response.Content.ReadFromJsonAsync<List<CalendarDto>>();
        var dto = Assert.Single(calendars!);
        Assert.Equal(calendarId, dto.Id);
        Assert.Equal("Sichtbar", dto.DisplayName);
        Assert.True(dto.CanWrite);
    }

    [Fact]
    public async Task GetEvents_for_a_calendar_without_permission_returns_403()
    {
        var client = factory.CreateClient();
        var token = await LoginAsync(client);

        Guid calendarId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FamilyCloudDbContext>();
            var account = await db.CalendarAccounts.FirstAsync();
            var familyId = (await db.Families.FirstAsync()).Id;
            var calendar = new CalendarEntity
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId,
                CalendarAccountId = account.Id,
                CalDavHref = "/testuser/no-permission/",
                DisplayName = "Ohne Berechtigung",
            };
            db.Calendars.Add(calendar);
            await db.SaveChangesAsync();
            calendarId = calendar.Id;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync($"/api/calendars/{calendarId}/events");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
