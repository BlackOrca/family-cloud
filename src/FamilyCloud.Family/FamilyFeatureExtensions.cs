namespace FamilyCloud.Family;

/// <summary>DI wiring for the Family feature. Currently no services beyond what's already registered
/// by Program.cs (Identity, the DbContext) — kept as an explicit extension point so future Family
/// work (e.g. a dedicated family-invite service) has an obvious home, consistent with every other
/// feature project's <c>Add&lt;Feature&gt;Feature()</c> pattern.</summary>
public static class FamilyFeatureExtensions
{
    public static IServiceCollection AddFamilyFeature(this IServiceCollection services) => services;
}
