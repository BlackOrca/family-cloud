using System.Net;
using System.Net.Http.Json;
using FamilyCloud.Contracts.Auth;

namespace FamilyCloud.Server.Tests;

public class AuthEndpointsTests(FamilyCloudWebApplicationFactory factory) : IClassFixture<FamilyCloudWebApplicationFactory>
{
    [Fact]
    public async Task Login_with_the_seeded_admin_credentials_returns_a_token()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(factory.SeedAdminUserName, factory.SeedAdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Admin", body.DisplayName);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(factory.SeedAdminUserName, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_an_unknown_user_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("no-such-user", "irrelevant"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
