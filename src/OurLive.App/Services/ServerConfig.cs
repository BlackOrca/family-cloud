namespace OurLive.App.Services;

/// <summary>
/// Dev-time default: pre-filled suggestion on the server setup screen, and the HttpClient fallback
/// used before a server address has been saved via <see cref="ServerAddressStore"/>. The Android
/// emulator reaches the host machine's localhost via the special alias 10.0.2.2; a physical device
/// needs the host's real LAN IP instead.
/// </summary>
internal static class ServerConfig
{
    public const string DefaultBaseAddress = "http://10.0.2.2:5253/";
}
