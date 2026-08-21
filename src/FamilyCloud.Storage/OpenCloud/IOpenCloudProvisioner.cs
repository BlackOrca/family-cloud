namespace FamilyCloud.Storage.OpenCloud;

/// <summary>
/// Bootstraps the bundled OpenCloud instance's service account and keeps per-family-member
/// <see cref="Domain.OpenCloudAccount"/>s in sync with their FamilyCloud login — mirrors
/// <c>IRadicaleCredentialProvisioner</c>'s role for Radicale, except unlike Radicale's single
/// overwritten htpasswd line, OpenCloud supports real per-user accounts, so every family member gets
/// their own (see the Phase 4 architecture roadmap for why that's worth the extra provisioning calls).
/// </summary>
public interface IOpenCloudProvisioner
{
    /// <summary>Confirms the "admin" account (already created by the container itself via
    /// IDM_ADMIN_PASSWORD — nothing to create here, unlike Immich's admin-sign-up) is reachable and the
    /// password FamilyCloud.Server was configured with actually works. Tolerates OpenCloud not being up
    /// yet, retrying with backoff like <c>IImmichProvisioner</c>.</summary>
    Task ProvisionServiceAccountAsync(string adminPassword, CancellationToken ct = default);

    /// <summary>Creates or updates the OpenCloud account mirroring one family member's FamilyCloud login.
    /// Called both at user creation (with their initial password) and on every later password change, so
    /// a newly created user's OpenCloud account exists immediately rather than only after they first
    /// change their own password.</summary>
    Task ProvisionUserAsync(Guid userId, string username, string? email, string plainPassword, CancellationToken ct = default);
}
