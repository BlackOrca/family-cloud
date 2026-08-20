namespace FamilyCloud.Photos.Domain;

/// <summary>
/// Single-row table (like <c>AppSettings</c>) holding the one shared Immich API key FamilyCloud uses
/// to broker all photo access — see the Phase 3 architecture roadmap for why a single service-account
/// credential was chosen over one Immich user per FamilyCloud member. Provisioned once by
/// <see cref="Security.IImmichProvisioner"/> during seed-admin bootstrap, mirroring how
/// <c>RadicaleCredentialProvisioner</c> bootstraps the bundled Radicale's single managed credential.
/// </summary>
public class ImmichAccount
{
    /// <summary>Always 1 — this table only ever holds one row.</summary>
    public int Id { get; set; } = 1;

    /// <summary>Immich API key, encrypted at rest via <see cref="Core.Security.IBackendCredentialProtector"/>.</summary>
    public required string EncryptedApiKey { get; set; }

    /// <summary>The Immich-side admin user id the key belongs to — diagnostic only, never used for auth.</summary>
    public required string ImmichUserId { get; set; }

    public DateTimeOffset ProvisionedUtc { get; set; }
}
