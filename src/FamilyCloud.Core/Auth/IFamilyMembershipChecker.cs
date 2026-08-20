namespace FamilyCloud.Core.Auth;

/// <summary>
/// Lets a feature project (e.g. Lists, when sharing a resource with another user) verify family
/// membership without referencing FamilyCloud.Family's domain types directly — feature projects are
/// only ever referenced by FamilyCloud.Server, never by each other. Implemented in
/// FamilyCloud.Family, registered by AddFamilyFeature().
/// </summary>
public interface IFamilyMembershipChecker
{
    Task<bool> IsMemberAsync(Guid familyId, Guid userId, CancellationToken ct = default);
}
