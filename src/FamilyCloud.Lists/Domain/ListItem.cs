namespace FamilyCloud.Lists.Domain;

/// <summary>One checkable entry in an <see cref="ItemList"/>.</summary>
public class ListItem
{
    public Guid Id { get; set; }

    public Guid ItemListId { get; set; }

    public ItemList? ItemList { get; set; }

    public required string Text { get; set; }

    /// <summary>Free-text amount (e.g. "2", "500g") — only meaningful for shopping-kind lists, left
    /// null otherwise.</summary>
    public string? Quantity { get; set; }

    public bool IsDone { get; set; }

    /// <summary>Manual sort order within the list, lowest first.</summary>
    public int Position { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }
}
