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

    /// <summary>
    /// True for the account the server itself provisions and keeps in sync with the bundled
    /// Radicale instance (see <see cref="Security.RadicaleCredentialProvisioner"/>) — its credentials
    /// mirror the seed admin's login and are not user-editable.
    /// </summary>
    public bool IsManaged { get; set; }

    public List<Calendar> Calendars { get; set; } = [];
}
