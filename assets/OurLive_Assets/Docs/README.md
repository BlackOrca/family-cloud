# OurLive Assets 1.0

Dieses Paket enthält die erste visuelle Basis für die .NET MAUI App "OurLive".

## Struktur

- `Resources/Images/Icons/` — 25 einheitliche SVG Line-Icons
- `Resources/Images/Backgrounds/` — 5 WebP Wallpapers, 1080×2340
- `Resources/AppIcon/` — Logo und drei App-Icon Varianten als SVG
- `Resources/Splash/` — Splash-Hintergrund als WebP
- `Docs/ourlive-design-tokens.json` — Farben, Typografie und Radien

## MAUI

Die SVG-Dateien können als `MauiImage` eingebunden werden. Die WebP-Hintergründe ebenfalls.

Beispiel `.csproj`:

```xml
<ItemGroup>
  <MauiImage Include="Resources\Images\**\*" />
  <MauiImage Include="Resources\AppIcon\**\*" />
  <MauiImage Include="Resources\Splash\**\*" />
</ItemGroup>
```

Für das Android App Icon kann eine der SVG-Varianten als Ausgangspunkt für `MauiIcon` verwendet werden.
Für produktive Store-Assets sollten zusätzlich die finalen Android Adaptive Icon Vorder-/Hintergrund-Layer erzeugt werden.

## Namenskonvention

Icons sind absichtlich semantisch benannt, z.B.:
- `calendar-shared.svg`
- `calendar-private.svg`
- `tasks.svg`
- `shopping.svg`
- `photos.svg`
- `messages.svg`
- `places.svg`

Damit lassen sie sich später direkt in XAML über `FileImageSource` oder als `ImageSource` verwenden.

## Designprinzip

OurLive soll nicht ausschließlich nach "Familien-App" aussehen. Die Bildsprache steht für gemeinsames Leben und funktioniert damit auch für Paare, Freunde, WGs und andere Lebensgemeinschaften.
