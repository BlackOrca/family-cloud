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

    /// <summary>A photo album changed (album metadata, sharing, or any of its assets) — the resource
    /// id is the album's id, not an individual asset's, same convention as <see cref="List"/>.</summary>
    Photo = 3,

    /// <summary>A Storage root ("Space") was created or its sharing changed — the resource id is the
    /// OpenCloud drive id. Unlike the other resource types, folder/file changes *within* a root never
    /// publish here: OpenCloud is the source of truth for that, not FamilyCloud's own DB, so there's
    /// nothing for FamilyCloud.Server to know about or publish (see the Phase 4 architecture roadmap).</summary>
    Files = 4,
}
