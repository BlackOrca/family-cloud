using Microsoft.EntityFrameworkCore;
using FamilyCloud.Contracts.Sync;
using FamilyCloud.Core.Domain;

namespace FamilyCloud.Core.Sync;

/// <summary>Records that a resource changed, for pollers to pick up via <c>GET /api/sync/changes</c>.
/// Deliberately doesn't save — callers add the row on their own <see cref="DbContext"/> right
/// before their own <c>SaveChangesAsync</c>, so the log entry is atomic with the actual mutation.
/// Takes the abstract EF Core <see cref="DbContext"/> (not the concrete, composed FamilyCloudDbContext,
/// which lives in FamilyCloud.Server) so this generic, cross-feature service has no dependency on any
/// one feature project.</summary>
public class SyncEventPublisher(DbContext db)
{
    public void Publish(SyncResourceType resourceType, string? resourceId)
    {
        db.Set<SyncEvent>().Add(new SyncEvent
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            ChangedAtUtc = DateTimeOffset.UtcNow,
        });
    }
}
