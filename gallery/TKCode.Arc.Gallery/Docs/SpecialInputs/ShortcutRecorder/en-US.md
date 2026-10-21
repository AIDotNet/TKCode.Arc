---
title: Shortcut recorder
summary: Record key combinations into key caps, with conflict warnings, Kbd, and a searchable cheatsheet.
---

<Demo name="Default" />

## When to use

`ShortcutRecorder` lets a person set a keyboard shortcut by pressing it, instead of typing a key name. It sits in a
settings page beside the other preferences, and it shows the result as key caps that match the platform. Behind the
field it checks the combination against the shortcuts the app already uses and against the combinations that browsers
and operating systems keep, so a remapping cannot silently break a command.

- **Shortcut settings in a productivity app**: a person remaps "Open file" from Ctrl+O to Ctrl+Shift+O. The recorder
  records the chord, warns if Ctrl+Shift+O already runs "Export report", and stores the result as a `KeyGesture`.
- **Keyboard maps in a developer tool or an IDE**: a terminal, an editor or a query console where the user rebinds
  "Run query" or "Comment line". `Bindings` holds the app's current map, so every conflict is caught at record time.
- **Help dialogs and cheatsheets that show the live binding**: the shortcut text beside a command comes from the stored
  `Value`, so a remapped shortcut shows the new keys everywhere.
- **Admin consoles with keyboard-first operators**: a dispatch or support tool where agents use shortcuts for triage.
  The recorder lets a supervisor change them without an editor for key names.
- **Reset to defaults in a preferences screen**: `ResetValue` is the default shortcut, and the reset button returns the
  field to it when the value differs.

## When not to use

- **A fixed shortcut that is only shown**: display it with your own text or a key cap control, because no one is
  remapping it.
- **Running an action by name**: use the command palette pattern from the `Blocks` and command components, which finds
  an action by its name and does not depend on a key chord.
- **Touch-only products**: there is no physical keyboard to record, and a tap would never produce a chord.
- **A text value with no key semantics**: use `Input`, for example for a macro name, a tag or a note about a shortcut.
- **Free text that a person types into a search box**: use `SearchField`; the recorder takes the whole chord and does not
  edit text.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ShortcutRecorder Label="Command palette"
                          Description="Opens the command palette from anywhere in the app." />
</UserControl>
```

Click the field or press Enter, then press the combination. The recorder stores a `KeyGesture`, and it draws the gesture
as key caps.

`ShortcutRecorder` derives from `TemplatedControl`. Its frame is the Arc field frame, the same frame as `Input`, and the
recording surface inside it is a button that draws its own key caps, recording light and placeholder. The recorder is
therefore a value editor with the label, the frame and the message row of a field, plus the recording behavior of a
toggle button.

### Anatomy

1. **Label** (`Label`): the field's name above the frame. `IsLabelVisible="False"` hides it, and assistive technology still
   reads it.
2. **Frame**: the Arc field frame at the medium control height (44 px in the default theme). Hover and focus outline it in
   the foreground color. While recording the surface takes a 4 percent accent tint, and a conflict uses a warning edge.
3. **Recording surface**: fills the row, with 6 px of padding on the right and 9 px on the left. It is the element that
   takes focus and starts recording.
4. **Key caps**: one cap per key of the shortcut, with a minimum size of 26 by 26 px, an 8 px corner radius and 4 px between
   caps. On a Mac the caps show glyphs (⌃ ⌥ ⇧ ⌘); elsewhere they show names (Ctrl, Alt, Shift).
5. **Recording light and placeholder**: a 7 px dot while recording, next to `RecordingPlaceholder` ("Press keys" by
   default). `Placeholder` ("Record shortcut") shows while there is no shortcut.
6. **Clear button** (`ClearText`, "Clear shortcut"): a 32 px square with a 16 px glyph. It shows while a shortcut is set and
   the field is not recording.
7. **Reset button**: a 32 px square with the same glyph. It shows when the value differs from `ResetValue`, and it restores
   `ResetValue`.
8. **Message row**: 12 px copy under the frame. It shows one message at a time, in this order: a conflict (with a 6 px
   warning dot and the links `UseAnywayText` and `CancelText`), the modifier nudge, the recording hint
   ("Esc to cancel, Backspace to clear") and then `Description`.

### Value and reset

`Value` is the recorded `KeyGesture`, or `null` when there is no shortcut. It is two-way. `ResetValue` is what the reset
button restores. Avalonia has no default value, so `ResetValue` is `null` unless you set it; a field whose value is set but
whose reset value is `null` shows its reset button as "Reset to none".

<Demo name="Empty" />

```csharp
var palette = new ShortcutRecorder
{
    Label = "Command palette",
    Value = ShortcutFormatter.Parse("mod+k", ShortcutPlatform.Other),
    ResetValue = ShortcutFormatter.Parse("mod+k", ShortcutPlatform.Other)
};
```

`mod` means Command on macOS and Control on other platforms. Use `ShortcutFormatter.Parse` to create a gesture from that
text, or `ShortcutFormatter.TryParse` when the text comes from a settings file and may be invalid. `Parse` throws a
`FormatException` for text that is not a shortcut with exactly one key.

### Known shortcuts and conflicts

`Bindings` lists the shortcuts the app already uses, as `ShortcutBinding` objects with a `Gesture` and a `Label`. Recording
one of them shows a conflict with two real buttons, Use anyway and Cancel. Use anyway takes the shortcut and reports the
binding it came from in `ValueChanged`, so the app can remove that binding from its own map. Cancel keeps the previous
shortcut.

<Demo name="Conflicts" />

Reserved combinations, such as Ctrl+W to close a tab, Ctrl+T for a new tab, Ctrl+C and Ctrl+V for copy and paste, Ctrl+Z
for undo and Ctrl+A for select all, also warn. They are checked after `Bindings`. A reserved combination that is taken
with Use anyway reports no replaced binding, because nothing in the app owns it. Set `IsReservedWarningEnabled` to `False`
only when the app owns those combinations and the warning would be noise.

<Demo name="Reserved" />

### Modifiers and platform

`IsModifierRequired` is `True` by default: a bare letter is not a shortcut, so the recorder shows the nudge "Include
Ctrl or Alt" (or "Include ⌘, ⌃, or ⌥" on a Mac) and waits for a modifier. Function keys are always allowed. Set it to
`False` for a single-key shortcut, such as a "?" help key or a number key that switches a view.

`Platform` sets the cap style and the meaning of `mod`: `ShortcutPlatform.Mac` shows glyphs and uses Command for `mod`,
and `ShortcutPlatform.Other` shows names and uses Control. Leave it `null` to detect the running platform, which is what
a desktop app wants. Set it explicitly in a settings screen that must show the same caps on every machine, or in a
screenshot.

<Demo name="NoModifier" />

<Demo name="Platforms" />

### Events

`ValueChanged` is raised when `Value` changes: a recorded shortcut, a cleared one, a reset, or a value set from code. Its
arguments carry the old and the new `KeyGesture` and the `Replaced` binding. `Replaced` holds the binding that a Use anyway
took the shortcut from, and it is `null` for every other change.

<Demo name="Events" />

```csharp
recorder.ValueChanged += (_, e) =>
{
    var replaced = e.Replaced is { } binding ? $" (replaced {binding.Label})" : string.Empty;
    Console.WriteLine($"{e.OldValue} -> {e.NewValue}{replaced}");
};
```

### Binding to a view model

A settings view model owns the map of shortcuts. Each row binds `Value` two-way to its own gesture, and the recorder's
`Bindings` list is the other shortcuts of the map. The example uses CommunityToolkit.Mvvm.

```csharp
public partial class ShortcutSettingsViewModel : ObservableObject
{
    public ShortcutSettingsViewModel()
    {
        Shortcuts.Add(new ShortcutBinding(ShortcutFormatter.Parse("ctrl+p", ShortcutPlatform.Other), "Quick open"));
        Shortcuts.Add(new ShortcutBinding(ShortcutFormatter.Parse("ctrl+s", ShortcutPlatform.Other), "Save"));
    }

    public ObservableCollection<ShortcutBinding> Shortcuts { get; } = new();

    [ObservableProperty]
    private KeyGesture? _openFile = ShortcutFormatter.Parse("ctrl+o", ShortcutPlatform.Other);

    [RelayCommand]
    private void ResetOpenFile() => OpenFile = ShortcutFormatter.Parse("ctrl+o", ShortcutPlatform.Other);
}
```

```xml
<arc:ShortcutRecorder Label="Open file"
                      Value="{Binding OpenFile, Mode=TwoWay}"
                      Bindings="{Binding Shortcuts}"
                      ResetValue="Ctrl+O"
                      Platform="Other" />
```

`Value` is two-way. `Bindings` is a list the field reads when a person records, so bind it to the map the app uses.
`ValueChanged` is an event, so connect it in code-behind when the app must remove the replaced binding from its own map.
`IsRecording` is read-only, and `StartRecording()` and `StopRecording()` start and stop the recorder from code.

### Hidden label

`IsLabelVisible="False"` keeps the label for screen readers only. Use it in a table row or a list where the row already
names the action, as the Accessibility section explains.

<Demo name="HiddenLabel" />

### Description

`Description` is helper copy under the field. The recording hint, the modifier nudge and a conflict take its place while
they apply, so the description describes the shortcut when nothing else needs the row.

<Demo name="Description" />

### Disabled

A disabled recorder is faded on the muted surface, and it does not record or take focus.

<Demo name="Disabled" />

### Example: keyboard shortcuts in a settings page

A preferences page of a desktop productivity app. Each row names an action in sentence case, and the recorder shows the
current shortcut. "Open file" has been remapped from Ctrl+O to Ctrl+Shift+O, so its reset button is visible and returns
it to the default. The description on the first row explains what the shortcut does, and the platform is fixed to
"Other" so the screenshot shows Ctrl names on every machine.

![Keyboard shortcut settings with four recorders, one remapped with a reset button](images/ShortcutSettings.png)

```xml
<StackPanel Width="600" Spacing="16">
    <TextBlock Text="Keyboard shortcuts"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:ShortcutRecorder Label="Command palette"
                          Platform="Other"
                          Value="Ctrl+K"
                          ResetValue="Ctrl+K"
                          Description="Opens the command palette from any screen." />
    <arc:ShortcutRecorder Label="Open file"
                          Platform="Other"
                          Value="Ctrl+Shift+O"
                          ResetValue="Ctrl+O"
                          Description="Reset returns the shortcut to Ctrl+O." />
    <arc:ShortcutRecorder Label="Toggle sidebar"
                          Platform="Other"
                          Value="Ctrl+B"
                          ResetValue="Ctrl+B" />
    <arc:ShortcutRecorder Label="Save report"
                          Platform="Other"
                          Value="Ctrl+S"
                          ResetValue="Ctrl+S" />
</StackPanel>
```

### Content guidelines

- **Label**: the action the shortcut runs, in sentence case and as a short noun phrase: "Command palette", "Open file",
  "Toggle sidebar". Never the key: a label of "Ctrl+O" tells the person nothing when the value changes.
- **Description**: one sentence on what the shortcut does or what it conflicts with: "Opens the command palette from any
  screen." Keep the sentence true for every default value; the conflict message covers the rest.
- **Conflict copy**: the recorder writes it from the binding's label. Name the action the shortcut already runs, and keep
  the two choices in the same terms: "Use anyway" and "Cancel".
- **Nudge**: a shortcut needs a modifier, so the nudge names the modifier to add. Do not write a generic error.

| Instead of | Write |
|---|---|
| Keybinding | Shortcut |
| Ctrl+O (as the label) | Open file |
| Shortcut invalid | Include Ctrl or Alt |
| Shortcut already taken | Ctrl+K is used by Command palette. Use anyway, or record another. |
| Set key | Record shortcut (the default placeholder; keep it) |

### Best practices

**Do**

- Pass the app's current map in `Bindings`. A conflict is only caught for shortcuts the field knows about, so an empty list
  makes every conflict invisible.
- Set `ResetValue` on every field that has a default. Without it the reset button reads "Reset to none" and returns
  nothing useful.
- Handle `ValueChanged` and remove `Replaced` from the app's own map when Use anyway takes a shortcut. The recorder does not
  unbind the other action for you.
- Match the shortcut in the window's key handling with `ShortcutFormatter.Matches`. The recorder records a `KeyGesture`, and
  `Matches` compares the physical key with the exact modifiers, so the same gesture runs the same action.

**Avoid**

- Using it for touch-only screens, where no chord can be pressed. Give those screens a different control.
- Turning off `IsReservedWarningEnabled` in a normal desktop app. The warning stops a remap from breaking copy, paste or
  close, which people rely on in every other app.
- Using a shortcut as the only way to reach a command. Put the command in the menu or the command palette as well, so the
  command is discoverable.
- Recording a bare letter in a shortcut field with `IsModifierRequired` on. The nudge will reject it, and a plain letter
  would fire while the person types into any text field.

### Common questions

**Why does the recorder not see Tab or Alt+Tab?** Tab is never recorded: it stops recording and moves focus. Chords the
operating system keeps (Alt+Tab, Ctrl+Alt+Delete, most Windows key combinations and the macOS screenshot keys) never reach
the application, so they cannot be recorded.

**How do I run the action a shortcut is bound to?** In the window's `KeyDown` handler, call `ShortcutFormatter.Matches(e,
gesture, platform)` with the stored gesture. It returns `true` when the physical key and the exact modifiers match.

**Why is there no reset button?** The reset button shows when the value differs from `ResetValue`. When both are `null`,
there is nothing to reset to, so no button is shown. Set `ResetValue` to the default shortcut to show it.

**Does a value set in code raise `ValueChanged`?** Yes. The event is raised for recorded, cleared and reset values and for
values set from code or a binding. Compare the old and new gestures in the handler if the save should run only for a
person's change.

## API reference

<!-- api: ShortcutRecorder -->

Companion types. `ShortcutPlatform` sets the cap style and the meaning of `mod`:

| Member | Meaning |
|---|---|
| `Mac` | Apple keyboards: ⌃ ⌥ ⇧ ⌘ glyphs, and `mod` is Command |
| `Other` | Windows and Linux keyboards: Ctrl, Alt, Shift and Win names, and `mod` is Control |

`ShortcutBinding` is an entry of `Bindings`. It has a parameterless constructor for XAML, and a constructor that takes the
gesture and the label. Its `Gesture` is a `KeyGesture` and its `Label` is the action name shown in the conflict message.

`ShortcutRecorderValueChangedEventArgs` is the argument of `ValueChanged`:

| Member | Meaning |
|---|---|
| `OldValue` | The previous shortcut, `null` when there was none |
| `NewValue` | The new shortcut, `null` when it was cleared |
| `Replaced` | The binding the shortcut was taken from after Use anyway; `null` for every other change, including a reserved shortcut |

`ShortcutFormatter` is the static helper that the recorder uses. It is public so that the app can display and match
gestures with the same rules:

| Member | Meaning |
|---|---|
| `CurrentPlatform` | The platform of the process: `Mac` on macOS and iOS |
| `Format(gesture, platform)` | The text ("⌘⇧K" on a Mac, "Ctrl+Shift+K" elsewhere) and the spoken form ("Command Shift K") |
| `Tokens(gesture, platform)` | The key caps in display order, each with its label, its id and its spoken name |
| `Matches(e, gesture, platform)` | `true` when a key event is the gesture, by physical key and exact modifiers |
| `FromKeyEvent(e)` | Reads a key-down as a shortcut, or `null` while only modifiers are down |
| `Parse(text, platform)` | Parses "mod+shift+k", "alt+f4" or "ctrl+/"; throws `FormatException` for an invalid shortcut |
| `TryParse(text, platform, out gesture)` | The same, without throwing |

## Accessibility

- The recorder is a toggle button named with its label and the spoken shortcut, such as "Command palette Command Shift K".
  It is pressed while it is recording, and its help text is the message row.
- Recording, the result, conflicts and clearing are announced politely through the live region. The announcements use the
  same text as the message row.
- The key caps are hidden from assistive technology inside the recorder. The spoken name is given to the button instead.
- The reset and clear buttons are named "Reset to <shortcut>" (or "Reset to none") and "Clear shortcut".
- While recording, the chord belongs to the recorder, so window shortcuts do not run.
- `IsLabelVisible="False"` hides the label, which still names the recorder for assistive technology.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` or `Space` on the recorder | Starts recording |
| A key combination while recording | Records the chord from physical keys; `Alt+K` records K, not a typed symbol |
| `Escape` while recording | Cancels, and the previous shortcut stays |
| `Backspace` or `Delete` while recording | Clears the shortcut |
| `Backspace` or `Delete` on the recorder, not recording | Clears the shortcut, when one is set |
| `Tab` while recording | Stops recording and moves focus without recording Tab |
| Focus leaves the recorder | Stops recording and clears the nudge |
| A bare letter without a modifier | Shows the nudge and keeps recording; a function key is recorded without a modifier |
| `Tab` into a conflict | Reaches Use anyway and Cancel; either one returns focus to the recorder |

## Motion

- Key caps pop in with a small scale and blur. When a combination is complete, the caps settle in order, 35 ms apart.
- When the combination changes, the caps glide to their new positions.
- The recording light pulses while recording. The message row opens and closes with a height spring.
- Reduced motion removes the scale, the stagger and the glide. Caps and messages only fade, and the recording light stops
  pulsing. Off motion goes to the end state at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Caps appear while recording | Pop in with a small scale and blur | Short fade | Immediate |
| Combination completes | Caps settle in order, 35 ms apart | Short fade | Immediate |
| Combination changes | Caps glide to their new positions on a snappy spring | Short fade | Immediate |
| Recording light | Pulses while recording | Steady; no pulse | Steady |
| Message row changes | Opens and closes on a height spring; new text rises with a soft blur | Short fade | Immediate |

## Related

- `Input` for free text that is not a shortcut.
- `Button` for the command that the shortcut triggers.
- `Field` and `FieldFrame` to build a similar field with a custom editor.
- `SearchField` for typed text that filters a list.

| Component | Choose it when |
|---|---|
| `ShortcutRecorder` | A person sets a keyboard shortcut by pressing it, with conflict checks |
| `Input` | The value is text, such as a macro name or a note |
| `SearchField` | The person types text that filters a list or a menu |
| `Button` | The shortcut runs a command, and the command needs its own control |
| `Field` + `FieldFrame` | You build your own editor and want the same label, frame and message rows |
