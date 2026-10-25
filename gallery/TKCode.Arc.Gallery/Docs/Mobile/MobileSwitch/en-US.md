---
title: Mobile switch
summary: A 51 by 31 toggle for settings that take effect at once, with a spring thumb and a 44 px hit area.
---

<Demo name="Settings" />

## When to use

`Switch` of `TKCode.Arc.Mobile` turns one setting on or off, and the change applies at once. It is a 51 by 31 track with a 27 px thumb that glides on a spring, and its hit area is at least 44 by 44. It has no hover state.

- **A settings list**: one row per setting, with the label on the left and the switch at the end.
- **Preferences that apply immediately**: notifications, sync, dark appearance.

The live demo runs inside a phone frame, a desktop simulation. Touch behavior on real Android and iOS devices has not been verified here.

## When not to use

- **A desktop settings page**: use `Switch` from `TKCode.Arc`.
- **A choice confirmed with a button**: use a checkbox or a form.
- **An action**: use `MobileButton`; a switch names a state.

## Usage

The switch derives from Avalonia's `ToggleButton`, so `IsChecked`, `IsCheckedChanged` and `Content` work as usual. It lives in the `https://github.com/AIDotNet/TKCode.Arc.Mobile` XAML namespace (prefix `arcm`); the API table calls it `MobileSwitch` because the desktop package has a `Switch` too. Put the label in your own row and the switch at its end.

```xml
<Grid xmlns="https://github.com/avaloniaui"
      xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
      ColumnDefinitions="*,Auto" MinHeight="52">
    <TextBlock Text="Notifications" VerticalAlignment="Center" />
    <arcm:Switch Grid.Column="1" IsChecked="True" />
</Grid>
```

The track is filled with the on color when checked. A disabled switch is dimmed to 0.5 and keeps its state.

### Tokens

`SwitchToken` holds the geometry: `TrackWidth` (51), `TrackHeight` (31), `TrackInset` (2), `ThumbSize` (27), `ThumbOnMargin`, `TouchTarget` (44) and `DisabledOpacity` (0.5). Colors come from the ArcDesign selection-control tokens.

## API reference

<!-- api: MobileSwitch -->

## Accessibility

It is a toggle button: it can be focused and toggled with the keyboard, and exposes its state. Put the setting's name in a label next to it and keep the row at least 44 px tall.

## Motion

The thumb glides on a spring and the track fades to the on color; both are declared in the control theme and switch at once with reduced motion.

## Related

- `MobileButton`: for actions.
- `MobilePage`: the screen a settings list sits on.
