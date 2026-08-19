namespace OurLive.Core.Domain;

/// <summary>One CalDAV server connection, managed by an admin.</summary>
public class CalendarAccount
{
    public Guid Id { get; set; }

    public required string DisplayName { get; set; }

    public required string BaseUrl { get; set; }

    public required string Username { get; set; }

    /// <summary>App password, encrypted at rest via ASP.NET Core Data Protection.</summary>
    public required string EncryptedAppPassword { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? LastDiscoveredUtc { get; set; }

    public List<Calendar> Calendars { get; set; } = [];
}
