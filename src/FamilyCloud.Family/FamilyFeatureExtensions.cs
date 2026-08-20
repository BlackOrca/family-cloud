using FamilyCloud.Core.Auth;

namespace FamilyCloud.Family;

/// <summary>DI wiring for the Family feature.</summary>
public static class FamilyFeatureExtensions
{
    public static IServiceCollection AddFamilyFeature(this IServiceCollection services)
    {
        services.AddScoped<IFamilyMembershipChecker, FamilyMembershipChecker>();
        return services;
    }
}
