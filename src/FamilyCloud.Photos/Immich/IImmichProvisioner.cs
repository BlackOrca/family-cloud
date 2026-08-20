namespace FamilyCloud.Photos.Immich;

/// <summary>
/// Bootstraps the single shared Immich service account FamilyCloud brokers all photo access through —
/// mirrors <c>IRadicaleCredentialProvisioner</c>'s role for the bundled Radicale instance, except Immich
/// has no htpasswd-style file to write: provisioning instead means calling Immich's own auth API to
/// create its first admin account (or sign in to the existing one) and mint an API key.
/// </summary>
public interface IImmichProvisioner
{
    /// <summary>Returns the newly minted plaintext Immich API key — the caller (Program.cs' seed-admin
    /// flow) is responsible for encrypting and persisting it, same as it already does for the Radicale
    /// password via <c>ICalDavPasswordProtector</c>.</summary>
    Task<string> ProvisionAsync(string adminEmail, string adminPassword, CancellationToken ct = default);
}
