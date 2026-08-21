namespace FamilyCloud.Contracts.Storage;

public sealed record StorageRootShareDto(Guid UserId, string DisplayName, bool CanWrite);
