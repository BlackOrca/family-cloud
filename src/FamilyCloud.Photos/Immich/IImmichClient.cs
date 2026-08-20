namespace FamilyCloud.Photos.Immich;

public sealed record ImmichAsset(string Id, string OriginalFileName, DateTimeOffset FileCreatedAt);

public sealed record ImmichAssetContent(Stream Content, string ContentType);

/// <summary>
/// Thin wrapper over the subset of Immich's REST API FamilyCloud needs, authenticated with the single
/// shared service-account API key from <see cref="Domain.ImmichAccount"/> (the "broker" model — see the
/// Phase 3 architecture roadmap: FamilyCloud controls visibility itself via
/// <see cref="Domain.PhotoAlbumPermission"/>, the same way it already brokers the one shared Radicale
/// credential for Calendar). Every FamilyCloud <see cref="Domain.PhotoAlbum"/> maps 1:1 to one Immich
/// album; asset listings are always fetched live rather than cached locally, since Immich has no
/// webhook mechanism to invalidate a local cache against.
/// </summary>
public interface IImmichClient
{
    /// <summary>Creates an Immich album and returns its Immich-side id.</summary>
    Task<string> CreateAlbumAsync(string albumName, CancellationToken ct = default);

    Task DeleteAlbumAsync(string immichAlbumId, CancellationToken ct = default);

    Task<IReadOnlyList<ImmichAsset>> GetAlbumAssetsAsync(string immichAlbumId, CancellationToken ct = default);

    /// <summary>Uploads a new asset and adds it to the given album in one call. Returns the Immich-side asset id.</summary>
    Task<string> UploadAssetAsync(string immichAlbumId, string fileName, Stream content, string contentType, CancellationToken ct = default);

    Task DeleteAssetAsync(string assetId, CancellationToken ct = default);

    Task<ImmichAssetContent> GetAssetThumbnailAsync(string assetId, CancellationToken ct = default);
}
