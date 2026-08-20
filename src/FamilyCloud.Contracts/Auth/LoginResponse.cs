namespace FamilyCloud.Contracts.Auth;

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresUtc, string DisplayName);
