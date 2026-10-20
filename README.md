# TKCode.Arc

Arc-style components for Avalonia, built on [AtomUI](https://github.com/AtomUI/AtomUI).
TKCode.Arc ports the free components of [Arc](https://uiarc.dev) — 110 components in 22 groups —
with Arc's visual language and spring motion.

- Targets `net10.0` and `net11.0`; desktop (Windows, macOS, Linux).
- Depends on AtomUI 6.2.6 and Avalonia 12.1.4 (exact versions).
- Design documents: [design/overview.md](design/overview.md).

## Packages

| Package | Contents |
|---|---|
| `TKCode.Arc` | The components (`UseArc()`), organized like Arc's sidebar. |
| `TKCode.Arc.Core` | ArcDesign tokens and palette, the spring motion engine, shared primitives, 187 built-in Lucide glyphs. |
| `TKCode.Arc.Icons.Lucide` | The full Lucide set (2155 icons): `LucideIcon` control and `LucideGeometry` markup extension. |
| `TKCode.Arc.Fonts` | Geist and Geist Mono (`WithArcFonts()`); body text uses Inter from `Avalonia.Fonts.Inter`. |

## Getting started

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .WithArcFonts()          // optional: Geist display type and Geist Mono code type
    .UseAtomUI(builder =>
    {
        builder.UseDesktopControls();
        builder.UseArc();
    });

public partial class App : Application { }
```

```xml
<Window xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
  <StackPanel Spacing="12">
    <arc:Button Content="Save changes" />
    <arc:Button Content="Cancel" Variant="Secondary" />
    <arc:Switch Content="Email notifications" IsChecked="True" />
    <arc:CopyButton Value="npx arc add copy-button" />
    <arc:Button Content="Share" arc:Tooltip.Tip="Copy link" />
    <arc:Calendar ShowToday="True" />
  </StackPanel>
</Window>
```

Icons: Arc's built-in glyphs (`{x:Static arc:ArcIcons.Check}`) cover every icon the components use; any other Lucide icon
comes from the icons package:

```xml
<Window xmlns:lucide="https://github.com/AtomUICommunity/TKCode.Arc/lucide">
  <lucide:LucideIcon Kind="Sparkles" Size="20" />
</Window>
```

Accent, appearance and motion level:

```csharp
var config = new ThemeConfigBuilder()
    .WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
    .WithArc(arc => arc.Accent(ArcAccentPreset.Violet))
    .Build();
await Application.Current!.GetThemeManager()!.ApplyThemeAsync(
    new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, config, ThemeTransitionReason.UserRequest));

ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);   // Full, Reduced or Off
```

Applications must not enable `InvariantGlobalization` (number and date formatting needs culture data).

## Building

Requires the .NET 11 SDK (see `global.json`) and the .NET 10 runtime (AtomUI build tasks run on it).

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Tests
dotnet run --project gallery/TKCode.Arc.Gallery.Desktop -f net10.0
```

## License

MIT. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for Arc (MIT), Lucide (ISC), fonts (OFL)
and the AtomUI (LGPL-3.0) dependency.
