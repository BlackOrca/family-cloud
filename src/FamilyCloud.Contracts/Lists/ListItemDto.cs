namespace FamilyCloud.Contracts.Lists;

public sealed record ListItemDto(Guid Id, Guid ItemListId, string Text, string? Quantity, bool IsDone, int Position);
