namespace FamilyCloud.Storage.Domain;

/// <summary>
/// Mirrors one family member's login into a real, individual OpenCloud account — username and password
/// always identical to their FamilyCloud login, kept in lockstep on every password change (see
/// <see cref="OpenCloud.IOpenCloudProvisioner"/>). Unlike Immich's single shared broker account (see
/// <c>FamilyCloud.Photos.Domain.ImmichAccount</c>), a real per-user account is what lets someone log
/// into the official OpenCloud Windows/Android clients directly — the whole reason OpenCloud, not a
/// hand-rolled store, was chosen for the Storage feature (see the Phase 4 architecture roadmap).
/// </summary>
public class OpenCloudAccount
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string Username { get; set; }

    /// <summary>The id OpenCloud assigned this account (from POST /graph/v1.0/users) — diagnostic only,
    /// never used for auth (FamilyCloud always authenticates as the user by username+password).</summary>
    public required string OpenCloudUserId { get; set; }

    public DateTimeOffset ProvisionedUtc { get; set; }
}
