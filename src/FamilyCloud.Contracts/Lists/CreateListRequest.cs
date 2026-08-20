namespace FamilyCloud.Contracts.Lists;

/// <summary><paramref name="Kind"/> is "Todo" or "Shopping" (matches FamilyCloud.Lists.Domain.ListKind).</summary>
public sealed record CreateListRequest(string Name, string Kind);
