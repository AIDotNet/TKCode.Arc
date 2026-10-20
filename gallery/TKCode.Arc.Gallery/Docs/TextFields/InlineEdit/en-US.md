---
title: Inline edit
summary: Rename in place: the text becomes a field without moving.
---

## When to use

- A name, title or description that is read often and changed rarely, such as a project name.
- A single value that is saved on its own, without a form around it.
- Text that should look like plain content until the user chooses to edit it.

## When not to use

- Several fields saved together: use a form built from `Input` or `Textarea`.
- A field that must always look editable: use `Input`.
- A multi-line body that the user writes in a form: use `Textarea`.

## Usage

The value is shown as text. Activating it turns it into a field of the same size. The value is saved when the
edit is committed.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:InlineEdit Label="Project name"
                    Value="Atlas redesign"
                    HeadingLevel="2" />
</UserControl>
```

### Save and validation

`SaveHandler` persists the committed value. It runs while the field shows the saving state. A faulted task rolls
the text back and shows a retry link. `Validator` returns a message when the draft cannot be saved; the message
is shown under the field.

```csharp
title.Validator = value => value.Length == 0 ? "A project needs a name." : null;

title.SaveHandler = async next =>
{
    await projects.RenameAsync(projectId, next);
};
```

Handle `TextCommitted`, `Saved` and `SaveFailed` to react to the outcome.

```csharp
title.TextCommitted += (_, e) => log.Text = $"Committed “{e.NewValue}” (was “{e.OldValue}”)";
title.Saved += (_, e) => log.Text = $"Saved “{e.NewValue}”";
title.SaveFailed += (_, e) => log.Text = $"Save failed: {e.Error.Message}";
```

### Variants and multiline

`Variant` sets the visual weight. `Title` is the default for names and headings; `Body` suits descriptions.
With `IsMultiline` set, the text wraps and the field grows in height. Enter still saves, and Shift+Enter adds a
line break.

```xml
<arc:InlineEdit Label="Description"
                Variant="Body"
                IsMultiline="True"
                PlaceholderText="Add a description"
                Value="{Binding Description}" />
```

### Binding

`Value` is two-way. A new value from outside replaces the text while the user is not editing it.

```xml
<arc:InlineEdit Label="Project name"
                Value="{Binding ProjectName}"
                HeadingLevel="2" />
```

## API reference

<!-- api: InlineEdit -->

## Accessibility

- The resting text is a button named with the label and the value, for example "Project name: Atlas redesign".
- `HeadingLevel` reports the text as a heading. Support for it depends on the platform.
- Enter or Space starts editing. Enter saves, Escape cancels and restores the saved value, and Tab saves when
  focus leaves the field.
- Validation messages and save errors are announced politely and linked to the field as help text. Saving progress
  and the result are announced through a status region.

## Motion

- The old value scrolls out and the new one rises in with a soft blur. A failed save rolls back the other way and
  briefly tints the frame.
- The width follows the draft while typing, and the box changes height smoothly when it is multiline.
- While saving, a small spinner turns. When the save lands, a check mark is drawn.
- Reduced motion cross-fades the text, draws the check at once and replaces the spinner with a static mark. Off does
  the same without the fade.

## Related

- `Input` for a field that is always visible and saved with a form.
- `Textarea` for a longer body text inside a form.
- `ExpandingSearch` for a control that opens a search field and results.
