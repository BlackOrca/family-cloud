using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FamilyCloud.Contracts.Photos;

namespace FamilyCloud.Photos.Tests;

public class PhotosEndpointsTests(FamilyCloudWebApplicationFactory factory) : IClassFixture<FamilyCloudWebApplicationFactory>
{
    [Fact]
    public async Task Creating_an_album_auto_grants_the_creator_write_access()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/photos/albums", new CreatePhotoAlbumRequest("Urlaub 2026"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PhotoAlbumDto>();
        Assert.NotNull(created);
        Assert.Equal("Urlaub 2026", created!.Name);
        Assert.True(created.CanWrite);

        var listResponse = await client.GetAsync("/api/photos/albums");
        var albums = await listResponse.Content.ReadFromJsonAsync<List<PhotoAlbumDto>>();
        Assert.Contains(albums!, a => a.Id == created.Id);
    }

    [Fact]
    public async Task Uploading_an_asset_makes_it_show_up_in_the_album_and_deleting_it_removes_it_again()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var album = await CreateAlbumAsync(client, "Fotos");

        var uploaded = await UploadAssetAsync(client, album.Id, "strand.jpg");

        var assetsResponse = await client.GetAsync($"/api/photos/albums/{album.Id}/assets");
        var assets = await assetsResponse.Content.ReadFromJsonAsync<List<PhotoAssetDto>>();
        Assert.Contains(assets!, a => a.Id == uploaded.Id);

        var deleteResponse = await client.DeleteAsync($"/api/photos/albums/{album.Id}/assets/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDeleteResponse = await client.GetAsync($"/api/photos/albums/{album.Id}/assets");
        var afterDelete = await afterDeleteResponse.Content.ReadFromJsonAsync<List<PhotoAssetDto>>();
        Assert.DoesNotContain(afterDelete!, a => a.Id == uploaded.Id);
    }

    [Fact]
    public async Task Deleting_an_album_removes_it()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var album = await CreateAlbumAsync(client, "Wird gelöscht");

        var deleteResponse = await client.DeleteAsync($"/api/photos/albums/{album.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/photos/albums");
        var albums = await listResponse.Content.ReadFromJsonAsync<List<PhotoAlbumDto>>();
        Assert.DoesNotContain(albums!, a => a.Id == album.Id);
    }

    [Fact]
    public async Task A_family_member_without_a_permission_grant_cannot_see_or_write_someone_elses_album()
    {
        var (memberUserName, memberPassword) = await SeedExtraFamilyMemberAsync("outsider");
        var owner = await factory.CreateAuthenticatedClientAsync();
        var album = await CreateAlbumAsync(owner, "Privat");

        var outsider = await factory.CreateAuthenticatedClientAsync(memberUserName, memberPassword);

        var assetsResponse = await outsider.GetAsync($"/api/photos/albums/{album.Id}/assets");
        Assert.Equal(HttpStatusCode.Forbidden, assetsResponse.StatusCode);

        var albumsResponse = await outsider.GetAsync("/api/photos/albums");
        var outsiderAlbums = await albumsResponse.Content.ReadFromJsonAsync<List<PhotoAlbumDto>>();
        Assert.DoesNotContain(outsiderAlbums!, a => a.Id == album.Id);
    }

    [Fact]
    public async Task Sharing_an_album_grants_the_target_family_member_access_and_revoking_removes_it_again()
    {
        var (memberUserName, memberPassword, memberUserId) = await SeedExtraFamilyMemberWithIdAsync("sharee");
        var owner = await factory.CreateAuthenticatedClientAsync();
        var album = await CreateAlbumAsync(owner, "Gemeinsames Album");

        var shareResponse = await owner.PutAsJsonAsync($"/api/photos/albums/{album.Id}/share", new SharePhotoAlbumRequest(memberUserId, CanWrite: true));
        Assert.Equal(HttpStatusCode.NoContent, shareResponse.StatusCode);

        var sharee = await factory.CreateAuthenticatedClientAsync(memberUserName, memberPassword);
        var assetsResponse = await sharee.GetAsync($"/api/photos/albums/{album.Id}/assets");
        Assert.Equal(HttpStatusCode.OK, assetsResponse.StatusCode);

        var sharesResponse = await owner.GetAsync($"/api/photos/albums/{album.Id}/share");
        var shares = await sharesResponse.Content.ReadFromJsonAsync<List<PhotoAlbumShareDto>>();
        Assert.Contains(shares!, s => s.UserId == memberUserId && s.CanWrite);

        var revokeResponse = await owner.DeleteAsync($"/api/photos/albums/{album.Id}/share/{memberUserId}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var afterRevokeResponse = await sharee.GetAsync($"/api/photos/albums/{album.Id}/assets");
        Assert.Equal(HttpStatusCode.Forbidden, afterRevokeResponse.StatusCode);
    }

    [Fact]
    public async Task Sharing_with_a_user_outside_the_family_returns_400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var album = await CreateAlbumAsync(owner, "Album");

        var response = await owner.PutAsJsonAsync($"/api/photos/albums/{album.Id}/share", new SharePhotoAlbumRequest(Guid.NewGuid(), CanWrite: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Photos_endpoints_without_a_token_return_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/photos/albums");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<PhotoAlbumDto> CreateAlbumAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/photos/albums", new CreatePhotoAlbumRequest(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PhotoAlbumDto>())!;
    }

    private static async Task<PhotoAssetDto> UploadAssetAsync(HttpClient client, Guid albumId, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xD9]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", fileName);

        var response = await client.PostAsync($"/api/photos/albums/{albumId}/assets", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PhotoAssetDto>())!;
    }

    private async Task<(string UserName, string Password)> SeedExtraFamilyMemberAsync(string userNamePrefix)
    {
        var (userName, password, _) = await SeedExtraFamilyMemberWithIdAsync(userNamePrefix);
        return (userName, password);
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
