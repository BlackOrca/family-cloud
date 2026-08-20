namespace FamilyCloud.Contracts.Lists;

public sealed record ListItemWriteRequest(string Text, string? Quantity, bool IsDone);
