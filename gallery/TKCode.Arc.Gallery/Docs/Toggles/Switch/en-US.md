---
title: Switch
summary: A tactile toggle for settings that take effect immediately.
---

<Demo name="SettingsList" />

## When to use

- Settings that take effect as soon as they are changed, such as notifications.
- A settings list with one switch on each row.
- A setting that is either on or off, with no confirm button.

## When not to use

- A choice that is confirmed with a submit button, such as accepting terms: use `Checkbox`.
- A choice among named options: use `SegmentedControl` or `RadioGroup`.
- Several options where the user picks more than one: use `Checkbox` for each.

## Usage

`Content` is the label of the switch. The whole label is part of the click target.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Switch Content="Email notifications" IsChecked="True" />
</UserControl>
```

### Settings list

Stack the switches to form a list of settings.

```xml
<StackPanel Spacing="4">
    <arc:Switch Content="Email notifications" IsChecked="True" />
    <arc:Switch Content="Weekly digest" />
    <arc:Switch Content="Product updates" />
</StackPanel>
```

### Binding

Bind `IsChecked` to a view model property. Bind `IsEnabled` to lock the switch.

```xml
<arc:Switch Content="Email notifications"
            IsChecked="{Binding EmailEnabled}"
            IsEnabled="{Binding CanEditSettings}" />
```

### Disabled

A disabled switch keeps its state and cannot be changed.

<Demo name="Disabled" />

### Changes

`IsCheckedChanged` is raised whenever the state changes, from the pointer, the keyboard or code. Apply the setting
there, so every way of changing it has the same effect.

```csharp
notifications.IsCheckedChanged += (_, _) =>
{
    settings.EmailEnabled = notifications.IsChecked == true;
};
```

## API reference

<!-- api: Switch -->

## Accessibility

- `Content` is the switch's name. A switch with only an icon needs `AutomationProperties.Name`.
- Space and Enter toggle the switch. The thumb keeps its stretched shape while Space is held.
- Assistive technology reports the switch as a toggle button in the checked or unchecked state. Avalonia has no separate switch control type.
- Keyboard focus draws a 2px outline offset by 2px. Pointer focus does not.
- The thumb is hidden from assistive technology. The on and off state is also shown by the track color and the thumb position.

## Motion

- Pressing the switch widens the thumb toward the other side while the far edge stays in place. Releasing glides the thumb across on a spring with no bounce.
- A change that did not come from a press, such as a key press or code, plays a brief stretch of the thumb.
- The track fill fades in and out, and the thumb color changes with it.
- Reduced motion removes the stretch, moves the thumb at once and turns off the color transitions. Off does the same.

## Related

- `Checkbox` for a choice that is confirmed with a submit button.
- `SegmentedControl` for switching between a few named views.
- `RadioGroup` for choosing exactly one option from a set.
