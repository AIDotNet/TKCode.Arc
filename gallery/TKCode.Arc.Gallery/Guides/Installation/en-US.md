---
title: Installation
---

# Installation

This guide adds TKCode.Arc to an Avalonia desktop application, from the package references to the first screen. It
takes about five minutes on a new project and does not change how the rest of your Avalonia code works.

## Requirements

| Item | Version | Notes |
|---|---|---|
| .NET | `net10.0` or `net11.0` | The packages target both. |
| Avalonia | `12.1.4` | Pinned to the exact version the library is built against. |
| TKCode.Arc.Foundation | same as Arc | MIT; the theme, token, language and package runtime. Comes with `TKCode.Arc`. |
| Fonts (optional) | `Avalonia.Fonts.Inter` | Arc's body text uses Inter; Geist comes from `TKCode.Arc.Fonts`. |

TKCode.Arc is in preview, and Avalonia is pinned to an exact version. Besides Avalonia, every Arc package depends only
on MIT packages of its own. Use the same Avalonia version in your application; mixing versions leads to restore
warnings and, at run time, to missing styles or type load errors.

## 1. Add the packages

```bash
dotnet add package TKCode.Arc --version 0.3.2-preview.2
dotnet add package TKCode.Arc.Fonts --version 0.3.2-preview.2           # optional: Geist and Geist Mono
dotnet add package TKCode.Arc.Icons.Lucide --version 0.3.2-preview.2    # optional: the full Lucide set
dotnet add package Avalonia.Fonts.Inter --version 12.1.4                 # optional: Inter for body text
```

`TKCode.Arc` brings in `TKCode.Arc.Core`, `TKCode.Arc.Foundation` and StreamMark. If your solution uses central
package management, add the versions to `Directory.Packages.props` once and keep the project files version-free:

```xml
<ItemGroup>
    <PackageVersion Include="TKCode.Arc" Version="0.3.2-preview.2" />
    <PackageVersion Include="TKCode.Arc.Fonts" Version="0.3.2-preview.2" />
    <PackageVersion Include="TKCode.Arc.Icons.Lucide" Version="0.3.2-preview.2" />
    <PackageVersion Include="Avalonia.Fonts.Inter" Version="12.1.4" />
</ItemGroup>
```

Pin the exact preview version. Floating versions such as `0.1.*` can pick up a preview with breaking changes on the next
restore.

## 2. Register Arc in the application builder

Arc is registered on the `AppBuilder` with `UseArc(...)`. A typical `Program.cs`:

```csharp
using Avalonia;
using TKCode.Arc;
using TKCode.Arc.Fonts;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .WithInterFont()      // Inter for body text (Avalonia.Fonts.Inter)
                  .WithArcFonts()       // Geist for display text, Geist Mono for code (TKCode.Arc.Fonts)
                  .UseArc(arc => arc
                      .UseArcControls()
                      .WithTheme(t => t.Appearance(ArcAppearance.System).Accent(ArcAccentPreset.Neutral)));
}
```

What each call does:

| Call | Purpose |
|---|---|
| `UseArc(...)` | Starts Arc's theme manager, design tokens and languages once the application is initialized. The callback lists the packages and the initial theme. |
| `UseArcControls()` | Registers Arc's controls, themes and tokens. It also registers `TKCode.Arc.Core` (`UseArcCore()`), so you do not call that yourself. |
| `WithTheme(...)` | Optional: the theme the first frame is drawn with (appearance, accent, token overrides). Without it Arc starts light with the `Neutral` accent. See **Theming**. |
| `WithInterFont()`, `WithArcFonts()` | Register the fonts Arc's type scale is designed for. Without them, text falls back to the system font. |

The `Application` class needs nothing from Arc: no base class, no `partial` keyword, no styles in `App.axaml`.

```csharp
public class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

Arc also styles the Avalonia controls its templates use (`ScrollViewer`, `ScrollBar`, `ItemsControl`, `PathIcon`, the
text selection handles and the popup hosts). Scroll bars look like Arc's `ScrollArea`: thin, shown while the pointer is
over the view or while it scrolls, hidden at rest; set `ScrollViewer.AllowAutoHide="False"` to keep them visible. When
`App.axaml` already includes `FluentTheme` or `SimpleTheme`, Arc leaves these controls to that theme.

## 3. Use the controls in XAML

Arc's XAML namespace is `https://github.com/AIDotNet/TKCode.Arc`; every control, token resource and markup
extension lives there.

![A notification settings card built in the first minutes with Arc](images/FirstScreen.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             Width="560">
    <arc:Card Title="Notifications" Description="Choose how Acme Analytics keeps your team informed.">
        <StackPanel Spacing="16" Margin="0,12,0,0">
            <arc:Input Label="Alert email"
                       Text="ops@acme.com"
                       Description="Incidents and failed payments are sent here." />
            <arc:Switch Content="Weekly usage digest" IsChecked="True" />
            <arc:Switch Content="Product announcements" />
            <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
                <arc:Button Content="Cancel" Variant="Secondary" />
                <arc:Button Content="Save changes" />
            </StackPanel>
        </StackPanel>
    </arc:Card>
</UserControl>
```

The controls behave like the Avalonia controls they derive from: bind `Text`, `IsChecked` and `Command` to a view
model, style them with selectors, and validate with Avalonia's data validation. Each component page in this gallery
shows the bindings, events and keyboard model of that control.

### Name clashes with Avalonia controls

`Button`, `Calendar`, `Carousel`, `ContextMenu`, `DatePicker`, `Slider`, `SplitButton`, `TabItem`, `TimePicker`,
`TreeView` and `TreeViewItem` share their names with types in `Avalonia.Controls`. In XAML the `arc:` prefix keeps them
apart. In C#, alias one namespace when a file uses both:

```csharp
using Avalonia.Controls;
using ArcButton = TKCode.Arc.Button;

var save = new ArcButton { Content = "Save changes" };
```

## Optional packages

### Icons

Arc's own glyphs (`{x:Static arc:ArcIcons.Check}` and 186 more) cover every icon the components use. For any other
Lucide icon, add `TKCode.Arc.Icons.Lucide` and use its namespace:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:lucide="https://github.com/AIDotNet/TKCode.Arc/lucide">
    <lucide:LucideIcon Kind="Sparkles" Size="20" />
</UserControl>
```

### Blocks and the data grid

`TKCode.Arc.Blocks` (page-level blocks), `TKCode.Arc.AdvancedCharts` (the `DataGrid`) and `TKCode.Arc.Special` (animated
AI explainers) are separate packages. Add the ones you need and register them inside `UseArc(...)`:

```bash
dotnet add package TKCode.Arc.Blocks --version 0.3.2-preview.2
dotnet add package TKCode.Arc.AdvancedCharts --version 0.3.2-preview.2
dotnet add package TKCode.Arc.Special --version 0.3.2-preview.2
dotnet add package TKCode.Arc.Workflow --version 0.3.2-preview.2
dotnet add package TKCode.Arc.Themes.Pixel --version 0.3.2-preview.2    # optional: pixel-art theme (UseArcPixelTheme())
dotnet add package TKCode.Arc.Themes.Corners --version 0.3.2-preview.2    # optional: global corner style (UseArcCorners())
dotnet add package TKCode.Arc.Mobile --version 0.3.2-preview.2    # optional: touch-first controls, not yet verified on devices (UseArcMobile())
```

```csharp
.UseArc(arc => arc
    .UseArcControls()
    .UseArcBlocks()            // TKCode.Arc.Blocks
    .UseArcAdvancedCharts()    // TKCode.Arc.AdvancedCharts
    .UseArcSpecial()           // TKCode.Arc.Special
    .UseArcWorkflow()          // TKCode.Arc.Workflow
    .UseArcPixelTheme()        // TKCode.Arc.Themes.Pixel
    .UseArcCorners()           // TKCode.Arc.Themes.Corners
    .UseArcMobile())           // TKCode.Arc.Mobile
```

Each package registers the packages it depends on (Blocks registers the controls, for example), and registering one
twice does nothing.

### Languages

Arc handles the application language (namespace `TKCode.Arc.Localization`). To offer more than one, declare them in
`UseArc(...)`: the first argument is the start-up language, the second the languages the application offers. The
gallery offers English, Simplified Chinese and Traditional Chinese this way:

```csharp
.UseArc(arc => arc
    .UseArcControls()
    .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]))

// Later, for example from a settings page:
ArcLanguage.Set(LanguageTags.ZhCN);
```

`ArcLanguage.Current` is the current language, `ArcLanguage.Culture` the culture to format numbers and dates with, and
`ArcLanguage.Changed` is raised after a switch. The packages with built-in text of their own (the `DataGrid`, the
workflow canvas) ship English, Simplified Chinese and Traditional Chinese. Start-up fails with a list of the missing
text when an enabled package does not cover a declared language.

Arc controls ship English defaults for their built-in text (for example "Show password" or "Clear search"). The
controls expose that text as properties or label objects, so you can translate it from your own resources.

## Publishing

- **NativeAOT and trimming.** The libraries are trimming- and AOT-compatible and use no reflection. Publish a desktop
  app as a single self-contained file with `-p:PublishAot=true`, or `-p:PublishSingleFile=true --self-contained` when
  you do not use AOT.
- **.NET SDK 11 and NativeAOT.** The .NET 11 SDK's optimized AOT publish drops the compiled XAML of every Avalonia
  12.1 application, which then fails at start with `No precompiled XAML found`. Turn it off in the application's
  project file: `<UseOptimizedPublish>false</UseOptimizedPublish>`.
- **License.** Arc, its Foundation package and its other dependencies are MIT licensed (Avalonia too), so a closed-source
  single-file or NativeAOT publish has no LGPL obligations.
- **Globalization.** Do not enable `InvariantGlobalization`. Number, currency and date formatting in the inputs,
  pickers and charts needs culture data.
- **macOS.** The repository's `tools/macos` scripts show how the gallery is wrapped into a signed and notarized `.app`;
  the same flow applies to your application.

## Building from source

Building the repository needs the .NET 11 SDK (see `global.json`) and the .NET 10 runtime (the tests and the
gallery run on `net10.0`):

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Foundation.Tests
dotnet test tests/TKCode.Arc.Tests
dotnet run --project gallery/TKCode.Arc.Gallery.Desktop -f net10.0
```

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Arc controls appear unstyled or empty | `UseArcControls()` is not called in `UseArc(...)` | Add it to the `UseArc(...)` callback. |
| `InvalidOperationException` at start-up listing missing text | A language in `UseLanguages` is not covered by an enabled package | Declare only languages the packages ship, or add the missing translations. |
| `No precompiled XAML found` in a NativeAOT build | The .NET 11 SDK's optimized publish | Set `<UseOptimizedPublish>false</UseOptimizedPublish>`. |
| `ThemeSwitcher` switches without its transition | The window has no overlay layer | Give the window's template a `VisualLayerManager` with `EnableOverlayLayer="True"`. |
| Headings and code use the system font | The fonts are not registered | Call `WithInterFont()` and `WithArcFonts()` on the `AppBuilder`. |
| Wrong number or date formats, or exceptions while formatting | `InvariantGlobalization` is enabled | Remove it from the project file. |
| `'Button' is an ambiguous reference` | `TKCode.Arc` and `Avalonia.Controls` are both imported | Alias one of them, as shown above. |
| Restore warnings about Avalonia versions | Another package pulls a different Avalonia | Reference Avalonia `12.1.4` explicitly in the application. |
| Blocks, `DataGrid` or Special types are not found | The package is not referenced or not registered | Add `TKCode.Arc.Blocks`, `TKCode.Arc.AdvancedCharts` or `TKCode.Arc.Special` and call `UseArcBlocks()`, `UseArcAdvancedCharts()` or `UseArcSpecial()` in `UseArc(...)`. |

## Next steps

- **Theming**: light and dark, the eight accents and the design tokens.
- **Motion**: motion levels and reduced motion.
- The component pages: start with Button, Input and Card, which most screens are built from.
- **Changelog**: *Migrating from 0.3.1 to 0.3.2* lists every change for applications that used the AtomUI-based 0.3
  releases.
