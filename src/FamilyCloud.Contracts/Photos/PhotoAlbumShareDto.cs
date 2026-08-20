namespace FamilyCloud.Contracts.Photos;

public sealed record PhotoAlbumShareDto(Guid UserId, string DisplayName, bool CanWrite);
