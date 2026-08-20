namespace FamilyCloud.Contracts.Lists;

public sealed record ListShareDto(Guid UserId, string DisplayName, bool CanWrite);
