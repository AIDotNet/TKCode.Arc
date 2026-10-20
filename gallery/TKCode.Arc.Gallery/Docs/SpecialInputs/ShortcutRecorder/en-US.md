---
title: Shortcut recorder
summary: Record key combinations into key caps, with conflict warnings, Kbd, and a searchable cheatsheet.
---

## When to use

- Keyboard shortcut settings where users remap actions.
- Shortcut hints that show the current binding of an action.
- Desktop and web apps where keyboard use is a main path.

## When not to use

- Shortcuts that are fixed and only need display: show them with your own text.
- Running an action by name: use the command palette pattern instead.
- Touch-only products: there is no physical keyboard to record.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ShortcutRecorder Label="Command palette"
                          Description="Opens the command palette from anywhere in the app." />
</UserControl>
```

Click the field or press Enter, then press the combination. The recorder stores a `KeyGesture`.

### Value and reset

`Value` holds the recorded `KeyGesture`, or `null` when there is no shortcut. `ResetValue` is the value that the reset
button returns to. In the sample it is set to the default shortcut.

```csharp
var palette = new ShortcutRecorder
{
    Label = "Command palette",
    Value = ShortcutFormatter.Parse("mod+k", ShortcutPlatform.Other),
    ResetValue = ShortcutFormatter.Parse("mod+k", ShortcutPlatform.Other)
};
```

`mod` means Command on macOS and Control on other platforms. Use `ShortcutFormatter.Parse` or `TryParse` to
create a gesture from that text.

### Known shortcuts and conflicts

`Bindings` lists the shortcuts that are already in use. Recording one of them shows a conflict with the buttons
Use anyway and Cancel. Use anyway replaces the binding and reports it in the event.

```csharp
var quickSearch = new ShortcutRecorder { Label = "Quick search" };
quickSearch.Bindings.Add(new ShortcutBinding(ShortcutFormatter.Parse("mod+s", ShortcutPlatform.Other), "Save"));
quickSearch.Bindings.Add(new ShortcutBinding(ShortcutFormatter.Parse("mod+f", ShortcutPlatform.Other), "Find"));
```

Reserved combinations such as close tab or copy warn too. Set `IsReservedWarningEnabled` to `False` to turn that off.

### Modifiers and platform

`IsModifierRequired="False"` accepts single keys. Function keys are always allowed. `Platform` sets the cap style
for `Mac` or `Other`. By default the recorder uses the running platform.

```xml
<arc:ShortcutRecorder Label="Next conversation" IsModifierRequired="False" />
<arc:ShortcutRecorder Label="Mac" Platform="Mac" />
```

### Events

`ValueChanged` reports the old and new gesture. After Use anyway, `Replaced` holds the binding that was taken.

```csharp
recorder.ValueChanged += (_, e) =>
{
    var replaced = e.Replaced is { } binding ? $" (replaced {binding.Label})" : string.Empty;
    Console.WriteLine($"{e.OldValue} -> {e.NewValue}{replaced}");
};
```

## API reference

<!-- api: ShortcutRecorder -->

## Accessibility

- The recorder is a toggle button named with its label and the spoken shortcut, such as "Command Shift K".
- Recording, the result, conflicts and clearing are announced politely. The help text is read with the field.
- Key caps are hidden from assistive technology inside the recorder. The spoken name is given to the shortcut instead.
- Enter or Space starts recording. Escape cancels, and Tab stops recording and moves focus without recording Tab.
- While recording, the keys do not trigger window shortcuts.

## Motion

- Key caps pop in with a small scale and blur. When a combination is complete, the caps settle in order.
- When the combination changes, the caps glide to their new positions.
- The recording dot pulses while recording. The message row opens and closes with a height spring.
- Reduced motion removes the scale, the stagger and the glide. Caps and messages only fade. Off motion goes to the end state at once.

## Related

- `Input` for free text that is not a shortcut.
- `Button` for the command that the shortcut triggers.
- `Field` and `FieldFrame` to build a similar field with a custom editor.
