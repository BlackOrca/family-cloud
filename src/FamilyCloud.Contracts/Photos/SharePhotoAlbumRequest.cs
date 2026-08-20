namespace FamilyCloud.Contracts.Photos;

public sealed record SharePhotoAlbumRequest(Guid UserId, bool CanWrite);
