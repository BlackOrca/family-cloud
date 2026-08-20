namespace FamilyCloud.Contracts.Lists;

public sealed record ItemListDto(Guid Id, string Name, string Kind, bool CanWrite);
