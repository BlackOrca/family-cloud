namespace FamilyCloud.Contracts.Storage;

public sealed record ShareStorageRootRequest(Guid UserId, bool CanWrite);
