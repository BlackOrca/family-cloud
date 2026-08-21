using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FamilyCloud.Contracts.Storage;
using FamilyCloud.Storage.OpenCloud;

namespace FamilyCloud.Storage.Tests;

public class StorageEndpointsTests(FamilyCloudWebApplicationFactory factory) : IClassFixture<FamilyCloudWebApplicationFactory>
{
    [Fact]
    public async Task Config_endpoint_returns_the_configured_base_urls()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/storage/config");

        response.EnsureSuccessStatusCode();
        var config = await response.Content.ReadFromJsonAsync<StorageConfigDto>();
        Assert.NotNull(config);
        Assert.Equal("https://opencloud.invalid/", config!.WebDavBaseUrl);
        Assert.Equal("https://opencloud.invalid/", config.GraphBaseUrl);
    }

    [Fact]
    public async Task Creating_a_root_without_a_provisioned_OpenCloud_account_returns_400()
    {
        var (userName, password, _) = await SeedExtraFamilyMemberWithIdAsync("no-account");
        var client = await factory.CreateAuthenticatedClientAsync(userName, password);

        var response = await client.PostAsJsonAsync("/api/storage/roots", new CreateStorageRootRequest("Dokumente"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_root_auto_grants_the_creator_manager_access()
    {
        var (userName, password, userId) = await SeedExtraFamilyMemberWithIdAsync("root-owner");
        await ProvisionAsync(userId, userName, password);
        var client = await factory.CreateAuthenticatedClientAsync(userName, password);

        var response = await client.PostAsJsonAsync("/api/storage/roots", new CreateStorageRootRequest("Dokumente"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<StorageRootDto>();
        Assert.NotNull(created);
        Assert.Equal("Dokumente", created!.Name);

        var sharesResponse = await client.GetAsync($"/api/storage/roots/{created.DriveId}/share");
        sharesResponse.EnsureSuccessStatusCode();
        var shares = await sharesResponse.Content.ReadFromJsonAsync<List<StorageRootShareDto>>();
        Assert.Contains(shares!, s => s.UserId == userId && s.CanWrite);
    }

    [Fact]
    public async Task Sharing_a_root_grants_the_target_family_member_access_and_revoking_removes_it_again()
    {
        var (ownerUserName, ownerPassword, ownerUserId) = await SeedExtraFamilyMemberWithIdAsync("share-owner");
        await ProvisionAsync(ownerUserId, ownerUserName, ownerPassword);
        var owner = await factory.CreateAuthenticatedClientAsync(ownerUserName, ownerPassword);
        var root = await CreateRootAsync(owner, "Gemeinsam");

        var (memberUserName, memberPassword, memberUserId) = await SeedExtraFamilyMemberWithIdAsync("share-sharee");
        await ProvisionAsync(memberUserId, memberUserName, memberPassword);

        var shareResponse = await owner.PutAsJsonAsync(
            $"/api/storage/roots/{root.DriveId}/share", new ShareStorageRootRequest(memberUserId, CanWrite: true));
        Assert.Equal(HttpStatusCode.NoContent, shareResponse.StatusCode);

        var sharesResponse = await owner.GetAsync($"/api/storage/roots/{root.DriveId}/share");
        var shares = await sharesResponse.Content.ReadFromJsonAsync<List<StorageRootShareDto>>();
        Assert.Contains(shares!, s => s.UserId == memberUserId && s.CanWrite);

        var revokeResponse = await owner.DeleteAsync($"/api/storage/roots/{root.DriveId}/share/{memberUserId}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var afterRevokeResponse = await owner.GetAsync($"/api/storage/roots/{root.DriveId}/share");
        var afterRevoke = await afterRevokeResponse.Content.ReadFromJsonAsync<List<StorageRootShareDto>>();
        Assert.DoesNotContain(afterRevoke!, s => s.UserId == memberUserId);
    }

    [Fact]
    public async Task Sharing_with_a_user_outside_the_family_returns_400()
    {
        var (ownerUserName, ownerPassword, ownerUserId) = await SeedExtraFamilyMemberWithIdAsync("outside-owner");
        await ProvisionAsync(ownerUserId, ownerUserName, ownerPassword);
        var owner = await factory.CreateAuthenticatedClientAsync(ownerUserName, ownerPassword);
        var root = await CreateRootAsync(owner, "Privat");

        var response = await owner.PutAsJsonAsync(
            $"/api/storage/roots/{root.DriveId}/share", new ShareStorageRootRequest(Guid.NewGuid(), CanWrite: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sharing_without_manager_access_on_the_root_is_forbidden()
    {
        var (ownerUserName, ownerPassword, ownerUserId) = await SeedExtraFamilyMemberWithIdAsync("real-owner");
        await ProvisionAsync(ownerUserId, ownerUserName, ownerPassword);
        var owner = await factory.CreateAuthenticatedClientAsync(ownerUserName, ownerPassword);
        var root = await CreateRootAsync(owner, "Nur der Owner");

        // Grant Viewer (not Manager) access, so the caller below is authenticated and does have *some*
        // relationship to the root — but still shouldn't be allowed to grant/revoke someone else's access.
        var (viewerUserName, viewerPassword, viewerUserId) = await SeedExtraFamilyMemberWithIdAsync("just-viewer");
        await ProvisionAsync(viewerUserId, viewerUserName, viewerPassword);
        var grantResponse = await owner.PutAsJsonAsync(
            $"/api/storage/roots/{root.DriveId}/share", new ShareStorageRootRequest(viewerUserId, CanWrite: false));
        Assert.Equal(HttpStatusCode.NoContent, grantResponse.StatusCode);

        var (thirdUserName, thirdPassword, thirdUserId) = await SeedExtraFamilyMemberWithIdAsync("third-party");
        await ProvisionAsync(thirdUserId, thirdUserName, thirdPassword);

        var viewer = await factory.CreateAuthenticatedClientAsync(viewerUserName, viewerPassword);
        var response = await viewer.PutAsJsonAsync(
            $"/api/storage/roots/{root.DriveId}/share", new ShareStorageRootRequest(thirdUserId, CanWrite: true));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Storage_endpoints_without_a_token_return_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/storage/config");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<StorageRootDto> CreateRootAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/storage/roots", new CreateStorageRootRequest(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StorageRootDto>())!;
    }

    /// <summary>Provisions an OpenCloudAccount directly through a DI scope — there's no HTTP endpoint for
    /// this (it normally happens as a side effect of admin user-creation or a self-service password
    /// change, both Blazor pages that WebApplicationFactory's HttpClient can't drive), mirroring how
    /// SeedExtraFamilyMemberWithIdAsync below creates FamilyMember rows directly rather than through UI.</summary>
    private async Task ProvisionAsync(Guid userId, string userName, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<IOpenCloudProvisioner>();
        var db = scope.ServiceProvider.GetRequiredService<FamilyCloud.Server.Data.FamilyCloudDbContext>();
        await provisioner.ProvisionUserAsync(userId, userName, email: null, password);
        await db.SaveChangesAsync();
    }

    private async Task<(string UserName, string Password, Guid UserId)> SeedExtraFamilyMemberWithIdAsync(string userNamePrefix)
    {
        var userName = $"{userNamePrefix}-{Guid.NewGuid():N}";
        var password = "Sup3r-Secret-Member-Test!";

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<FamilyCloud.Core.Data.AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<FamilyCloud.Server.Data.FamilyCloudDbContext>();

        var user = new FamilyCloud.Core.Data.AppUser
        {
            UserName = userName,
            DisplayName = userNamePrefix,
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

        return (userName, password, user.Id);
    }
}
