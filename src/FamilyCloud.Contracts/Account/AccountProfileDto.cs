namespace FamilyCloud.Contracts.Account;

public sealed record AccountProfileDto(string UserName, string DisplayName, string? Email, string Role);
