---
title: Keycap keyboard
summary: A keyboard of keycaps that lights up a shortcut, shows what keys do while modifiers are held, and presses keys as you type or click.
---

<Demo name="Preview" />

## When to use

`KeycapKeyboard` draws a compact keyboard of real-looking keycaps. It can light up one shortcut in the accent color, and
it can print what each key does: hold Ctrl and the keys with a Ctrl shortcut show their action. Keys go down as they are
pressed on the real keyboard, and they can be clicked; a clicked modifier stays held until the next key.

- **Shortcut help**: a "keyboard shortcuts" panel where people hold a modifier to explore.
- **Onboarding and tips**: show the one combination that does a thing.
- **Key testers and typing lessons**: watch keys light up while typing.

## When not to use

- **Typing text**: it is not an on-screen keyboard for input; use the platform's.
- **Long lists of shortcuts**: a searchable table reads better than a keyboard.
- **Layouts other than a US keyboard**: keys are placed and labelled as on a US layout.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:KeycapKeyboard Label="Editor shortcuts">
        <arc:KeycapBinding Gesture="Ctrl+C" Label="Copy" />
        <arc:KeycapBinding Gesture="Ctrl+V" Label="Paste" />
        <arc:KeycapBinding Gesture="Ctrl+Shift+P" Label="Run" />
    </arc:KeycapKeyboard>
</UserControl>
```

`KeycapKeyboard` is in the Special package; register it with `UseArcSpecial()` next to `UseArcControls()`. Put
`KeycapBinding` items in it: while exactly a binding's modifiers are held (`ActiveModifiers`), its key shows its label.
A binding with no modifiers shows while none are held. `KeyInvoked` is raised for every clicked key, and for every real
key press while presses are tracked; it carries the key, the held modifiers and the binding they make.

### One shortcut

`Shortcut` lights up one combination, written as "Ctrl+Shift+P", "Cmd+K" or with Mac symbols, "⇧⌘P". `Platform` prints
Windows or Mac labels on the modifier keys (Auto follows the system), `ShowsFunctionRow` adds Esc and F1 to F12, and
`KeySize` sets the largest key.

<Demo name="Shortcut" />

### Following the real keyboard

`Tracking` decides which presses light up keys: none, presses while the keyboard has focus (the default), or every
press in the window. Window tracking only watches; the presses still reach the control that has focus.

<Demo name="Typing" />

### Best practices

**Do**

- Keep binding labels to one short word: a key has room for about eight letters.
- Set `Platform` when the shortcuts are written for one system.
- Name the keyboard with `Label`, like "Editor shortcuts".

**Avoid**

- Window tracking on screens with passwords: the keys show what is typed.
- Lighting up several shortcuts at once; use bindings instead.

## API reference

<!-- api: KeycapKeyboard -->

<!-- api: KeycapBinding -->

## Accessibility

- The keyboard is one group named by `Label`. Its help text reads the lit shortcut ("Shortcut Ctrl+Shift+P.") and the
  shortcuts shown now ("Ctrl+C: Copy.").
- It is a tab stop. While it has focus with the default tracking, real key presses play it and raise `KeyInvoked`; Tab
  still moves focus on.
- Pointer focus draws no ring; keyboard focus shows the focus ring around the whole keyboard.

### Keyboard interactions

| Key | Action |
|---|---|
| Tab | Moves focus to the keyboard and on again. |
| Any key | Presses that key on the keyboard (with the default `Tracking`). |
| Ctrl, Shift, Alt, Win or ⌘ | Held: shows the bindings for the held modifiers. |

## Motion

- A key sinks into its side as it goes down and rises when it is let go, in about 120 ms.
- One frame loop drives every key and stops as soon as all keys rest.
- Keys and labels are drawn by the keyboard itself; a frame allocates nothing.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Key press | Sinks and rises | Jumps down and up | Jumps down and up |
| Modifier held | Its bindings appear | Its bindings appear | Its bindings appear |

## Responsive behavior

- The keyboard is 15 key units wide. Keys take `KeySize` (44 px) when there is room and shrink to fit the width given,
  so it stays whole on a phone; labels shrink with them.
- Hover is not shown for touch; a tap clicks the key.

## Related

- `ShortcutRecorder` records a shortcut that people type.
- `CommandPalette` finds commands by name and shows their shortcuts.

| Component | Choose it when |
|---|---|
| `KeycapKeyboard` | Showing where shortcut keys are, or exploring them |
| `ShortcutRecorder` | Letting people set their own shortcut |
| `CommandPalette` | Finding commands by name |
