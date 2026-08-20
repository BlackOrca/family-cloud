using FamilyCloud.Calendar.CalDav;
using FamilyCloud.Calendar.Security;
using FamilyCloud.Calendar.Sync;

namespace FamilyCloud.Calendar;

/// <summary>DI wiring for the Calendar feature — everything Program.cs needs to enable it in one call.</summary>
public static class CalendarFeatureExtensions
{
    /// <param name="radicaleHtpasswdPath">Where the bundled Radicale container's htpasswd auth file
    /// lives — resolved by Program.cs (from config, alongside the other data-directory paths) rather
    /// than re-derived here, so there's one place that decides where FamilyCloud's on-disk state goes.</param>
    public static IServiceCollection AddCalendarFeature(this IServiceCollection services, string radicaleHtpasswdPath)
    {
        services.AddHttpClient<ICalDavClient, CalDavClient>()
            // Radicale's bundled server replies HTTP/1.0 and closes the connection after every response
            // (no keep-alive). Without this, SocketsHttpHandler's connection pool intermittently tries to
            // reuse a connection Radicale already closed, surfacing as HttpIOException "ResponseEnded".
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.Zero });
        services.AddScoped<CalendarSyncService>();
        services.AddScoped<CalendarWriteService>();
        services.AddHostedService<CalendarPollingService>();

        services.AddSingleton<ICalDavPasswordProtector, CalDavPasswordProtector>();
        services.AddSingleton<IRadicaleCredentialProvisioner>(
            _ => new RadicaleCredentialProvisioner(radicaleHtpasswdPath));

        return services;
    }
}
