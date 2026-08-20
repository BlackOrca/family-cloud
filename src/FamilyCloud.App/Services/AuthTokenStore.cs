namespace FamilyCloud.App.Services;

/// <summary>Persists the JWT across app restarts via the platform secure storage.</summary>
internal sealed class AuthTokenStore
{
    private const string TokenKey = "auth_token";
    private const string ExpiresKey = "auth_token_expires";
    private const string DisplayNameKey = "auth_display_name";

    public async Task SaveAsync(string token, DateTimeOffset expiresUtc, string displayName)
    {
        await SecureStorage.Default.SetAsync(TokenKey, token);
        await SecureStorage.Default.SetAsync(ExpiresKey, expiresUtc.ToString("O"));
        await SecureStorage.Default.SetAsync(DisplayNameKey, displayName);
    }

    public async Task<(string Token, string DisplayName)?> LoadAsync()
    {
        var token = await SecureStorage.Default.GetAsync(TokenKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var expiresRaw = await SecureStorage.Default.GetAsync(ExpiresKey);
        if (expiresRaw is null || !DateTimeOffset.TryParse(expiresRaw, out var expiresUtc) || expiresUtc <= DateTimeOffset.UtcNow)
        {
            Clear();
            return null;
        }

        var displayName = await SecureStorage.Default.GetAsync(DisplayNameKey) ?? "";
        return (token, displayName);
    }

    public void Clear()
    {
        SecureStorage.Default.Remove(TokenKey);
        SecureStorage.Default.Remove(ExpiresKey);
        SecureStorage.Default.Remove(DisplayNameKey);
    }
}
