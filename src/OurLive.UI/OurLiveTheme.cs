using MudBlazor;

namespace OurLive.UI;

/// <summary>Shared MudBlazor theme, used identically by the admin Blazor app and the MAUI Blazor Hybrid app.</summary>
public static class OurLiveTheme
{
    // Colors from the OurLive design tokens (assets/OurLive_Assets/Docs/ourlive-design-tokens.json).
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#19B5C8",
            Secondary = "#7650D9",
            Tertiary = "#3A7DFF",
            AppbarBackground = "#122C55",
            Background = "#F7F8FC",
            Surface = "#FFFFFF",
            LinesDefault = "#E6ECF3",
            TextSecondary = "#64748B",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#4DD8EA",
            Secondary = "#A78BFA",
            Tertiary = "#6FA0FF",
            AppbarBackground = "#0B1B33",
            Background = "#0F2645",
            Surface = "#16335C",
        },
    };
}
