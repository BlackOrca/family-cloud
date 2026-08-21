namespace FamilyCloud.App.Services;

/// <summary>Persists the JWT across app restarts via the platform secure storage. Also persists the
/// plaintext username/password from login — needed because <see cref="OpenCloudClient"/> talks to the
/// bundled OpenCloud instance directly with Basic-Auth using these same, mirrored credentials (see
/// OpenCloudAccount on the server), not the JWT this store otherwise carries.</summary>
internal sealed class AuthTokenStore
{
    private const string TokenKey = "auth_token";
    private const string ExpiresKey = "auth_token_expires";
    private const string DisplayNameKey = "auth_display_name";
    private const string UserNameKey = "auth_user_name";
    private const string PasswordKey = "auth_password";

    public async Task SaveAsync(string token, DateTimeOffset expiresUtc, string displayName, string userName, string password)
    {
        await SecureStorage.Default.SetAsync(TokenKey, token);
        await SecureStorage.Default.SetAsync(ExpiresKey, expiresUtc.ToString("O"));
        await SecureStorage.Default.SetAsync(DisplayNameKey, displayName);
        await SecureStorage.Default.SetAsync(UserNameKey, userName);
        await SecureStorage.Default.SetAsync(PasswordKey, password);
    }

    public async Task<(string Token, string DisplayName, string UserName, string Password)?> LoadAsync()
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
        var userName = await SecureStorage.Default.GetAsync(UserNameKey) ?? "";
        var password = await SecureStorage.Default.GetAsync(PasswordKey) ?? "";
        return (token, displayName, userName, password);
    }

    public void Clear()
    {
        SecureStorage.Default.Remove(TokenKey);
        SecureStorage.Default.Remove(ExpiresKey);
        SecureStorage.Default.Remove(DisplayNameKey);
        SecureStorage.Default.Remove(UserNameKey);
        SecureStorage.Default.Remove(PasswordKey);
    }
}
