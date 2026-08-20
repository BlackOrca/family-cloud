namespace FamilyCloud.Lists.Domain;

/// <summary>Grants a user visibility (and optionally write access) on one <see cref="ItemList"/>.
/// Mirrors CalendarPermission's shape — a resource-scoped grant rather than a family-wide default —
/// so a list can later be kept private to a subset of the household (e.g. a personal todo list)
/// instead of always being visible to every family member.</summary>
public class ListPermission
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid ItemListId { get; set; }

    public ItemList? ItemList { get; set; }

    public bool CanWrite { get; set; }

    public DateTimeOffset GrantedUtc { get; set; }
}
