using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using FamilyCloud.Photos.Domain;
using FamilyCloud.Photos.Security;

namespace FamilyCloud.Photos.Immich;

public class ImmichClient(HttpClient http, DbContext db, IImmichCredentialProtector protector) : IImmichClient
{
    private sealed record CreateAlbumRequest(string AlbumName);
    private sealed record CreateAlbumResponse(string Id);
    private sealed record AddAssetsRequest(string[] Ids);
    // Immich no longer inlines an album's assets on GET /api/albums/{id} (verified live against a
    // real Immich instance) — the search endpoint below is the current way to list them.
    private sealed record SearchMetadataRequest(string[] AlbumIds);
    private sealed record SearchMetadataResponse(SearchMetadataAssets Assets);
    private sealed record SearchMetadataAssets(List<AssetResponse> Items);
    private sealed record AssetResponse(string Id, string OriginalFileName, DateTimeOffset FileCreatedAt);
    private sealed record UploadAssetResponse(string Id);
    private sealed record DeleteAssetsRequest(string[] Ids, bool Force);

    public async Task<string> CreateAlbumAsync(string albumName, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Post, "api/albums", new CreateAlbumRequest(albumName), ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateAlbumResponse>(ct)
            ?? throw new InvalidOperationException("Immich album-creation response did not include an id.");
        return created.Id;
    }

    public async Task DeleteAlbumAsync(string immichAlbumId, CancellationToken ct = default)
    {
        var response = await SendAsync<object>(HttpMethod.Delete, $"api/albums/{immichAlbumId}", body: null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ImmichAsset>> GetAlbumAssetsAsync(string immichAlbumId, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Post, "api/search/metadata", new SearchMetadataRequest([immichAlbumId]), ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SearchMetadataResponse>(ct)
            ?? throw new InvalidOperationException("Immich search/metadata response was empty.");
        return result.Assets.Items.Select(a => new ImmichAsset(a.Id, a.OriginalFileName, a.FileCreatedAt)).ToList();
    }

    public async Task<string> UploadAssetAsync(string immichAlbumId, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var apiKey = await GetApiKeyAsync(ct);

        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "assetData", fileName);
        form.Add(new StringContent(Guid.NewGuid().ToString()), "deviceAssetId");
        form.Add(new StringContent("FamilyCloud"), "deviceId");
        var nowIso = DateTimeOffset.UtcNow.ToString("O");
        form.Add(new StringContent(nowIso), "fileCreatedAt");
        form.Add(new StringContent(nowIso), "fileModifiedAt");

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/assets") { Content = form };
        request.Headers.Add("x-api-key", apiKey);
        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var uploaded = await response.Content.ReadFromJsonAsync<UploadAssetResponse>(ct)
            ?? throw new InvalidOperationException("Immich asset-upload response did not include an id.");

        var addResponse = await SendAsync(HttpMethod.Put, $"api/albums/{immichAlbumId}/assets", new AddAssetsRequest([uploaded.Id]), ct);
        addResponse.EnsureSuccessStatusCode();

        return uploaded.Id;
    }

    public async Task DeleteAssetAsync(string assetId, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Delete, "api/assets", new DeleteAssetsRequest([assetId], Force: true), ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ImmichAssetContent> GetAssetThumbnailAsync(string assetId, CancellationToken ct = default)
    {
        var response = await SendAsync<object>(HttpMethod.Get, $"api/assets/{assetId}/thumbnail?size=thumbnail", body: null, ct);
        response.EnsureSuccessStatusCode();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        return new ImmichAssetContent(await response.Content.ReadAsStreamAsync(ct), contentType);
    }

    private async Task<HttpResponseMessage> SendAsync<TBody>(HttpMethod method, string requestUri, TBody? body, CancellationToken ct)
    {
        var apiKey = await GetApiKeyAsync(ct);
        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Add("x-api-key", apiKey);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        // Not disposed via `using` on the caller's side by design — callers read the response body
        // (JSON or a stream, for the thumbnail case) after this returns.
        return await http.SendAsync(request, ct);
    }

    private async Task<string> GetApiKeyAsync(CancellationToken ct)
    {
        var account = await db.Set<ImmichAccount>().FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "No Immich service account has been provisioned yet — see ImmichProvisioner and the seed-admin startup flow in Program.cs.");
        return protector.Decrypt(account.EncryptedApiKey);
    }
}
