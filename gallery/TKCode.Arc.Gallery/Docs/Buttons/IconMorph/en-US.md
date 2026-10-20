---
title: Icon morph
summary: Icons that morph between states: menu to close, play to pause, copy to check, and more.
---

## When to use

- A button whose icon means something different in each state, such as a menu that becomes a close icon.
- A toggle for play and pause, theme, notifications or like, where the change should be seen as a morph.
- A disclosure control whose chevron turns when the section opens.

## When not to use

- A copy action with a confirmation: use `CopyButton`, which already morphs its icon.
- A light and dark switch with a label: use `ThemeSwitcher`.
- An icon that never changes state: use a plain Lucide icon.
- The icon alone as a control: `IconMorph` is decorative, so put it inside a `Button` or another control that has a name and a state.

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

### Kinds

`Menu` morphs to close, `Play` to pause, `Plus` to check, `Copy` to check, `Theme` to the moon, `Chevron` turns,
`Bell` turns notifications on and off, and `Heart` likes.

```xml
<arc:IconMorph Kind="Play" />
<arc:IconMorph Kind="Theme" IsActive="True" />
<arc:IconMorph Kind="Heart" IsActive="{Binding IsLiked}" />
```

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

```xml
<arc:IconMorph Kind="Menu" IconSize="16" />
<arc:IconMorph Kind="Menu" IconSize="32" StrokeThickness="2" />
```

### Chevron direction

For `Chevron`, `ChevronPointing` sets the rest direction, and `ChevronTurn` sets the angle in degrees when the icon is
active. A value of 180 flips down to up. A value of 90 turns right to down.

```xml
<arc:IconMorph Kind="Chevron" />
<arc:IconMorph Kind="Chevron" ChevronPointing="Right" ChevronTurn="90" />
```

## API reference

<!-- api: IconMorph -->

## Accessibility

- The icon is hidden from assistive technology. The button around it provides the name.
- Show the state on the button: a toggle for a pressed state, an expanded state for a disclosure, or a changed name.
- Change the name when the meaning changes, such as Play to Pause.
- The icon does not take focus and does not handle keys. The surrounding button handles them.

## Motion

- Lines rotate and slide into place, and outlines interpolate between their two shapes. Quick toggles reverse from
  wherever the icon currently is.
- A copy icon folds its back sheet away and draws a check. The sun becomes a moon with a crescent cut.
- A bell rings once when turned on, and a heart pops with a ring and sparks each time it is liked.
- Reduced motion completes every morph at once and skips the ring and the burst. Off does the same.

## Related

- `CopyButton` for a copy action with its own confirmation.
- `ThemeSwitcher` for a labelled light and dark switch.
- `Button` as the control that carries the name and state of an icon morph.
- `ExpandingButtonGroup` for icon actions that reveal their labels.
