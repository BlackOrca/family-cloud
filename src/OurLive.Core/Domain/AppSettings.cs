namespace OurLive.Core.Domain;

/// <summary>Single-row table of household-wide app settings, editable by any admin user.</summary>
public class AppSettings
{
    /// <summary>Always 1 — this table only ever holds one row.</summary>
    public int Id { get; set; } = 1;

    public string Title { get; set; } = "OurLive";
}
