namespace OurLive.Contracts.Sync;

/// <summary>
/// Kinds of resource a <see cref="SyncChangeDto"/> can refer to. <c>Todo</c> and
/// <c>ShoppingListItem</c> are reserved for future features that don't exist yet — adding them
/// (plus a publisher on their write path and a subscriber on their page) is the entire extension
/// point of the sync mechanism.
/// </summary>
public enum SyncResourceType
{
    Settings = 0,
    Calendar = 1,
}
