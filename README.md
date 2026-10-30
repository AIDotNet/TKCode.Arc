# TKCode.Arc

English | [简体中文](README.zh-CN.md)

Arc-style components for Avalonia.
TKCode.Arc ports the free components of [Arc](https://uiarc.dev) — 112 components in 22 groups —
with Arc's visual language and spring motion.

- Targets `net10.0` and `net11.0`; desktop (Windows, macOS, Linux).
- Depends on Avalonia 12.1.4 (exact version), on `TKCode.Arc.Foundation` (its own MIT theme, token and language
  runtime) and on [StreamMark](https://www.nuget.org/packages/TKCode.StreamMark.Highlighting.TextMate) for Markdown and
  code highlighting. Every package in the chain is MIT licensed; there is no AtomUI dependency since 0.3.2.
- Design documents: [design/overview.md](design/overview.md).
- Live demo: <https://docs.tkcode.si/> (the gallery of every component, in the browser).

## Packages

| Package | Contents |
|---|---|
| `TKCode.Arc` | The components (`UseArcControls()`), organized like Arc's sidebar, plus a streaming `Markdown` control. |
| `TKCode.Arc.Core` | ArcDesign tokens and palette, the spring motion engine, shared primitives, 187 built-in Lucide glyphs. |
| `TKCode.Arc.Foundation` | The theme, token, language and package runtime (`UseArc(...)`, `ArcTheme`, `ArcLanguage`); from 0.3.2. |
| `TKCode.Arc.Icons.Lucide` | The full Lucide set (2155 icons): `LucideIcon` control and `LucideGeometry` markup extension. |
| `TKCode.Arc.Fonts` | Geist and Geist Mono (`WithArcFonts()`); body text uses Inter from `Avalonia.Fonts.Inter`. |

## Install

The latest preview is [`0.3.2-preview.2`](CHANGELOG.md) on [NuGet](https://www.nuget.org/packages/TKCode.Arc):

```bash
dotnet add package TKCode.Arc --version 0.3.2-preview.2
dotnet add package TKCode.Arc.Fonts --version 0.3.2-preview.2           # optional: Geist and Geist Mono
dotnet add package TKCode.Arc.Icons.Lucide --version 0.3.2-preview.2    # optional: the full Lucide set
```

`TKCode.Arc` brings in `TKCode.Arc.Core` (and, from 0.3.2, `TKCode.Arc.Foundation`). `TKCode.Arc.Blocks`, `TKCode.Arc.AdvancedCharts` and `TKCode.Arc.Special`
(the animated AI explainers of the Special components section) are separate packages on NuGet; `TKCode.Arc.Workflow`
(the workflow canvas, `UseArcWorkflow()`) ships with them too. `TKCode.Arc.Themes.Pixel` (a pixel-art theme, `UseArcPixelTheme()`) and
`TKCode.Arc.Mobile` (the first touch-first controls, `UseArcMobile()`, not yet verified on devices) are new in 0.3.1-preview.1; `TKCode.Arc.Themes.Corners` (global corner style, `UseArcCorners()`) is new in 0.3.1-preview.2. `TKCode.Arc.Terminal` (`TerminalView` over libghostty-vt, native libraries bundled) is new in 0.3.1-preview.4. `TKCode.Arc.Audio` (the `IAudioPlayer` abstraction behind the media blocks, silent by default) with its backends `TKCode.Arc.Audio.Native` (desktop, miniaudio, `ArcAudioNative.Use()`) and `TKCode.Arc.Audio.Browser` (WebAssembly, `ArcAudioBrowser.UseAsync()`) is new in 0.3.2-preview.1. Versions carry a `-preview` suffix, so the API may still change between
releases.

## Getting started

The code below is the API from 0.3.2-preview.1 on. Apps on 0.3.1 and earlier register Arc inside `UseAtomUI(...)`;
[CHANGELOG.md](CHANGELOG.md) has the migration notes.

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .WithArcFonts()          // optional: Geist display type and Geist Mono code type
    .UseArc(arc => arc
        .UseArcControls()
        .WithTheme(t => t.Appearance(ArcAppearance.System)));

// Later, for example from a settings page:
ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark).Accent(ArcAccentPreset.Violet));
```

```xml
<Window xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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
<Window xmlns:lucide="https://github.com/AIDotNet/TKCode.Arc/lucide">
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

Markdown and code: `CodeBlock` highlights out of the box (TextMate grammars painted in Arc's palette), and `Markdown`
renders a document or a streamed reply with the same styling:

```csharp
var reply = new Markdown();
reply.Append("Here is the **fix**:\n\n```csharp\n");
reply.Append("var total = items.Sum(i => i.Price);\n```\n");
reply.Complete();
```

Notes for applications:

- Do not enable `InvariantGlobalization` (number and date formatting needs culture data).
- NativeAOT with the .NET 11 SDK: set `<UseOptimizedPublish>false</UseOptimizedPublish>`, otherwise the compiled XAML
  is left out of the published app.
- Anchored popups (menus, selects, tooltips, popovers) are drawn in the window's overlay layer, so they sit exactly
  `sideOffset` away from their trigger and never cover it.
- `Button`, `Calendar`, `Carousel`, `ContextMenu`, `DatePicker`, `Slider`, `SplitButton`, `TabItem`, `TimePicker`,
  `TreeView` and `TreeViewItem` share names with `Avalonia.Controls`; alias one namespace when a file imports both.

## Building

Requires the .NET 11 SDK (see `global.json`) and the .NET 10 runtime (the tests and the gallery run on `net10.0`).

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Tests
dotnet run --project gallery/TKCode.Arc.Gallery.Desktop -f net10.0
```

## Sponsors

TKCode.Arc is mainly sponsored by [nextco.work](https://nextco.work/), which provides affordable, easy-to-use
access to Claude, OpenAI and DeepSeek models. Thank you for supporting the project.

## License

MIT. Arc depends only on Avalonia (MIT) and MIT packages of its own, so closed-source applications can ship it in a
single file or a NativeAOT build. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for Arc (MIT), Lucide (ISC),
fonts (OFL), StreamMark, TextMateSharp, Oniguruma and Markdig.
