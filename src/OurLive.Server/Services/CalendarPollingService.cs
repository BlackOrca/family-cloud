using Microsoft.EntityFrameworkCore;
using OurLive.Core.CalDav;
using OurLive.Core.Data;
using OurLive.Core.Security;
using OurLive.Core.Sync;

namespace OurLive.Server.Services;

/// <summary>Periodically pulls every known calendar from its CalDAV server so changes made outside
/// OurLive (a third-party CalDAV client, or another OurLive server) show up here too — the
/// counterpart to the immediate <see cref="CalendarWriteService"/> path for OurLive's own writes.</summary>
internal sealed class CalendarPollingService(IServiceScopeFactory scopeFactory, ILogger<CalendarPollingService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Calendar polling pass failed; will retry next tick.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OurLiveDbContext>();
        var syncService = scope.ServiceProvider.GetRequiredService<CalendarSyncService>();
        var passwordProtector = scope.ServiceProvider.GetRequiredService<ICalDavPasswordProtector>();

        var calendars = await db.Calendars.Include(c => c.CalendarAccount).ToListAsync(ct);
        foreach (var calendar in calendars)
        {
            var account = calendar.CalendarAccount!;
            var credentials = new CalDavCredentials(account.Username, passwordProtector.Decrypt(account.EncryptedAppPassword));
            await syncService.SyncEventsAsync(
                account, calendar, credentials,
                DateTimeOffset.UtcNow.AddMonths(-3), DateTimeOffset.UtcNow.AddMonths(6), ct);
        }
    }
}
