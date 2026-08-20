namespace FamilyCloud.Lists.Domain;

/// <summary>
/// A household todo or shopping list. Todo and shopping lists share this one entity (distinguished
/// by <see cref="Kind"/>) rather than being separate types, since they're structurally identical —
/// a named list of checkable items — and only differ in how the UI presents them. Named
/// <c>ItemList</c>, not <c>List</c>, to avoid colliding with <see cref="System.Collections.Generic.List{T}"/>.
/// </summary>
public class ItemList
{
    public Guid Id { get; set; }

    /// <summary>The household this list belongs to.</summary>
    public Guid FamilyId { get; set; }

    public ListKind Kind { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public List<ListItem> Items { get; set; } = [];

    public List<ListPermission> Permissions { get; set; } = [];
}
