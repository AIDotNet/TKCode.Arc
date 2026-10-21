---
title: Theming
---

# Theming

Every color, radius, shadow, font size and motion timing in TKCode.Arc comes from one set of design tokens, the
ArcDesign tokens. Controls never hard-code a value; they read tokens, so changing the appearance (light or dark), the
accent or a single token restyles the whole application at once and keeps every control consistent with the others.

This page explains how the theme is built, how to switch it at run time, which tokens exist, and how to use them in
your own views and controls.

## How the theme is built

TKCode.Arc sits on AtomUI's theme system:

1. **AtomUI's theme manager** (`IThemeManager`) owns the active theme and applies it to the application. A theme is a
   `ThemeConfig` built with `ThemeConfigBuilder`.
2. **Algorithms** decide the appearance: `ThemeAlgorithm.Default` for light, plus `ThemeAlgorithm.Dark` for dark.
3. **The ArcDesign identity** holds Arc's own tokens. `WithArc(...)` on the builder sets the accent, overrides tokens
   and sets the motion mode for that identity.
4. **Control tokens** hold the geometry of individual controls (paddings, widths, offsets) and colors that do not
   depend on the accent.

When a theme is applied, every token is recalculated for the appearance and the accent, and every control that reads a
token updates in place: no page reload, no restart.

## Light and dark

Build a theme config and apply it through the theme manager:

```csharp
using AtomUI;
using AtomUI.Theme;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using TKCode.Arc;

public static async Task ApplyAppearanceAsync(bool dark, ArcAccentPreset accent)
{
    var builder = new ThemeConfigBuilder();
    builder = dark
        ? builder.WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
        : builder.WithAlgorithms(ThemeAlgorithm.Default);
    builder = builder.WithArc(arc => arc.Accent(accent));

    var manager = Application.Current!.GetThemeManager()!;
    await manager.ApplyThemeAsync(new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, builder.Build(),
                                                   ThemeTransitionReason.UserRequest));
}
```

Keep the user's choice (light, dark, or following the system) in your settings store and apply it at start-up, before
the main window is shown, so the first frame already has the right appearance. The gallery's Appearance panel works
exactly this way.

### The ThemeSwitcher control

For a header or a toolbar, `ThemeSwitcher` is a ready-made light and dark switch with Arc's page transitions (`Reveal`,
`Eclipse`, `Split` and `Rise`, set with `Variant`). Pressing it raises `ThemeChangeRequested`. If no handler marks the event
handled, the switcher toggles `ThemeAlgorithm.Dark` itself and keeps the rest of the current theme. Handle the event
when the application owns the appearance, for example to save the choice:

```csharp
themeSwitcher.ThemeChangeRequested += async (_, e) =>
{
    e.Handled = true;
    await settings.SaveAppearanceAsync(e.Next);
    await ApplyAppearanceAsync(e.Next == ThemeSwitcherAppearance.Dark, settings.Accent);
};
```

## Accents

Arc ships eight accent presets. The accent drives buttons, selection, focus rings, switches, sliders, chart series and
links; everything else stays neutral, so changing the accent changes the brand color without changing the layout or
the contrast of text.

![The eight accent presets in their light and dark tones](images/AccentPresets.png)

```xml
<StackPanel Width="720" Spacing="10">
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Grid.Column="1" Text="Light" Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <TextBlock Grid.Column="2" Text="Dark" Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Neutral" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#353535" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#BEBEBE" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Violet" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#7747FF" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#A78BFF" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Blue" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#0562EF" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#6BA3FF" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Green" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#0DB879" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#53DCA6" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Amber" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#F3AD20" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#FFCB62" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Orange" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#F48120" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#FFAD5E" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Coral" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#F15F55" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#FF9386" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Rose" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#ED4E9D" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#FF8BC5" />
    </Grid>
</StackPanel>
```

| Preset | Seed | Accent, light | Accent, dark | Strong, light |
|---|---|---|---|---|
| `Neutral` (default) | `#353535` | `#353535` | `#BEBEBE` | `#1F1F1F` |
| `Violet` | `#7747FF` | `#7747FF` | `#A78BFF` | `#5528CE` |
| `Blue` | `#0562EF` | `#0562EF` | `#6BA3FF` | `#074DB7` |
| `Green` | `#0DB879` | `#0DB879` | `#53DCA6` | `#087C54` |
| `Amber` | `#F3AD20` | `#F3AD20` | `#FFCB62` | `#9C6300` |
| `Orange` | `#F48120` | `#F48120` | `#FFAD5E` | `#AA4700` |
| `Coral` | `#F15F55` | `#F15F55` | `#FF9386` | `#B92C27` |
| `Rose` | `#ED4E9D` | `#ED4E9D` | `#FF8BC5` | `#AE2570` |

Each preset has hand-tuned values for both appearances: the accent, a stronger shade for text and pressed states, a
subtle tint for selected backgrounds, the color of text on the accent, and the colors of switches and checkboxes.

### A brand color

When none of the presets matches the brand, pass the brand color itself. Arc derives the light and dark accent family
from it in the OKLCH color space, so the steps stay perceptually even:

```csharp
builder = builder.WithArc(arc => arc.Accent(Color.Parse("#0E7C86")));
```

Check the result in both appearances. Very light seeds (yellow, lime) make poor text colors on white; prefer a preset
or a darker shade of the brand for those.

## Design tokens

Tokens are grouped by what they describe. The names below are the members of `ArcDesignTokenKind`, which is also how
XAML refers to them.

![Surfaces, text levels, status colors and radii, read from the tokens of the current appearance](images/TokenPreview.png)

```xml
<StackPanel Width="720" Spacing="16">
    <UniformGrid Columns="4">
        <Border Height="84" Margin="0,0,8,0" Padding="12" CornerRadius="{arc:ArcDesignTokenResource RadiusControl}"
                Background="{arc:ArcDesignTokenResource Background}"
                BorderBrush="{arc:ArcDesignTokenResource Border}" BorderThickness="1">
            <TextBlock Text="Background" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </Border>
        <Border Height="84" Margin="4,0" Padding="12" CornerRadius="{arc:ArcDesignTokenResource RadiusControl}"
                Background="{arc:ArcDesignTokenResource Surface}"
                BorderBrush="{arc:ArcDesignTokenResource Border}" BorderThickness="1">
            <TextBlock Text="Surface" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </Border>
        <Border Height="84" Margin="4,0" Padding="12" CornerRadius="{arc:ArcDesignTokenResource RadiusControl}"
                Background="{arc:ArcDesignTokenResource SurfaceMuted}"
                BorderBrush="{arc:ArcDesignTokenResource Border}" BorderThickness="1">
            <TextBlock Text="SurfaceMuted" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </Border>
        <Border Height="84" Margin="8,0,0,0" Padding="12" CornerRadius="{arc:ArcDesignTokenResource RadiusControl}"
                Background="{arc:ArcDesignTokenResource SurfaceRaised}"
                BorderBrush="{arc:ArcDesignTokenResource Border}" BorderThickness="1">
            <TextBlock Text="SurfaceRaised" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </Border>
    </UniformGrid>
    <StackPanel Spacing="4">
        <TextBlock Text="Foreground: titles and primary text" Foreground="{arc:ArcDesignTokenResource Foreground}" FontSize="16" />
        <TextBlock Text="TextSecondary: descriptions and secondary values" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <TextBlock Text="TextMuted: hints, captions and placeholders" Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <StackPanel Orientation="Horizontal" Spacing="8">
        <arc:Badge Content="Success" Tone="Success" />
        <arc:Badge Content="Warning" Tone="Warning" />
        <arc:Badge Content="Danger" Tone="Danger" />
        <arc:Button Content="Accent" />
        <arc:Button Content="Secondary" Variant="Secondary" />
    </StackPanel>
</StackPanel>
```

| Group | Tokens | Values and use |
|---|---|---|
| Surfaces | `Background`, `Surface`, `SurfaceMuted`, `SurfaceRaised`, `Scrim` | The page, cards and fields, muted wells, raised menus and popups, the dim layer behind dialogs. |
| Text | `Foreground`, `TextSecondary`, `TextMuted` | Three levels of emphasis. Use `TextMuted` only for hints, never for information a reader must see. |
| Borders | `Border`, `BorderStrong`, `BorderSubtle` | Card outlines, field frames, dividers. |
| Accent | `Accent`, `AccentStrong`, `AccentSubtle`, `AccentForeground` | Brand color, its strong shade for text, its tint for selected backgrounds, text on the accent. |
| Controls | `ControlOn`, `ControlOnSubtle`, `ControlThumb`, `ControlThumbOn`, `ControlTrack`, `ControlTrackHover`, `ControlGlyph`, `ControlFill`, `ControlThumbShadow` | Switches, checkboxes, radios and sliders. |
| Status | `Success`, `Warning`, `Danger` and their `…Subtle` tints | Results, warnings and errors; the tints are for badge and alert backgrounds. |
| Data | `Series1` to `Series4`, `Neutral0` to `Neutral11` | Chart series that follow the accent, and a neutral ramp for charts and illustrations. |
| Focus | `FocusRing`, `FocusRingWidth` (2), `FocusRingOffset` (2) | The keyboard focus ring. |
| Shape | `RadiusControl` (18), `RadiusPanel` (26), `RadiusSurface` (34), `RadiusPill` | Fields and buttons, cards and panels, large surfaces, pills and badges. |
| Size | `ControlSizeSM` (36), `ControlSizeMD` (44), `ControlSizeLG` (50); `GlyphSizeSM` (16), `GlyphSizeMD` (20), `GlyphSizeLG` (24); `IconStrokeWidth` | Control heights for the `Small`, `Medium` and `Large` sizes, and icon sizes. |
| Spacing | `Space1` (4) to `Space24` (96) | A 4 px based spacing scale: 4, 8, 12, 16, 20, 24, 32, 40, 48, 64, 80, 96. |
| Type | `TypeXS` (12), `TypeSM` (14), `TypeBase` (16), `TypeLG` (18), `TypeXL` (22), `Type2XL` (28), `Type3XL` (36), `Type4XL` (52), `Type5XL` (72) | The type scale. |
| Type style | `BodyFontFamily`, `DisplayFontFamily`, `MonoFontFamily`, `WeightRegular`, `WeightMedium`, `LeadingBody` (1.4), `LeadingDisplay` (1.1), `TrackingBody`, `TrackingDisplay` | Families, weights, line height and letter spacing for body and display text. |
| Elevation | `ShadowResting`, `ShadowRaised`, `ShadowFloating` | Cards at rest, raised cards and menus, floating dialogs and popovers. Dark mode uses deeper shadows. |
| Effects | `BlurSubtle`, `BlurSoft`, `BlurText`, `BackdropBlurDialog`, `BackdropBlurDrawer`, `ReduceTransparency` | Motion blur steps and the frosted backdrops of dialogs and drawers. |
| Motion | `Duration…`, `Ease…`, `Spring…`, `Stagger…`, `MotionMode` | Timings, curves, spring presets and the motion level. See **Motion**. |

## Using tokens in your views

### In XAML

`ArcDesignTokenResource` reads a token and follows theme changes:

```xml
<Border Padding="16"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <StackPanel Spacing="4">
        <TextBlock Text="Usage this month"
                   FontSize="{arc:ArcDesignTokenResource TypeLG}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="182 of 250 GB"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
</Border>
```

### In code

`ArcTokens.Bind` binds a property to a token so it updates with the theme; `ArcTokens.Get` and `ArcTokens.GetColor`
read the current value once, for example to draw in a custom `Render`:

```csharp
ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
ArcTokens.Bind(title, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);

var accent = ArcTokens.GetColor(chart, ArcDesignTokenKind.Accent);
```

Prefer `Bind` over `Get` for anything that stays on screen: a value read once does not change when the user switches
to dark mode.

### Control tokens

Each Arc control has its own token class for its geometry, read in XAML as `{arc:<Control>TokenResource Name}`. For
example, all text fields share `{arc:FieldTextBoxTokenResource SelectionFill}` for the selection color. Control tokens
hold values that do not depend on the accent, so they rarely need to change; when you write a control that should look
like an Arc field, read these instead of copying the numbers.

## Changing tokens

`WithArc` can override the final value of any ArcDesign token, and set the motion mode for the whole theme:

```csharp
builder = builder.WithArc(arc => arc
    .Accent(ArcAccentPreset.Blue)
    .Token(nameof(ArcDesignTokenKind.Danger), "#D92D20")
    .MotionMode(ArcMotionLevel.Reduced));
```

Change as little as possible. A token change applies everywhere: overriding `Danger` changes every error message,
destructive button and failed status in the application. Overrides of colors should be checked in both appearances,
because the override is not recalculated for dark mode.

## Styling a single control

For local adjustments, use Avalonia's normal tools instead of tokens:

- **Properties**: `Width`, `Margin`, `HorizontalAlignment` and the documented properties of each control.
- **Styles**: a `Style` with a selector such as `arc|Button.toolbar` changes every button with that class in a scope.
- **Control themes**: a `ControlTheme` with `BasedOn` the Arc theme replaces the template of one kind of control in
  your application while keeping Arc's states.

Keep colors in these styles token-based (`{arc:ArcDesignTokenResource ...}`) so they still follow the appearance.

## Guidelines

**Do**

- Read every color from a token. A single hex value in a view is a bug in dark mode.
- Use the accent for the one action or state that matters on a screen; leave the rest neutral.
- Pair status colors with words or icons. A red border alone does not explain an error, and some readers cannot see
  the difference between red and green.
- Test each screen in light and dark, with at least two accents, before shipping.

**Avoid**

- Using `TextMuted` for information people must read: it has the lowest contrast of the three text levels and is
  meant for short hints and captions.
- Overriding many tokens to restyle the library into a different design system; the controls' spacing, radii and
  motion are tuned together.
- Hard-coding `#FFFFFF` for text on an accent background; use `AccentForeground`, which is dark on light accents such
  as Amber.
