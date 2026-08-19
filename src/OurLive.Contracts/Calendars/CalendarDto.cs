namespace OurLive.Contracts.Calendars;

public sealed record CalendarDto(Guid Id, string DisplayName, string? ColorHex, bool CanWrite);
