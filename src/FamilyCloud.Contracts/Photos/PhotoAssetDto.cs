namespace FamilyCloud.Contracts.Photos;

/// <summary><paramref name="ThumbnailUrl"/> is a server-relative path (behind the same JWT-authorized
/// API as everything else) — the client never talks to Immich directly, consistent with the broker
/// model described on <c>IImmichClient</c>.</summary>
public sealed record PhotoAssetDto(string Id, string OriginalFileName, DateTimeOffset FileCreatedAt, string ThumbnailUrl);
