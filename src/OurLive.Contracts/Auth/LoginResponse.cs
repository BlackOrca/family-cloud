namespace OurLive.Contracts.Auth;

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresUtc, string DisplayName);
