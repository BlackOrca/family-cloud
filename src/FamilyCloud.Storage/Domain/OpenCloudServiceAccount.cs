namespace FamilyCloud.Storage.Domain;

/// <summary>
/// Single-row table (like <c>FamilyCloud.Photos.Domain.ImmichAccount</c>) holding the bundled OpenCloud
/// instance's built-in "admin" account password — used only by <see cref="OpenCloud.IOpenCloudClient"/>
/// to provision per-family-member <see cref="OpenCloudAccount"/>s and to create/share "Spaces" on the
/// family's behalf. Unlike Immich's admin API key (minted by calling Immich's own sign-up API), this
/// password is one FamilyCloud.Server already knows — it's the same value AppHost.cs passes to the
/// OpenCloud container as IDM_ADMIN_PASSWORD at first boot — so there's nothing to "create" here, only
/// to confirm (see <see cref="OpenCloud.IOpenCloudProvisioner.ProvisionServiceAccountAsync"/>) and store
/// encrypted for reuse on every later request.
/// </summary>
public class OpenCloudServiceAccount
{
    /// <summary>Always 1 — this table only ever holds one row.</summary>
    public int Id { get; set; } = 1;

    public required string EncryptedAdminPassword { get; set; }

    public DateTimeOffset ProvisionedUtc { get; set; }
}
