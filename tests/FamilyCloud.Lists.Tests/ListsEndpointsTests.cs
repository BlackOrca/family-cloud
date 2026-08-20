using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FamilyCloud.Contracts.Lists;

namespace FamilyCloud.Lists.Tests;

public class ListsEndpointsTests(FamilyCloudWebApplicationFactory factory) : IClassFixture<FamilyCloudWebApplicationFactory>
{
    [Fact]
    public async Task Creating_a_list_auto_grants_the_creator_write_access()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/lists", new CreateListRequest("Wocheneinkauf", "Shopping"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ItemListDto>();
        Assert.NotNull(created);
        Assert.Equal("Wocheneinkauf", created!.Name);
        Assert.Equal("Shopping", created.Kind);
        Assert.True(created.CanWrite);

        var listResponse = await client.GetAsync("/api/lists");
        var lists = await listResponse.Content.ReadFromJsonAsync<List<ItemListDto>>();
        Assert.Contains(lists!, l => l.Id == created.Id);
    }

    [Fact]
    public async Task Creating_a_list_with_an_unknown_kind_returns_400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/lists", new CreateListRequest("Whatever", "NotAKind"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Items_can_be_added_updated_and_deleted_and_come_back_in_position_order()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "Todo-Liste", "Todo");

        var firstItem = await AddItemAsync(client, list.Id, "Rasen mähen");
        var secondItem = await AddItemAsync(client, list.Id, "Müll rausbringen");

        var itemsResponse = await client.GetAsync($"/api/lists/{list.Id}/items");
        var items = await itemsResponse.Content.ReadFromJsonAsync<List<ListItemDto>>();
        Assert.Equal([firstItem.Id, secondItem.Id], items!.Select(i => i.Id));

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/lists/items/{firstItem.Id}", new ListItemWriteRequest("Rasen mähen", null, IsDone: true));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ListItemDto>();
        Assert.True(updated!.IsDone);

        var deleteResponse = await client.DeleteAsync($"/api/lists/items/{secondItem.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDeleteResponse = await client.GetAsync($"/api/lists/{list.Id}/items");
        var afterDelete = await afterDeleteResponse.Content.ReadFromJsonAsync<List<ListItemDto>>();
        Assert.Equal([firstItem.Id], afterDelete!.Select(i => i.Id));
    }

    [Fact]
    public async Task Deleting_a_list_removes_it_and_its_items()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "Wird gelöscht", "Todo");
        await AddItemAsync(client, list.Id, "Irrelevant");

        var deleteResponse = await client.DeleteAsync($"/api/lists/{list.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var itemsResponse = await client.GetAsync($"/api/lists/{list.Id}/items");
        Assert.Equal(HttpStatusCode.Forbidden, itemsResponse.StatusCode);
    }

    [Fact]
    public async Task A_family_member_without_a_permission_grant_cannot_see_or_write_someone_elses_list()
    {
        var (memberUserName, memberPassword) = await SeedExtraFamilyMemberAsync("outsider");
        var owner = await factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner, "Privat", "Todo");

        var outsider = await factory.CreateAuthenticatedClientAsync(memberUserName, memberPassword);

        var itemsResponse = await outsider.GetAsync($"/api/lists/{list.Id}/items");
        Assert.Equal(HttpStatusCode.Forbidden, itemsResponse.StatusCode);

        var addResponse = await outsider.PostAsJsonAsync(
            $"/api/lists/{list.Id}/items", new ListItemWriteRequest("Sneaky", null, IsDone: false));
        Assert.Equal(HttpStatusCode.Forbidden, addResponse.StatusCode);

        var listsResponse = await outsider.GetAsync("/api/lists");
        var outsiderLists = await listsResponse.Content.ReadFromJsonAsync<List<ItemListDto>>();
        Assert.DoesNotContain(outsiderLists!, l => l.Id == list.Id);
    }

    [Fact]
    public async Task Sharing_a_list_grants_the_target_family_member_access_and_revoking_removes_it_again()
    {
        var (memberUserName, memberPassword, memberUserId) = await SeedExtraFamilyMemberWithIdAsync("sharee");
        var owner = await factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner, "Gemeinsame Liste", "Shopping");

        var shareResponse = await owner.PutAsJsonAsync($"/api/lists/{list.Id}/share", new ShareListRequest(memberUserId, CanWrite: true));
        Assert.Equal(HttpStatusCode.NoContent, shareResponse.StatusCode);

        var sharee = await factory.CreateAuthenticatedClientAsync(memberUserName, memberPassword);
        var itemsResponse = await sharee.GetAsync($"/api/lists/{list.Id}/items");
        Assert.Equal(HttpStatusCode.OK, itemsResponse.StatusCode);

        var addResponse = await sharee.PostAsJsonAsync(
            $"/api/lists/{list.Id}/items", new ListItemWriteRequest("Milch", "2L", IsDone: false));
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        var sharesResponse = await owner.GetAsync($"/api/lists/{list.Id}/share");
        Assert.Equal(HttpStatusCode.OK, sharesResponse.StatusCode);
        var shares = await sharesResponse.Content.ReadFromJsonAsync<List<ListShareDto>>();
        Assert.Contains(shares!, s => s.UserId == memberUserId && s.CanWrite);

        var revokeResponse = await owner.DeleteAsync($"/api/lists/{list.Id}/share/{memberUserId}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var afterRevokeResponse = await sharee.GetAsync($"/api/lists/{list.Id}/items");
        Assert.Equal(HttpStatusCode.Forbidden, afterRevokeResponse.StatusCode);

        var sharesAfterRevoke = await (await owner.GetAsync($"/api/lists/{list.Id}/share")).Content.ReadFromJsonAsync<List<ListShareDto>>();
        Assert.DoesNotContain(sharesAfterRevoke!, s => s.UserId == memberUserId);
    }

    [Fact]
    public async Task Sharing_with_a_user_outside_the_family_returns_400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner, "Liste", "Todo");

        var response = await owner.PutAsJsonAsync($"/api/lists/{list.Id}/share", new ShareListRequest(Guid.NewGuid(), CanWrite: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lists_endpoints_without_a_token_return_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lists");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<ItemListDto> CreateListAsync(HttpClient client, string name, string kind)
    {
        var response = await client.PostAsJsonAsync("/api/lists", new CreateListRequest(name, kind));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ItemListDto>())!;
    }

    private static async Task<ListItemDto> AddItemAsync(HttpClient client, Guid listId, string text)
    {
        var response = await client.PostAsJsonAsync($"/api/lists/{listId}/items", new ListItemWriteRequest(text, null, IsDone: false));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ListItemDto>())!;
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
