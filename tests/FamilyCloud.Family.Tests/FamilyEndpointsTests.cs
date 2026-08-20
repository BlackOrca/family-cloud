using System.Net;
using System.Net.Http.Json;
using FamilyCloud.Contracts.Families;

namespace FamilyCloud.Family.Tests;

public class FamilyEndpointsTests(FamilyCloudWebApplicationFactory factory) : IClassFixture<FamilyCloudWebApplicationFactory>
{
    [Fact]
    public async Task GetMembers_returns_the_seeded_admin_as_an_admin_member()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/family/members");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var members = await response.Content.ReadFromJsonAsync<List<FamilyMemberDto>>();
        Assert.NotNull(members);
        var admin = Assert.Single(members!);
        Assert.Equal("Admin", admin.DisplayName);
        Assert.Equal("Admin", admin.Role);
    }

    [Fact]
    public async Task GetMembers_without_a_token_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/family/members");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
