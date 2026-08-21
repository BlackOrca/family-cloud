using FamilyCloud.Storage.OpenCloud;
using FamilyCloud.Storage.Security;

namespace FamilyCloud.Storage;

/// <summary>DI wiring for the Storage feature — everything Program.cs needs to enable it in one call.</summary>
public static class StorageFeatureExtensions
{
    /// <param name="openCloudBaseUrl">Where the bundled OpenCloud instance's API lives — resolved by
    /// Program.cs from config, same as Photos' <c>immichBaseUrl</c>.</param>
    public static IServiceCollection AddStorageFeature(this IServiceCollection services, string openCloudBaseUrl)
    {
        // OpenCloud's HTTPS endpoint uses a self-signed certificate (OC_INSECURE=true — see AppHost.cs;
        // it fatally refuses to start with a plain http:// OC_URL), so certificate validation has to be
        // overridden here the same way a browser would need --insecure/click-through against it. This is
        // the one place in the codebase that needs it, since Radicale/Immich are plain HTTP internally.
        // A fresh handler per registration (not a shared instance) — IHttpClientFactory owns each
        // handler's lifetime independently and periodically disposes/rotates them.
        static HttpClientHandler CreateInsecureHandler() => new()
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };

        services.AddHttpClient<IOpenCloudClient, OpenCloudClient>(c => c.BaseAddress = new Uri(openCloudBaseUrl))
            .ConfigurePrimaryHttpMessageHandler(CreateInsecureHandler);
        services.AddHttpClient<IOpenCloudProvisioner, OpenCloudProvisioner>(c => c.BaseAddress = new Uri(openCloudBaseUrl))
            .ConfigurePrimaryHttpMessageHandler(CreateInsecureHandler);

        services.AddSingleton<IOpenCloudCredentialProtector, OpenCloudCredentialProtector>();

        return services;
    }
}
