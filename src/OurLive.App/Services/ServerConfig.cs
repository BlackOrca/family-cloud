namespace OurLive.App.Services;

/// <summary>
/// Dev-time default shown as a pre-filled suggestion on the server setup screen — not used directly
/// for the live HttpClient anymore (see <see cref="ServerAddressStore"/>). The Android emulator reaches
/// the host machine's localhost via the special alias 10.0.2.2; a physical device needs the host's
/// real LAN IP instead.
/// </summary>
internal static class ServerConfig
{
    public const string DefaultBaseAddress = "http://10.0.2.2:5253/";
}
