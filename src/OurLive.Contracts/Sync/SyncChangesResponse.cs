namespace OurLive.Contracts.Sync;

/// <summary>
/// <paramref name="FullResyncRequired"/> is always <c>false</c> today — reserved so a future
/// pruning job can tell a caller with a too-old cursor to resync from scratch, without a breaking
/// contract change.
/// </summary>
public sealed record SyncChangesResponse(long Cursor, IReadOnlyList<SyncChangeDto> Changes, bool FullResyncRequired);
