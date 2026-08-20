using FamilyCloud.Contracts.Calendars;

namespace FamilyCloud.App.Services;

internal enum EventWriteOutcome
{
    Success,
    Forbidden,
    NotFound,
    /// <summary>The event changed on the CalDAV server since it was last synced (ETag mismatch, HTTP 412/409).</summary>
    Conflict,
    Error,
}

internal sealed record EventWriteResult(EventWriteOutcome Outcome, EventDto? Event = null, string? ErrorMessage = null);
