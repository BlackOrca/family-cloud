using FamilyCloud.Photos.Immich;
using FamilyCloud.Photos.Security;

namespace FamilyCloud.Photos;

/// <summary>DI wiring for the Photos feature — everything Program.cs needs to enable it in one call.</summary>
public static class PhotosFeatureExtensions
{
    /// <param name="immichBaseUrl">Where the bundled Immich instance's API lives — resolved by
    /// Program.cs from config, same as Calendar's <c>radicaleHtpasswdPath</c>/<c>radicaleBaseUrl</c>.</param>
    public static IServiceCollection AddPhotosFeature(this IServiceCollection services, string immichBaseUrl)
    {
        services.AddHttpClient<IImmichClient, ImmichClient>(client => client.BaseAddress = new Uri(immichBaseUrl));
        services.AddHttpClient<IImmichProvisioner, ImmichProvisioner>(client => client.BaseAddress = new Uri(immichBaseUrl));

        services.AddSingleton<IImmichCredentialProtector, ImmichCredentialProtector>();

        return services;
    }
}
