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
| AtomUI | `6.2.7` | `AtomUI.Desktop.Controls`; Arc's theme and token system sit on AtomUI. |
| Fonts (optional) | `Avalonia.Fonts.Inter` | Arc's body text uses Inter; Geist comes from `TKCode.Arc.Fonts`. |

TKCode.Arc is in preview, and AtomUI and Avalonia are pinned to exact versions. Use the same Avalonia version in your
application; mixing versions leads to restore warnings and, at run time, to missing styles or type load errors.

## 1. Add the packages

```bash
dotnet add package TKCode.Arc --version 0.2.0-preview.1
dotnet add package TKCode.Arc.Fonts --version 0.2.0-preview.1           # optional: Geist and Geist Mono
dotnet add package TKCode.Arc.Icons.Lucide --version 0.2.0-preview.1    # optional: the full Lucide set
dotnet add package Avalonia.Fonts.Inter --version 12.1.4                 # optional: Inter for body text
```

`TKCode.Arc` brings in `TKCode.Arc.Core`, AtomUI and StreamMark. If your solution uses central package management, add
the versions to `Directory.Packages.props` once and keep the project files version-free:

```xml
<ItemGroup>
    <PackageVersion Include="TKCode.Arc" Version="0.2.0-preview.1" />
    <PackageVersion Include="TKCode.Arc.Fonts" Version="0.2.0-preview.1" />
    <PackageVersion Include="TKCode.Arc.Icons.Lucide" Version="0.2.0-preview.1" />
    <PackageVersion Include="Avalonia.Fonts.Inter" Version="12.1.4" />
</ItemGroup>
```

Pin the exact preview version. Floating versions such as `0.1.*` can pick up a preview with breaking changes on the next
restore.

## 2. Register Arc in the application builder

Arc is registered in AtomUI's builder, next to AtomUI's desktop controls. A typical `Program.cs`:

```csharp
using AtomUI;
using AtomUI.Desktop.Controls;
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
                  .UseAtomUI(builder =>
                  {
                      builder.UseDesktopControls();
                      builder.UseArc();
                  });
}
```

What each call does:

| Call | Purpose |
|---|---|
| `UseAtomUI(...)` | Starts AtomUI's theme manager, design tokens and localization for the application. |
| `UseDesktopControls()` | AtomUI's desktop control package, which Arc builds on. |
| `UseArc()` | Registers Arc's controls, themes and tokens. It also registers `TKCode.Arc.Core` (`UseArcCore()`), so you do not call that yourself. |
| `WithInterFont()`, `WithArcFonts()` | Register the fonts Arc's type scale is designed for. Without them, text falls back to the system font. |

Declare the `Application` class `partial`. AtomUI's source generator adds its startup code to it, and the build stops
with `ATOMUILOC008` when the class is not partial:

```csharp
public partial class App : Application
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

## 3. Use the controls in XAML

Arc's XAML namespace is `https://github.com/AtomUICommunity/TKCode.Arc`; every control, token resource and markup
extension lives there.

![A notification settings card built in the first minutes with Arc](images/FirstScreen.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
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
             xmlns:lucide="https://github.com/AtomUICommunity/TKCode.Arc/lucide">
    <lucide:LucideIcon Kind="Sparkles" Size="20" />
</UserControl>
```

### Blocks and the data grid

`TKCode.Arc.Blocks` (application shell and settings blocks) and `TKCode.Arc.AdvancedCharts` (the `DataGrid`) are not
on NuGet yet. Reference the projects from a clone of the repository and register them next to `UseArc()`:

```csharp
builder.UseDesktopControls();
builder.UseArc();
builder.UseArcBlocks();          // TKCode.Arc.Blocks
builder.UseArcAdvancedCharts();  // TKCode.Arc.AdvancedCharts
```

### Languages

AtomUI handles the application language. To offer more than one, declare them in the builder; the gallery offers
English, Simplified Chinese and Traditional Chinese this way:

```csharp
builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]);
```

Arc controls ship English defaults for their built-in text (for example "Show password" or "Clear search"). The
controls expose that text as properties or label objects, so you can translate it from your own resources.

## Publishing

- **NativeAOT and trimming.** The libraries are trimming- and AOT-compatible and use no reflection. Publish a desktop
  app as a single self-contained file with `-p:PublishAot=true`, or `-p:PublishSingleFile=true --self-contained` when
  you do not use AOT.
- **Globalization.** Do not enable `InvariantGlobalization`. Number, currency and date formatting in the inputs,
  pickers and charts needs culture data.
- **macOS.** The repository's `tools/macos` scripts show how the gallery is wrapped into a signed and notarized `.app`;
  the same flow applies to your application.

## Building from source

Building the repository needs the .NET 11 SDK (see `global.json`) and the .NET 10 runtime, which AtomUI's build tasks
run on:

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Tests
dotnet run --project gallery/TKCode.Arc.Gallery.Desktop -f net10.0
```

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Arc controls appear unstyled or empty | `UseArc()` is not called in `UseAtomUI(...)` | Register it after `UseDesktopControls()`. |
| Build error `ATOMUILOC008` | The `Application` class is not `partial` | Declare it `public partial class App : Application`. |
| Headings and code use the system font | The fonts are not registered | Call `WithInterFont()` and `WithArcFonts()` on the `AppBuilder`. |
| Wrong number or date formats, or exceptions while formatting | `InvariantGlobalization` is enabled | Remove it from the project file. |
| `'Button' is an ambiguous reference` | `TKCode.Arc` and `Avalonia.Controls` are both imported | Alias one of them, as shown above. |
| Restore warnings about Avalonia versions | Another package pulls a different Avalonia | Reference Avalonia `12.1.4` explicitly in the application. |
| Blocks or `DataGrid` types are not found | The packages are not on NuGet yet | Reference the projects from the repository and call `UseArcBlocks()` or `UseArcAdvancedCharts()`. |

## Next steps

- **Theming**: light and dark, the eight accents and the design tokens.
- **Motion**: motion levels and reduced motion.
- The component pages: start with Button, Input and Card, which most screens are built from.
