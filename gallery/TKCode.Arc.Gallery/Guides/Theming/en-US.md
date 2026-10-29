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

The theme runtime is part of `TKCode.Arc.Foundation`, which `TKCode.Arc` brings in:

1. **The theme manager** (`ArcThemeManager`, one per application, created by `UseArc(...)`) owns the active theme and
   publishes its tokens to the application's resources.
2. **The theme state** (`ArcThemeState`) is everything a theme is computed from: the appearance (`Light`, `Dark` or
   `System`), the accent seed, the motion level, theme presets and token overrides. It is immutable; an
   `ArcThemeBuilder` describes a change, and what a change does not mention stays as it was.
3. **The ArcDesign identity** holds Arc's own tokens: colors, radii, type, spacing and motion.
4. **Control tokens** hold the geometry of individual controls (paddings, widths, offsets) and colors that do not
   depend on the accent.

When a theme is applied, every token is recalculated for the appearance and the accent, only the values that changed
are written, and every control that reads a token updates in place: no page reload, no restart.

## Light and dark

Set the theme the application starts with in `UseArc(...)`, and change it later with `ArcTheme.Apply`:

```csharp
using TKCode.Arc;

// Program.cs: the first frame is drawn with this theme.
.UseArc(arc => arc
    .UseArcControls()
    .WithTheme(t => t.Appearance(ArcAppearance.System).Accent(ArcAccentPreset.Violet)))

// At run time: only the appearance changes, the accent and every other setting stay.
public static void ApplyAppearance(bool dark)
    => ArcTheme.Apply(t => t.Appearance(dark ? ArcAppearance.Dark : ArcAppearance.Light));
```

`ArcAppearance.System` follows the operating system's light or dark setting and switches with it. `ArcTheme.State` is
the current state, and `ArcThemeManager.Current` the manager: its `IsDark` says which appearance is showing, and
`StateChanged` is raised after every change. `ArcThemeManager.ApplyAsync` computes the tokens on a background thread and
publishes them on the UI thread; await it, and never block on it (`.Wait()`, `.Result`) on the UI thread, which
deadlocks. A change that names an unknown token or an invalid value throws `ArcThemeException` listing every problem,
and nothing is changed.

Keep the user's choice (light, dark, or following the system) in your settings store and pass it to `WithTheme` at
start-up, so the first frame already has the right appearance. The gallery's Appearance panel works exactly this way.

### The ThemeSwitcher control

For a header or a toolbar, `ThemeSwitcher` is a ready-made light and dark switch with Arc's page transitions (`Reveal`,
`Eclipse`, `Split` and `Rise`, set with `Variant`). Pressing it raises `ThemeChangeRequested`. If no handler marks the event
handled, the switcher changes the appearance through `ArcThemeManager` itself and keeps the rest of the current theme. Handle the event
when the application owns the appearance, for example to save the choice:

```csharp
themeSwitcher.ThemeChangeRequested += async (_, e) =>
{
    e.Handled = true;
    await settings.SaveAppearanceAsync(e.Next);
    ApplyAppearance(e.Next == ThemeSwitcherAppearance.Dark);
};
```

The transition draws on the window's overlay layer. A window whose template has no `VisualLayerManager` with
`EnableOverlayLayer="True"` switches without it.

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
ArcTheme.Apply(t => t.Accent(Color.Parse("#0E7C86")));
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

`Token(name, value)` overrides the final value of an ArcDesign token, `Token(identity, name, value)` one of a control's
tokens, and `MotionLevel` sets the motion level for the whole theme:

```csharp
ArcTheme.Apply(t => t
    .Accent(ArcAccentPreset.Blue)
    .Token(nameof(ArcDesignTokenKind.Danger), "#D92D20")
    .Token("Switch", "TrackWidth", "50")
    .MotionLevel(ArcMotionLevel.Reduced));
```

Values are text, parsed for the token's type (`#RRGGBB`, `12`, `4,8`, `120ms`, an enum member). The identity of a control
token is its control name; the mobile controls use `MobileButton`, `MobileCheckbox`, `MobileDialog`, `MobileSwitch` and
`MobileToast`. `ClearTokens()` removes every override.

Change as little as possible. A token change applies everywhere: overriding `Danger` changes every error message,
destructive button and failed status in the application. Overrides of colors should be checked in both appearances,
because the override is not recalculated for dark mode.

## Corner style

Arc's corners are round: 18 for controls, 26 for panels, 34 for surfaces. If your product wants squarer corners, change
the corner style instead of overriding the four `Radius*` tokens. Those four are only the top layer: the radius steps
used inside templates, the radius tokens of about 300 control properties (a checkbox box, a combobox option, the
highlight behind a hovered menu row) and the radii that some controls compute while they animate are separate values.
Setting only the four leaves popups, hover backgrounds and checkboxes round.

The `TKCode.Arc.Themes.Corners` package sets all of them with one call and changes nothing but corners: colors, borders,
shadows and motion stay as they are. Register it with `UseArcCorners()` in `UseArc(...)`, then:

```csharp
using TKCode.Arc.Themes.Corners;

ArcTheme.Apply(t => t
    .Accent(ArcAccentPreset.Blue)
    .Corners(ArcCornerStyle.Square));
```

| Style | Result |
|---|---|
| `ArcCornerStyle.Default` | Arc as shipped. Nothing is written. |
| `ArcCornerStyle.Scale(0.5)` | Every radius is multiplied by the factor. `Scale(0)` is `Square`, `Scale(1)` is `Default`, a factor above 1 rounds more. |
| `ArcCornerStyle.Square` | Every corner is square. |

- **Pills and circles.** Radii of 999 and more (pill buttons, round avatars, dots) keep their shape under `Scale`. Only
  `Square` flattens them, so a square style turns round avatars into squares. Use `Scale` to keep them round.
- **Presets.** A corner style is a theme preset (`ArcThemePreset`): a named set of token overrides for light and dark.
  `Corners(style)` is `Preset(ArcCornerPresets.Create(style))`; `ArcCornerPresets.Square` and `ArcCornerPresets.Half`
  are ready-made. A preset covers every Arc package and skips the ones the application has not registered.
- **Replacing.** A corner style fills one slot (`ArcCornerPresets.Slot`), so the next `Corners(...)` replaces it and
  `Corners(ArcCornerStyle.Default)` removes it. Your own `Token(...)` overrides are applied after presets and win.
- **Scroll bars.** Scroll bars keep their thin pill shape under every corner style.
- **Pixel.** The `TKCode.Arc.Themes.Pixel` package (`UseArcPixelTheme()`) adds the Pixel style: square corners, hard
  shadows, a limited palette for light and dark and pixel fonts. Apply it with `ArcTheme.Apply(t => t.Pixel())` and
  go back with `t.RemovePreset(ArcPixelPresets.Slot)`. It has its own slot, so a corner style chosen after it changes
  its corners.

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
