---
title: Copy button
summary: Copy a value with immediate confirmation.
---

## When to use

- A value that the user copies often, such as an API key, an install command or a link.
- Code snippets and inline values where the confirmation should appear in place.
- Dense rows where an icon-only copy action is enough.

## When not to use

- Copying the value plus other formats, such as Copy link or Copy as Markdown: use `SplitButton`.
- A complete code sample with its own copy control: use `CodeBlock`, which already has one.

## Usage

Set `Value` to the text that goes to the clipboard. `Label` is the visible text and the accessible name.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CopyButton Value="npx arc add copy-button" />
</UserControl>
```

### Custom label

`Label` sets the idle text. `CopiedLabel` and `FailedLabel` set the words shown after a copy. The label cell reserves
the widest of the three, so the button never changes width.

```xml
<arc:CopyButton Value="sk_live_51H..."
                Label="Copy key"
                CopiedLabel="Key copied"
                FailedLabel="Could not copy" />
```

### Icon only and plain variants

`IsIconOnly` hides the label. The label stays the accessible name. `Variant="Plain"` drops the outline, which suits
dense rows and code blocks.

```xml
<arc:CopyButton Value="https://uiarc.dev" IsIconOnly="True" />
<arc:CopyButton Value="#7C5CFF" Label="Copy hex" Variant="Plain" />
```

### Binding the value

Bind `Value` to the text that should be copied. A value of `null` makes the copy fail, and the button shows the
failed state.

```xml
<arc:CopyButton Value="{Binding InstallCommand}" Label="Copy command" />
```

### Reset time and events

The button returns to idle after `ResetAfter`, which is 1.9 seconds by default. `Copied` is raised after a successful
copy. `State` reports `Idle`, `Copied` or `Error`.

```csharp
copy.Copied += (_, _) => log.Text = "Copied to the clipboard.";
copy.ResetAfter = TimeSpan.FromSeconds(3);
```

## API reference

<!-- api: CopyButton -->

## Accessibility

- The accessible name is always `Label`, including when the button is icon only.
- A hidden polite status region announces the result, such as "Copy key: Copied" or "Copy key: Could not copy".
- Enter and Space copy the value.
- The label cell keeps the width of its widest state, so the layout around the button does not move.
- Keyboard focus shows the standard focus ring. Pointer focus draws no ring.

## Motion

- The icon swaps with a soft blur and scales in on a slow, nearly critically damped spring. The check draws itself.
- Only the changed letters of the label move, and they play back in reverse when the button returns to idle.
- A press scales the content down slightly and a release springs it back.
- Reduced motion uses an instant crossfade for the icon and the text, skips the stroke drawing and the press scale. Off
  replaces the icon and the label at once.

## Related

- `SplitButton` when copying is one of several actions, such as Copy page with Copy link and Copy as Markdown.
- `ActionButton` for an asynchronous submit with a pending and a success state.
- `Button` for a plain action with no clipboard result.
- `CodeBlock` for a code sample that already includes a copy control.
