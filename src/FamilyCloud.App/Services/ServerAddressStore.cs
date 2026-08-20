namespace FamilyCloud.App.Services;

/// <summary>Persists the FamilyCloud.Server base address the app talks to. Not a secret, so
/// <see cref="Preferences"/> (unlike <see cref="AuthTokenStore"/>'s <c>SecureStorage</c>) is enough.</summary>
internal sealed class ServerAddressStore
{
    private const string AddressKey = "server_base_address";

    public Uri? Load()
    {
        var raw = Preferences.Default.Get(AddressKey, (string?)null);
        return raw is not null && Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri : null;
    }

    public void Save(Uri address) => Preferences.Default.Set(AddressKey, address.ToString());
}
