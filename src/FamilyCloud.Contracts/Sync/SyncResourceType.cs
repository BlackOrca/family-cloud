namespace FamilyCloud.Contracts.Sync;

/// <summary>Kinds of resource a <see cref="SyncChangeDto"/> can refer to.</summary>
public enum SyncResourceType
{
    Settings = 0,
    Calendar = 1,

    /// <summary>A todo or shopping list changed (list metadata, or any of its items) — the
    /// resource id is the list's id, not an individual item's, mirroring how <see cref="Calendar"/>
    /// events are published under their calendar's id.</summary>
    List = 2,
}
