namespace FamilyCloud.App.Services;

/// <summary>
/// In-memory login state for the current app session. The MAUI app has no ASP.NET Core auth
/// middleware/AuthenticationStateProvider pipeline, so this simple notifier stands in for it —
/// pages subscribe to Changed to react to login/logout (including a forced logout from a 401).
/// UserName/Password are kept here too (not just the JWT) because OpenCloudClient authenticates to the
/// bundled OpenCloud instance directly with these same, mirrored credentials — see AuthTokenStore.
/// </summary>
internal sealed class AppAuthState
{
    public bool IsAuthenticated { get; private set; }
    public string? Token { get; private set; }
    public string? DisplayName { get; private set; }
    public string? UserName { get; private set; }
    public string? Password { get; private set; }

    public event Action? Changed;

    public void SetLoggedIn(string token, string displayName, string userName, string password)
    {
        Token = token;
        DisplayName = displayName;
        UserName = userName;
        Password = password;
        IsAuthenticated = true;
        Changed?.Invoke();
    }

    public void SetLoggedOut()
    {
        Token = null;
        DisplayName = null;
        UserName = null;
        Password = null;
        IsAuthenticated = false;
        Changed?.Invoke();
    }
}
