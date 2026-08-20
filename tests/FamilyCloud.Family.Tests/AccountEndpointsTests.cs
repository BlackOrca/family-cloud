using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FamilyCloud.Contracts.Account;
using FamilyCloud.Contracts.Auth;

namespace FamilyCloud.Family.Tests;

public class AccountEndpointsTests(FamilyCloudWebApplicationFactory factory) : IClassFixture<FamilyCloudWebApplicationFactory>
{
    [Fact]
    public async Task GetAccount_returns_the_calling_users_own_profile()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/account");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<AccountProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal(factory.SeedAdminUserName, profile!.UserName);
        Assert.Equal("Admin", profile.DisplayName);
        Assert.Equal("Admin", profile.Role);
    }

    [Fact]
    public async Task GetAccount_without_a_token_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/account");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_persists_the_new_display_name_and_email()
    {
        // Isolated seed user so this test's profile edit doesn't affect other tests sharing the fixture
        // (e.g. GetAccount_returns_the_calling_users_own_profile asserting on the seed admin's DisplayName).
        var userName = "profile-update-user";
        var password = "Sup3r-Secret-Profile-Test!";
        await SeedExtraFamilyMemberAsync(userName, password);

        var client = await factory.CreateAuthenticatedClientAsync(userName, password);

        var putResponse = await client.PutAsJsonAsync(
            "/api/account/profile", new UpdateProfileRequest("Renamed Member", "member@example.test"));
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/account");
        var profile = await getResponse.Content.ReadFromJsonAsync<AccountProfileDto>();
        Assert.Equal("Renamed Member", profile!.DisplayName);
        Assert.Equal("member@example.test", profile.Email);
    }

    [Fact]
    public async Task ChangePassword_with_the_wrong_current_password_returns_400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/account/password", new ChangePasswordRequest("wrong-current-password", "New-Sup3r-Secret!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_with_the_correct_current_password_lets_the_user_log_in_with_the_new_one()
    {
        // Isolated seed user so this test's password change doesn't affect other tests sharing the fixture.
        var userName = "password-change-user";
        var originalPassword = "Original-Sup3r-Secret!";
        var newPassword = "New-Sup3r-Secret!";
        await SeedExtraFamilyMemberAsync(userName, originalPassword);

        var client = await factory.CreateAuthenticatedClientAsync(userName, originalPassword);
        var changeResponse = await client.PutAsJsonAsync(
            "/api/account/password", new ChangePasswordRequest(originalPassword, newPassword));
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        var loginClient = factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(userName, newPassword));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    private async Task SeedExtraFamilyMemberAsync(string userName, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<FamilyCloud.Core.Data.AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<FamilyCloud.Server.Data.FamilyCloudDbContext>();

        var user = new FamilyCloud.Core.Data.AppUser
        {
            UserName = userName,
            DisplayName = "Extra Member",
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));

        var family = await db.Families.FirstAsync();
        db.FamilyMembers.Add(new FamilyCloud.Family.Domain.FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            UserId = user.Id,
            Role = FamilyCloud.Family.Domain.FamilyRole.Member,
            JoinedUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
