---
title: Icon morph
summary: Icons that morph between states: menu to close, play to pause, copy to check, and more.
---

<Demo name="EightPairs" />

## When to use

`IconMorph` draws the eight icon pairs that products use for a state change: a menu that becomes a close icon, a play
button that becomes pause, a copy icon that becomes a check. It sits inside a button and shows the state the way a
person would see it change, so the change reads as one action rather than two icons swapping. It belongs in the
toolbars, headers and rows of an application, where the icon is the only thing a person looks at.

- **A live-data header in an operations dashboard**: a play and pause control that freezes the metrics stream while
  someone reads a spike. The icon morphs, so the person sees that the stream stopped.
- **A notification switch on an incident or order page**: a bell that rings once when notifications are turned on and
  is struck through when they are off, for a team that follows a single incident.
- **A disclosure control in a settings or billing list**: a chevron that turns when an invoice group or a filter
  panel opens, so the row shows its state without a separate label.
- **A navigation toggle in a SaaS app shell**: a menu icon that becomes a close icon when the side navigation opens on
  a narrow window.
- **A like or favourite on a shared report**: a heart that fills with a short burst when a teammate marks a report as
  useful.

## When not to use

- **A copy action with a confirmation**: use `CopyButton`, which already morphs its icon and announces the result.
- **A light and dark switch with a label**: use `ThemeSwitcher`, which carries the text and the state together.
- **An icon that never changes state**: use a plain Lucide icon from `ArcIcons` in the button. A morph with no second
  state adds nothing.
- **The icon as the only control**: `IconMorph` is decorative and never takes focus, so put it inside a `Button` or
  another control that has a name and a state.
- **A menu of several actions behind one icon**: use `DropdownMenu` with its trigger. The morph only shows that one
  thing is open or closed.

## Usage

`IconMorph` draws one of eight icon pairs. `Kind` selects the pair and `IsActive` selects the second state. Put it
inside a button and name the button.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Button Variant="Ghost" AutomationProperties.Name="Open menu">
        <arc:IconMorph Kind="Menu" />
    </arc:Button>
</UserControl>
```

`IconMorph` derives from Avalonia's `Control`. It draws its glyph in `Render` on a 24 by 24 grid, so it has no template
and no parts to restyle. Its `IsActive` property is two-way by default, which makes it a natural target for a view
model property. The control does not handle pointer or keyboard input: clicks and key presses go to the button that
hosts it.

### Anatomy

1. **Glyph** (`Kind`): one drawn shape, on a 24-unit grid, that morphs between its inactive and active forms.
2. **Stroke** (`StrokeThickness`): 1.75 on the grid by default. It scales with `IconSize`, so the icon keeps its weight
   at 16 px, 24 px and 32 px.
3. **Size** (`IconSize`): the rendered size in pixels, 24 by default. The theme sets the same default.
4. **Color** (`Foreground`): inherited from the parent like the text foreground, so a ghost button colors its icon with
   its own text color.
5. **Liked color** (`LikeBrush`): the color of a liked heart. The theme sets it to the danger color.
6. **Moment**: the bell's ring and the heart's ring and sparks. A moment plays once each time the icon is turned on and
   has no visible part in the other states.

### Kinds

`Menu` morphs to close, `Play` to pause, `Plus` to check, `Copy` to check, `Theme` to the moon, `Chevron` turns,
`Bell` turns notifications on and off, and `Heart` likes. The active state of each pair is the second item in its name:
a plus becomes a check, a bell turns on, a heart is liked.

```xml
<arc:IconMorph Kind="Play" />
<arc:IconMorph Kind="Theme" IsActive="True" />
<arc:IconMorph Kind="Heart" IsActive="{Binding IsLiked}" />
```

Choose the pair by the meaning of the state, not by the look of the icon. `Plus` to `Check` fits an "add to list"
action whose result is a checked item. `Copy` to `Check` fits a copy action. `Play` to `Pause` is for a stream or a
clip that the person starts and stops, not for a one-way action.

### Toggle state

Bind `IsActive` to the state of the button. Flip it in the click handler, and update the name of the button to match
the new meaning.

```csharp
var icon = new IconMorph { Kind = IconMorphKind.Play };
var button = new Button { Content = icon, Variant = ButtonVariant.Ghost };
button.Click += (_, _) =>
{
    icon.IsActive = !icon.IsActive;
    AutomationProperties.SetName(button, icon.IsActive ? "Pause" : "Play");
};
```

### Size and stroke

`IconSize` sets the rendered size in pixels; the default is 24. `StrokeThickness` is set on the 24-unit grid and scales
with `IconSize`, so the default stroke of 1.75 stays in proportion at any size.

<Demo name="Sizes" />

```xml
<arc:IconMorph Kind="Menu" IconSize="16" />
<arc:IconMorph Kind="Menu" IconSize="32" StrokeThickness="2" />
```

Match the icon to the button. A 16 px icon fits a 36 px button, a 24 px icon fits 44 px, and a 32 px icon fits the
52 px hero buttons. A 24 px icon in a 28 px button looks cramped, and a 16 px icon in a 52 px button looks lost.

### Chevron direction

For `Chevron`, `ChevronPointing` sets the rest direction, and `ChevronTurn` sets the angle in degrees when the icon is
active. A value of 180 flips down to up. A value of 90 turns right to down.

<Demo name="ChevronAndMoments" />

Use `Down` with a 180 turn for a section that expands downward, and `Right` with a 90 turn for a row that opens to the
side, such as a tree node. Keep the same direction for every chevron in one list, so that a row that opens and a row
that does not are easy to tell apart.

### MorphGlyph

`MorphGlyph` is the path morph primitive: it morphs between any two pieces of path data. Menu to close keeps its vertices, and close to check resamples by arc length.

<Demo name="MorphGlyph" />

Use `MorphGlyph` for a shape the eight pairs do not cover. Give it the second shape with `MorphTo`, which animates
from the current outline. Use `Morph` in code when a custom state needs to know that the morph has finished.

```csharp
var glyph = this.FindControl<MorphGlyph>("StatusGlyph")!;
await glyph.MorphTo("M4 12.5l5 5L20 6.5");
```

### Binding to a view model

A dashboard view model owns the live state, and the header only shows it. `IsActive` binds two-way, so the icon can
both show and change the state.

```csharp
public partial class LiveMetricsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveToggleName))]
    private bool _isPaused;

    public string LiveToggleName => IsPaused ? "Resume live updates" : "Pause live updates";

    [RelayCommand]
    private void ToggleLive() => IsPaused = !IsPaused;
}
```

```xml
<arc:Button Variant="Ghost"
            Width="36"
            Height="36"
            Padding="0"
            AutomationProperties.Name="{Binding LiveToggleName}"
            Command="{Binding ToggleLiveCommand}">
    <arc:IconMorph Kind="Play" IsActive="{Binding IsPaused}" IconSize="18" />
</arc:Button>
```

- `IsActive` is two-way. A change in the view model moves the icon, and a change to the icon writes the new state.
  Bind it only to the property that owns the state; a second writer makes the morph flip back and forth.
- The button name comes from the view model, so a screen reader hears the action the next press performs, not the icon.
- The command performs the change. The icon follows the state, and does not run the action itself.

### Using motion with restraint

The morph is the feedback for a state change, so it helps wherever a person has to see that something changed. It
distracts where the same change repeats many times in a row, or where the icon is the whole signal of an action.

- **Helps**: a single toggle that changes what the next action does, such as pause and resume, or menu and close.
- **Distracts**: a bell in a list that fires for every item, or a heart in a feed where people like many posts in a row.
  The burst then plays on every press. Keep the moments for a single, deliberate action.
- **Reduced motion** completes every morph at once and skips the bell's ring and the heart's burst. Test the state
  change without the motion too: the icon and the button name must still tell the person what happened.

### Example: incident header controls

An incident page in an operations console shows the live status of the incident and three controls: pause the live
updates, turn notifications on or off, and expand the list of affected services. Each control is a ghost button with a
name that matches the state the button is in, and the icons morph so the state is visible without a label. The
header keeps a menu icon for less frequent actions, with its name written as the action it opens.

![An incident header with a pause control, a bell, a collapsed chevron and a menu icon](images/IncidentHeader.png)

```xml
<StackPanel Width="600" Spacing="12">
    <Border Padding="20"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
        <StackPanel Spacing="14">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Spacing="2">
                    <TextBlock Text="Incident INC-2026-0412"
                               FontSize="16"
                               FontWeight="SemiBold"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Checkout latency above 900 ms since 14:05 UTC"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <StackPanel Grid.Column="1"
                            Orientation="Horizontal"
                            Spacing="4"
                            VerticalAlignment="Center">
                    <arc:Button Width="36"
                                Height="36"
                                Padding="0"
                                Variant="Ghost"
                                AutomationProperties.Name="Pause live updates">
                        <arc:IconMorph Kind="Play" IsActive="True" IconSize="18" />
                    </arc:Button>
                    <arc:Button Width="36"
                                Height="36"
                                Padding="0"
                                Variant="Ghost"
                                AutomationProperties.Name="Turn notifications off">
                        <arc:IconMorph Kind="Bell" IsActive="True" IconSize="18" />
                    </arc:Button>
                    <arc:Button Width="36"
                                Height="36"
                                Padding="0"
                                Variant="Ghost"
                                AutomationProperties.Name="Open incident actions">
                        <arc:IconMorph Kind="Menu" IconSize="18" />
                    </arc:Button>
                </StackPanel>
            </Grid>
            <StackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock Text="Affected services (3)"
                           FontWeight="Medium"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <arc:Button Width="28"
                            Height="28"
                            Padding="0"
                            Variant="Ghost"
                            AutomationProperties.Name="Collapse affected services">
                    <arc:IconMorph Kind="Chevron" IsActive="True" IconSize="16" />
                </arc:Button>
            </StackPanel>
            <StackPanel Spacing="6">
                <TextBlock Text="Checkout API: p95 1.2 s, error rate 4.8%"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Text="Payment gateway: p95 640 ms, error rate 0.3%"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Text="Session service: healthy"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </StackPanel>
    </Border>
</StackPanel>
```

### Content guidelines

- **Button name**: the action the press performs, as a verb and an object, in sentence case: "Pause live updates",
  "Turn notifications off", "Collapse affected services". When the state changes, the name changes with it.
- **Icon-only buttons**: the name is the only text a screen reader gets, so it must be complete. Do not name a button
  after its icon ("Play icon").
- **Pairs**: use the same pair for the same action everywhere in the app, so that a play control always means play.

| Instead of | Write |
|---|---|
| Play | Pause live updates (while it runs) |
| Menu | Open incident actions |
| Click to expand | Expand affected services |
| Bell icon | Turn notifications on |
| Like button | Like (then Unlike while liked) |

### Best practices

**Do**

- Put the icon inside a button and give the button the name. The icon is decorative, and only the button can be reached
  and named.
- Bind `IsActive` to the state the view model owns, and let the command perform the action.
- Keep one icon size per row, and match it to the button size.
- Change the button name whenever `IsActive` changes, so the name and the icon never disagree.

**Avoid**

- Using a morph for a one-way action with no second state. A copy action that never reverts needs no morph, and
  `CopyButton` already confirms it.
- Putting a heart or a bell burst on a control that people press many times in a row. The repeated moment becomes noise.
- Setting `IsActive` on both the icon and a binding that writes it back. The state flips twice and the morph stutters.
- Relying on the color change alone. The shape changes too; keep the name, so the state is clear without the color.

### Common questions

**Why does my icon not change its color?** `IconMorph` inherits `Foreground` from its parent, as text does. Set the
color on the button or on the icon, or use the theme's ghost button colors. The liked heart uses `LikeBrush`, not
`Foreground`.

**Why does a click on the icon not toggle it?** The icon is not hit-test visible, so the click goes to the button that
hosts it. Put the click handler or the command on the button.

**Does the bell ring again when I turn it on a second time?** Yes. The ring plays once each time `IsActive` becomes
`True`, and turning it off strikes the bell through.

**How do I show a state that is not one of the eight pairs?** Use `MorphGlyph` with its own path data, or use a plain
icon from `ArcIcons` and change it in code.

## API reference

<!-- api: IconMorph -->

`IconMorphKind` lists the eight pairs, in the order of the `Kind` values: `Menu`, `Play`, `Plus`, `Copy`, `Theme`,
`Chevron`, `Bell` and `Heart`. `IconMorphChevronPointing` has two values: `Down` (the default) and `Right`.

## Accessibility

- The icon is hidden from assistive technology. The button around it provides the name.
- Show the state on the button: a toggle for a pressed state, an expanded state for a disclosure, or a changed name.
- Change the name when the meaning changes, such as Play to Pause.
- The icon does not take focus, does not hit-test and does not handle keys. The surrounding button handles them, so a
  keyboard user reaches the control through the button and its focus ring.

## Motion

- Lines rotate and slide into place, and outlines interpolate between their two shapes. Quick toggles reverse from
  wherever the icon currently is.
- A copy icon folds its back sheet away and draws a check. The sun becomes a moon with a crescent cut.
- A bell rings once when turned on, and a heart pops with a ring and sparks each time it is liked.
- Reduced motion completes every morph at once and skips the ring and the burst. Off does the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Menu, Play, Plus, Theme, Chevron | Morphs on an interruptible spring from the current position | Completes at once | Immediate |
| Copy to check | The sheet folds away and the check draws in | Completes at once | Immediate |
| Bell turned on | Rings once, the clapper lagging the bell slightly | No ring | No ring |
| Heart liked | Pops with a ring and sparks | No burst | No burst |

## Related

- `CopyButton` for a copy action with its own confirmation.
- `ThemeSwitcher` for a labelled light and dark switch.
- `Button` as the control that carries the name and state of an icon morph.
- `ExpandingButtonGroup` for icon actions that reveal their labels.

| Component | Choose it when |
|---|---|
| `CopyButton` | The action copies a value and should confirm the copy in place |
| `ThemeSwitcher` | A light and dark choice needs a visible label |
| `Button` | The icon needs a name, a state and a pressed or focus treatment |
| `ExpandingButtonGroup` | Several icon actions should reveal their labels on focus |
| `DropdownMenu` | One trigger opens a list of actions rather than showing a state |
| `ArcIcons` glyphs | The icon never changes state |
