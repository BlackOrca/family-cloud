using OurLive.Contracts.Sync;
using OurLive.Core.Data;
using OurLive.Core.Domain;

namespace OurLive.Core.Sync;

/// <summary>Records that a resource changed, for pollers to pick up via <c>GET /api/sync/changes</c>.
/// Deliberately doesn't save — callers add the row on their own <see cref="OurLiveDbContext"/> right
/// before their own <c>SaveChangesAsync</c>, so the log entry is atomic with the actual mutation.</summary>
public class SyncEventPublisher(OurLiveDbContext db)
{
    public void Publish(SyncResourceType resourceType, string? resourceId)
    {
        db.SyncEvents.Add(new SyncEvent
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            ChangedAtUtc = DateTimeOffset.UtcNow,
        });
    }
}
