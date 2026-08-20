using FamilyCloud.Contracts.Sync;

namespace FamilyCloud.App.Services;

/// <summary>
/// Polls <c>GET /api/sync/changes</c> on a timer while the app is authenticated and foregrounded,
/// and dispatches each change to whichever handlers subscribed for its <see cref="SyncResourceType"/>.
/// This dispatch registry is the extension point: a future feature (e.g. Todos) just needs a new
/// <see cref="SyncResourceType"/> member, a write path that publishes to it, and a page that calls
/// <see cref="Subscribe"/> — nothing here needs to change.
/// </summary>
internal sealed class SyncCoordinator : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(12);

    private readonly ApiClient api;
    private readonly AppAuthState authState;
    private readonly Dictionary<SyncResourceType, List<Func<string?, Task>>> handlers = [];
    private readonly object gate = new();

    private CancellationTokenSource? loopCts;
    private long? cursor;
    private bool isForeground = true;

    public SyncCoordinator(ApiClient api, AppAuthState authState)
    {
        this.api = api;
        this.authState = authState;
        authState.Changed += OnAuthChanged;
    }

    /// <summary>Registers <paramref name="onChanged"/> for changes of <paramref name="resourceType"/>.
    /// Dispose the result to unsubscribe (e.g. from a page's <c>Dispose()</c>).</summary>
    public IDisposable Subscribe(SyncResourceType resourceType, Func<string?, Task> onChanged)
    {
        lock (gate)
        {
            if (!handlers.TryGetValue(resourceType, out var subs))
            {
                subs = [];
                handlers[resourceType] = subs;
            }
            subs.Add(onChanged);
        }

        return new Subscription(this, resourceType, onChanged);
    }

    public void Resume()
    {
        isForeground = true;
        EvaluateRunState();
    }

    public void Pause()
    {
        isForeground = false;
        EvaluateRunState();
    }

    private void OnAuthChanged()
    {
        if (!authState.IsAuthenticated)
        {
            // Force a clean bootstrap (fresh cursor) on the next login rather than replaying
            // another user/session's history.
            cursor = null;
        }
        EvaluateRunState();
    }

    private void EvaluateRunState()
    {
        var shouldRun = isForeground && authState.IsAuthenticated;
        if (shouldRun && loopCts is null)
        {
            loopCts = new CancellationTokenSource();
            _ = RunLoopAsync(loopCts.Token);
        }
        else if (!shouldRun && loopCts is not null)
        {
            loopCts.Cancel();
            loopCts.Dispose();
            loopCts = null;
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(PollInterval);
            do
            {
                await PollOnceAsync(ct);
            } while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
            // Pause() or logout cancelled the loop — expected.
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        try
        {
            var response = await api.GetSyncChangesAsync(cursor, ct);
            if (response is null)
            {
                return;
            }

            if (response.FullResyncRequired)
            {
                foreach (var type in handlers.Keys.ToList())
                {
                    await DispatchAsync(type, null);
                }
            }
            else
            {
                foreach (var change in response.Changes)
                {
                    await DispatchAsync(change.ResourceType, change.ResourceId);
                }
            }

            cursor = response.Cursor;
        }
        catch (Exception)
        {
            // Offline, unreachable, or a 401 (already handled by AuthTokenHandler, which will flip
            // AppAuthState and stop this loop via OnAuthChanged) — just retry next tick.
        }
    }

    private async Task DispatchAsync(SyncResourceType resourceType, string? resourceId)
    {
        List<Func<string?, Task>>? subs;
        lock (gate)
        {
            if (!handlers.TryGetValue(resourceType, out var found))
            {
                return;
            }
            subs = [.. found];
        }

        foreach (var handler in subs)
        {
            try
            {
                await handler(resourceId);
            }
            catch (Exception)
            {
                // One subscriber's failure must not break the loop or other subscribers.
            }
        }
    }

    private void Unsubscribe(SyncResourceType resourceType, Func<string?, Task> onChanged)
    {
        lock (gate)
        {
            if (handlers.TryGetValue(resourceType, out var subs))
            {
                subs.Remove(onChanged);
            }
        }
    }

    public void Dispose()
    {
        authState.Changed -= OnAuthChanged;
        loopCts?.Cancel();
        loopCts?.Dispose();
    }

    private sealed class Subscription(SyncCoordinator coordinator, SyncResourceType resourceType, Func<string?, Task> handler) : IDisposable
    {
        public void Dispose() => coordinator.Unsubscribe(resourceType, handler);
    }
}
