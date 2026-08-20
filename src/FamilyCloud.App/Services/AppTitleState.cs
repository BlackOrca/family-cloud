namespace FamilyCloud.App.Services;

/// <summary>Household app name fetched from the server's <c>/api/settings</c> endpoint. Defaults to
/// "FamilyCloud" until the first successful fetch (e.g. no server configured yet, or offline).</summary>
internal sealed class AppTitleState
{
    public string Title { get; private set; } = "FamilyCloud";

    public event Action? Changed;

    public void Set(string title)
    {
        if (Title == title)
        {
            return;
        }

        Title = title;
        Changed?.Invoke();
    }
}
