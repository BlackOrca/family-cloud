using FamilyCloud.Contracts.Sync;

namespace FamilyCloud.Core.Domain;

/// <summary>Append-only change log. A client polls for rows past its last-seen <see cref="Id"/>
/// (which doubles as the sync cursor) to find out what to refetch.</summary>
public class SyncEvent
{
    public long Id { get; set; }

    public SyncResourceType ResourceType { get; set; }

    public string? ResourceId { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; }
}
