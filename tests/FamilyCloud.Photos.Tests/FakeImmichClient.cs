using FamilyCloud.Photos.Immich;

namespace FamilyCloud.Photos.Tests;

/// <summary>
/// In-memory stand-in for <see cref="IImmichClient"/> — tests run against no live Immich instance
/// (see FamilyCloudWebApplicationFactory, which swaps this in for the real ImmichClient), so this
/// mimics just enough of Immich's album/asset behavior for PhotosEndpoints' round trips to work.
/// </summary>
internal sealed class FakeImmichClient : IImmichClient
{
    private readonly Dictionary<string, List<ImmichAsset>> albums = [];
    private int nextId;

    public Task<string> CreateAlbumAsync(string albumName, CancellationToken ct = default)
    {
        var id = $"immich-album-{++nextId}";
        albums[id] = [];
        return Task.FromResult(id);
    }

    public Task DeleteAlbumAsync(string immichAlbumId, CancellationToken ct = default)
    {
        albums.Remove(immichAlbumId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ImmichAsset>> GetAlbumAssetsAsync(string immichAlbumId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ImmichAsset>>(albums.TryGetValue(immichAlbumId, out var assets) ? assets : []);

    public Task<string> UploadAssetAsync(string immichAlbumId, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var id = $"immich-asset-{++nextId}";
        albums[immichAlbumId].Add(new ImmichAsset(id, fileName, DateTimeOffset.UtcNow));
        return Task.FromResult(id);
    }

    public Task DeleteAssetAsync(string assetId, CancellationToken ct = default)
    {
        foreach (var assets in albums.Values)
        {
            assets.RemoveAll(a => a.Id == assetId);
        }
        return Task.CompletedTask;
    }

    public Task<ImmichAssetContent> GetAssetThumbnailAsync(string assetId, CancellationToken ct = default) =>
        Task.FromResult(new ImmichAssetContent(new MemoryStream([1, 2, 3]), "image/jpeg"));
}
