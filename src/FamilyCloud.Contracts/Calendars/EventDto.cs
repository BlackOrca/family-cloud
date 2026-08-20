namespace FamilyCloud.Contracts.Calendars;

public sealed record EventDto(
    Guid Id,
    Guid CalendarId,
    string Summary,
    string? Location,
    string? Description,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    bool IsAllDay,
    string? RecurrenceRule);
