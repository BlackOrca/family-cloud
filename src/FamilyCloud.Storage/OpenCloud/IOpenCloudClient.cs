namespace FamilyCloud.Storage.OpenCloud;

/// <summary>Space-root sharing roles, mirroring OpenCloud's own role names ("Can view"/"Can edit"/"Can
/// manage") — see <see cref="OpenCloudClient"/> for how these resolve to OpenCloud's internal role
/// GUIDs.</summary>
public enum StorageRole
{
    Viewer,
    Editor,
    Manager,
}

/// <summary>
/// Thin wrapper over the subset of the bundled OpenCloud instance's Graph API FamilyCloud needs,
/// authenticated with the shared "admin" account password from
/// <see cref="Domain.OpenCloudServiceAccount"/> — used only to provision per-family-member accounts and
/// to create/share Spaces on the family's behalf (see the Phase 4 architecture roadmap). Unlike
/// <c>FamilyCloud.Photos.Immich.IImmichClient</c>, this is not the transport for actual file data — once
/// a family member has their own <see cref="Domain.OpenCloudAccount"/>, browsing/upload/download happens
/// directly between FamilyCloud.App and OpenCloud's own WebDAV/Graph API, never through this client or
/// FamilyCloud.Server at all.
/// </summary>
public interface IOpenCloudClient
{
    /// <summary>Confirms the admin credential is valid and OpenCloud is reachable (GET /graph/v1.0/me).
    /// Throws if not — callers retry, mirroring <c>IImmichProvisioner</c>'s tolerance for OpenCloud still
    /// starting up.</summary>
    Task VerifyServiceAccountAsync(CancellationToken ct = default);

    /// <summary>Creates a real OpenCloud user account and returns its OpenCloud-side id.</summary>
    Task<string> CreateUserAsync(string username, string email, string plainPassword, CancellationToken ct = default);

    /// <summary>Updates an existing OpenCloud user's password (used to keep an <see cref="Domain.OpenCloudAccount"/>
    /// in lockstep with its FamilyCloud counterpart after a password change).</summary>
    Task UpdatePasswordAsync(string openCloudUserId, string newPlainPassword, CancellationToken ct = default);

    /// <summary>Creates a "project" Space (OpenCloud's sharable root-folder unit) and returns its drive id.</summary>
    Task<string> CreateDriveAsync(string name, CancellationToken ct = default);

    /// <summary>Grants a user access to a Space's root and returns the permission id (needed for later
    /// <see cref="UpdateRoleAsync"/>/<see cref="RevokeAsync"/> calls).</summary>
    Task<string> InviteAsync(string driveId, string openCloudUserId, StorageRole role, CancellationToken ct = default);

    /// <summary>Changes an existing grant's role — re-inviting an already-invited user 409s, so this is
    /// the only way to change someone's access level once granted.</summary>
    Task UpdateRoleAsync(string driveId, string permissionId, StorageRole role, CancellationToken ct = default);

    Task RevokeAsync(string driveId, string permissionId, CancellationToken ct = default);

    /// <summary>Looks up the permission id for an existing grant on a drive, or null if the user has no
    /// grant there yet — StorageEndpoints only knows the FamilyCloud user id, not OpenCloud's internal
    /// permission id, and needs this to decide between <see cref="InviteAsync"/> and
    /// <see cref="UpdateRoleAsync"/>/<see cref="RevokeAsync"/>.</summary>
    Task<string?> FindPermissionIdAsync(string driveId, string openCloudUserId, CancellationToken ct = default);

    /// <summary>Whether the given user holds Manager access on this drive's root. Every StorageEndpoints
    /// sharing call authenticates to OpenCloud as the admin service account (never as the caller), so
    /// OpenCloud's own authorization can't gate who's allowed to share a root — this is the check
    /// StorageEndpoints uses instead, mirroring how Photos/Lists gate sharing on the caller's own
    /// CanWrite permission row before letting them grant/revoke someone else's access.</summary>
    Task<bool> HasManagerAccessAsync(string driveId, string openCloudUserId, CancellationToken ct = default);
}
