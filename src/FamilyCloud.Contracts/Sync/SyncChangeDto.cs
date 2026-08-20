namespace FamilyCloud.Contracts.Sync;

public sealed record SyncChangeDto(long Cursor, SyncResourceType ResourceType, string? ResourceId, DateTimeOffset ChangedAtUtc);
