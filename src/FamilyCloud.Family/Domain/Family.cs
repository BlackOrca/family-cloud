namespace FamilyCloud.Family.Domain;

/// <summary>One household. Modeled as a real row (not a singleton config value) so multi-family
/// operation isn't architecturally foreclosed later, even though today exactly one is ever seeded.</summary>
public class Family
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}
