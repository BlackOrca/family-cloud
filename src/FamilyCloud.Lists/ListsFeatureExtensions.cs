namespace FamilyCloud.Lists;

/// <summary>DI wiring for the Lists feature. Currently no services beyond what's already registered
/// by Program.cs (the DbContext, SyncEventPublisher) — kept as an explicit extension point,
/// consistent with every other feature project's <c>Add&lt;Feature&gt;Feature()</c> pattern.</summary>
public static class ListsFeatureExtensions
{
    public static IServiceCollection AddListsFeature(this IServiceCollection services) => services;
}
