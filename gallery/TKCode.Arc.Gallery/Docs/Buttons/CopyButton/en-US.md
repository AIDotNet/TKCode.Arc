---
title: Copy button
summary: Copy a value with immediate confirmation.
---

<Demo name="Outline" />

## When to use

`CopyButton` copies one value to the clipboard and confirms the copy in place: the icon becomes a check, the label
changes to "Copied" and the button returns to rest after a moment. It sits next to the value it copies, so the person
never has to select text, find the right characters or leave the row they are working in. It is a `Button`, so it works
with the keyboard, in a toolbar and in any layout that takes a button.

- **Secrets and keys shown once**: an API key or a webhook signing secret on a developer console page. The person copies
  the full value into a password manager or a config file, and the masked text beside it never has to be selected.
- **Install commands and snippets in onboarding pages**: a line such as `npx arc add copy-button` in a docs page or a
  setup wizard. The `Plain` variant keeps the command readable and still offers the copy action.
- **Shareable links on an invoice or a report**: a payment link or a read-only report URL on a billing page, copied
  so the person can paste it into an email to a customer.
- **Identifiers in dense tables**: order IDs, workspace IDs or tracking numbers in a list, where the icon-only form adds
  one copy action per row without widening the table.
- **Values that must be exact**: a connection string or a SKU, where typing it by hand or selecting it from wrapped text
  invites a mistake.

## When not to use

- **Copying the value in several formats**, such as Copy link, Copy as Markdown or Copy embed code: use `SplitButton`,
  so the primary copy stays one press and the alternatives sit in its menu.
- **A complete code sample** with a copy control of its own: use `CodeBlock`, which already has one in its header.
- **A read-only field the person may need to edit or read in full**: use `Input` with `IsReadOnly="True"`, which keeps
  the value selectable. Use `CopyButton` beside that field when a single press should copy the whole value.
- **Actions that send or run something** rather than copy it, such as a Send invite or a Rotate key action: use `Button`
  or `ActionButton`, whose pending and success states describe the work.
- **A list of row actions where copying is one of many**: use `DropdownMenu` from Menus, so the copy action sits with
  Edit, Rename and Delete instead of taking a button of its own on every row.

## Usage

Set `Value` to the text that goes to the clipboard. `Label` is the visible text and the accessible name.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CopyButton Value="npx arc add copy-button" />
</UserControl>
```

`CopyButton` derives from Avalonia's `Button`. It keeps `Command`, `Click`, `IsEnabled` and the other button behavior,
and it adds the copy in its click handling: the value is read when the button is clicked, and the clipboard write is
awaited, so the window keeps running while the copy completes.

### Anatomy

1. **Frame** (`Variant`, `Background`, `BorderBrush`): a 1 px border on the surface color with the control radius and a
   minimum height of the small control size (36 px). Hover moves the frame to the muted surface and the strong border.
2. **Icon**: a 16 px copy glyph. After a copy it becomes a check in the success color; after a failure it becomes an
   alert in the danger color.
3. **Label** (`Label`, `CopiedLabel`, `FailedLabel`): the text. The label cell reserves the widest of the three words,
   so the button keeps its width while the words change. The gap between icon and label is 8 px and the inline padding
   is 12 px on each side.
4. **Focus ring**: a 2 px ring in the focus color, drawn 4 px outside the frame. It appears only for keyboard focus.

### Variants and sizes

`Variant` sets the frame. `Outline` (the default) draws the surface and the border, so the button reads as an action.
`Plain` removes the frame and uses the secondary text color, which suits a row of code, a dense table or a caption where
an outlined button would compete with the content.

<Demo name="Plain" />

```xml
<arc:CopyButton Value="https://uiarc.dev" IsIconOnly="True" />
<arc:CopyButton Value="#7C5CFF" Label="Copy hex" Variant="Plain" />
```

Pick one variant per region. Use `Outline` for the copy that matters most in a panel, and `Plain` for the copy actions
that repeat in a list or sit inside a code block.

### Icon only and labels

`IsIconOnly` hides the label and leaves a square 36 px button with the icon. The `Label` still names the button for
assistive technology, so an icon-only copy needs a specific label: "Copy order ID" is announced where "Copy" would be
ambiguous in a table with many rows. The icon-only button is 36 px wide, so it fits a table column without widening it.

### Reset time and events

The button returns to idle after `ResetAfter`, which is 1.9 seconds by default. `Copied` is raised after a successful
copy, and never after a failure. `State` reports `Idle`, `Copied` or `Error` and is read-only: the control sets it, and
code reads it.

```csharp
copy.Copied += (_, _) => log.Text = "Copied to the clipboard.";
copy.ResetAfter = TimeSpan.FromSeconds(3);
```

Lengthen `ResetAfter` only when the person must read the confirmation before it changes, for example when the copy is
followed by a step on another page. Keep the default for a single copy, so the control is ready again for the next one.

### Binding to a view model

The value is usually data the view model already holds, so bind `Value` one way and let the click copy whatever it holds
at that moment. The `Failure` demo shows the case where no value exists.

<Demo name="Failure" />

```csharp
public partial class DeveloperKeysViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _apiKey = "ak_live_2026_7f3a9c21";

    [ObservableProperty]
    private string? _installCommand = "npx arc add copy-button";
}
```

```xml
<StackPanel Spacing="8">
    <arc:CopyButton Value="{Binding ApiKey}" Label="Copy key" />
    <arc:CopyButton Value="{Binding InstallCommand}" Label="Copy command" Variant="Plain" />
</StackPanel>
```

- `Value` is one-way. The control never writes to the view model, so a copy cannot change the data it copies.
- A `null` value makes the copy fail: the button shows its failed state and its width does not change. Bind the value
  to the data that is actually available, rather than to a placeholder that would be copied by mistake.
- Keep the `Value` in step with the data on screen. A masked display such as "ak_live_****" should not be the bound
  value, because the person would paste the mask.

### Example: developer API keys

A developer console page that shows a production key with a copy action and an install command in the same panel. The
key is the most important value, so it uses the outline variant and a full label. The command is a secondary value, so
it uses the plain variant with an icon-only copy. Both values are visible in full, so the person can check them before
copying.

![An API keys panel with a production key and a copy command row](images/ApiKeys.png)

```xml
<StackPanel Width="620" Spacing="16">
    <TextBlock Text="API keys"
               FontSize="{arc:ArcDesignTokenResource TypeLG}"
               FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Border Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
            Padding="20">
        <StackPanel Spacing="18">
            <StackPanel Spacing="6">
                <TextBlock Text="Production key"
                           FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Created 14 April 2026. Use it on the server only."
                           TextWrapping="Wrap"
                           FontSize="{arc:ArcDesignTokenResource TypeSM}"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
            <Grid ColumnDefinitions="*,Auto">
                <Border Grid.Column="0"
                        Background="{arc:ArcDesignTokenResource SurfaceMuted}"
                        CornerRadius="{arc:ArcDesignTokenResource RadiusControl}"
                        Padding="12,9"
                        VerticalAlignment="Center">
                    <TextBlock Text="ak_live_2026_7f3a9c21"
                               FontFamily="{arc:ArcDesignTokenResource MonoFontFamily}"
                               FontSize="{arc:ArcDesignTokenResource TypeSM}"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </Border>
                <arc:CopyButton Grid.Column="1"
                                Label="Copy key"
                                Value="ak_live_2026_7f3a9c21"
                                Margin="12,0,0,0"
                                VerticalAlignment="Center" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource BorderSubtle}" />
            <Grid ColumnDefinitions="Auto,*,Auto">
                <TextBlock Grid.Column="0"
                           Text="Install"
                           Margin="0,0,16,0"
                           VerticalAlignment="Center"
                           FontSize="{arc:ArcDesignTokenResource TypeSM}"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Grid.Column="1"
                           Text="npx arc add copy-button"
                           FontFamily="{arc:ArcDesignTokenResource MonoFontFamily}"
                           FontSize="{arc:ArcDesignTokenResource TypeSM}"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <arc:CopyButton Grid.Column="2"
                                Label="Copy command"
                                Value="npx arc add copy-button"
                                Variant="Plain"
                                IsIconOnly="True"
                                VerticalAlignment="Center" />
            </Grid>
        </StackPanel>
    </Border>
</StackPanel>
```

The key is the one value the page exists to copy, so it has a full label, the surface frame and a monospace face that
keeps characters such as the `0` and `O` distinct. The command is secondary, so its icon-only copy lets the row stay
short while its accessible name still says what it copies.

### Content guidelines

- **Label**: a verb and its object in sentence case, such as "Copy key" or "Copy command". Name the value so the
  button makes sense out of context and to a screen reader reading a list of buttons.
- **Copied label**: a short past-tense confirmation, "Copied" or "Key copied". The copied state lasts only a moment, so
  the word must be understood at a glance.
- **Failed label**: say what did not happen, not why. "Could not copy" tells the person to try again or select the text.

| Instead of | Write |
|---|---|
| Click to copy | Copy key |
| Success! | Copied |
| Error | Could not copy |
| Copy to clipboard (in a table of 40 rows) | Copy order ID |

### Best practices

**Do**

- Name the value in the `Label` ("Copy key", "Copy invoice link"), since the label is the accessible name even when the
  icon hides it. A generic "Copy" repeated down a list gives a screen reader nothing to tell the rows apart.
- Copy the complete value the person needs, not a masked display. A pasted mask is a support ticket the page could have
  prevented.
- Use `Plain` in dense rows and code, and `Outline` for the copy that matters most in a panel. One outlined copy per
  panel keeps the primary action clear.
- Keep the default `ResetAfter` for a single copy, so the control returns to its label before the person moves on.

**Avoid**

- Binding `Value` to data that may be missing without handling the failure. A `null` value shows the failed state, and
  the person needs a reason to try again, not a silent button.
- Combining the copy with a destructive action in one button, such as "Copy and rotate key". Copying is safe, so keep
  the rotation in its own control with a confirmation.
- Stacking several outlined copy buttons in one row. The eye reads the row as several equal actions; use `Plain` for
  the secondary copies.
- Relying on the `Copied` event to show a message. The button already confirms the copy in place; a second message
  repeats the same signal and competes with the page.

### Common questions

**Why does the button show the failed state even though `Value` is set?** The click reports failure when the clipboard
refuses the write, so the failed state is not caused by the value alone. Show the failure label and let the person copy
the text by hand from a selectable field.

**Does `Copied` fire when the copy fails?** No. The event is raised only after a successful copy. Use `State` to read the
result of the last click, including `Error`.

**Can the button copy a value that is generated when the person clicks?** Not from `Value` alone. The value is read at
the click, so keep it current with the data on screen. Generate long or remote values before the page shows the button,
and bind the result to `Value`.

**How do I make the button icon only on narrow screens?** Set `IsIconOnly` from code when the layout narrows, for example
in a `SizeChanged` handler. The label stays the accessible name, so keep it specific.

## API reference

<!-- api: CopyButton -->

## Accessibility

- The accessible name is always `Label`, including when the button is icon only.
- A hidden polite status region announces the result, such as "Copy key: Copied" or "Copy key: Could not copy".
- Enter and Space copy the value.
- The label cell keeps the width of its widest state, so the layout around the button does not move.
- Keyboard focus shows the focus ring. Pointer focus draws no ring.
- A disabled button drops to half opacity.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Moves focus to the next or previous focusable control, including the copy button |
| `Enter` or `Space` | Copies the value; the copied or failed state shows, then returns to rest after `ResetAfter` |

## Motion

- The icon swaps with a soft blur and scales in on a slow, nearly critically damped spring. The check draws itself.
- Only the changed letters of the label move, and they play back in reverse when the button returns to idle.
- A press scales the content down slightly and a release springs it back.
- Reduced motion uses an instant crossfade for the icon and the text, skips the stroke drawing and the press scale. Off
  replaces the icon and the label at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Copied or failed | The icon swaps with a soft blur and the check draws itself | The icon and the label crossfade instantly; the check does not draw | Immediate |
| Label change | Only the changed letters move, and they return in reverse at rest | Instant crossfade | Immediate |
| Press | The content scales to 0.97 (0.96 when icon only) and springs back on release | No press scale | No press scale |
| Hover | The frame crossfades to the muted surface and the strong border | Instant | Immediate |

## Related

- `SplitButton` when copying is one of several actions, such as Copy page with Copy link and Copy as Markdown.
- `ActionButton` for an asynchronous submit with a pending and a success state.
- `Button` for a plain action with no clipboard result.
- `CodeBlock` for a code sample that already includes a copy control.

| Component | Choose it when |
|---|---|
| `SplitButton` | Copy is the primary action and the alternatives are formats or variants of the same value |
| `CodeBlock` | The copied text is a code sample that already shows its own copy control |
| `Input` (`IsReadOnly`) | The value must stay selectable and readable in full, with a copy action beside it |
| `ActionButton` | The action sends or saves something and needs a pending and success state |
| `DropdownMenu` | Copying is one of several row commands that belong in a menu |
