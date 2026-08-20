namespace FamilyCloud.Server.Services;

/// <summary>Broadcasts app-title changes to every connected admin UI circuit so open tabs pick up
/// a renamed household without a manual reload.</summary>
internal sealed class AppTitleNotifier
{
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}
