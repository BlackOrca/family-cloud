namespace FamilyCloud.Contracts.Calendars;

public sealed record EventWriteRequest(
    string Summary,
    string? Location,
    string? Description,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    bool IsAllDay);
