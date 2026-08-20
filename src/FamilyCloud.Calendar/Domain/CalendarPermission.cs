namespace FamilyCloud.Calendar.Domain;

/// <summary>Grants a user visibility (and optionally write access) on one <see cref="Calendar"/>.</summary>
public class CalendarPermission
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid CalendarId { get; set; }

    public Calendar? Calendar { get; set; }

    public bool CanWrite { get; set; }

    public DateTimeOffset GrantedUtc { get; set; }
}
