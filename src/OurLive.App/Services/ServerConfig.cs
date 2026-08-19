namespace OurLive.App.Services;

/// <summary>
/// Dev-time server address. The Android emulator reaches the host machine's localhost via the
/// special alias 10.0.2.2 (see plan point on Aspire+MAUI: no dev-tunnel integration for now).
/// A physical device needs the host's real LAN IP instead — swap this constant when testing on one.
/// </summary>
internal static class ServerConfig
{
    public const string BaseAddress = "http://10.0.2.2:5253/";
}
