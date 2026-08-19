using MudBlazor;

namespace OurLive.UI;

/// <summary>Shared MudBlazor theme, used identically by the admin Blazor app and the MAUI Blazor Hybrid app.</summary>
public static class OurLiveTheme
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#3F6B8A",
            Secondary = "#8A6B3F",
            AppbarBackground = "#3F6B8A",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7FA8C9",
            Secondary = "#C9A87F",
        },
    };
}
