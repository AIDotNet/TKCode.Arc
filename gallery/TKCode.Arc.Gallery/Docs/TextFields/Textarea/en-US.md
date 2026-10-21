---
title: Textarea
summary: A multiline field for notes, descriptions, and longer text.
---

<Demo name="LiveCount" />

## When to use

- Free text over several lines, such as a bio, a comment or feedback.
- Longer values where a live character count helps the user stay within a limit.
- Text the user may want to read back and edit as a block.

## When not to use

- A single line such as a name or an email address: use `Input`.
- A secret value: use `PasswordField`.
- Editing a title or description in place on a page: use `InlineEdit`.
- A short list of values: use `TagInput`.

## Usage

Enter adds a line. The field fills its column and is at least 110 px tall.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Textarea Label="Bio"
                  Placeholder="A sentence or two about you"
                  Description="160 characters left"
                  MaxLength="160"
                  Width="420"
                  HorizontalAlignment="Left" />
</UserControl>
```

### Live count and validation

Update the description as the text changes. Only the digits that change roll. Set `ErrorMessage` to mark the
field invalid; an empty value clears the error.

<Demo name="Validation" />

```csharp
const int limit = 160;
bio.TextChanged += (_, _) =>
{
    var length = bio.Text?.Length ?? 0;
    bio.Description = $"{limit - length} characters left";
    bio.ErrorMessage = length < 40 ? "Tell us a little more: at least 40 characters." : null;
};
```

### Bind the text

`Text` is a two-way property. Bind it to a view model string.

```xml
<arc:Textarea Label="Feedback"
              Text="{Binding Feedback}"
              ErrorMessage="{Binding FeedbackError}" />
```

### Resizing

The corner grip drags the field taller. The height starts at the minimum and does not follow the content;
the field scrolls instead. Turn the grip off with `IsResizable`.

```xml
<arc:Textarea Label="Notes"
              IsResizable="False"
              Text="Fixed height." />
```

When the user drags the grip, `FrameHeight` holds the chosen height in pixels. Set it from code to start
the field at a given height.

```csharp
notes.FrameHeight = 200;
```

### Disabled

A disabled field is faded to 0.6 and has no grip.

<Demo name="Disabled" />

## API reference

<!-- api: Textarea -->

## Accessibility

- `Label` names the field. `Description` and `ErrorMessage` are merged into its help text.
- An error is announced assertively when it appears. Animated copy is hidden from assistive technology, which reads
  a plain version instead.
- Enter inserts a line break. Caret, selection, IME, undo and clipboard shortcuts are the native `TextBox` behavior.
- Focus draws a 3 px focus ring around the frame.

## Motion

- The helper and error row opens its height on a smooth spring. Changed words rise in with a soft blur.
- The character count rolls only the digits that change.
- Reduced motion removes the spring, the digit roll and the blur, and changes the copy at once. Off does the same.

## Related

- `Input` for a single line of text.
- `InlineEdit` to edit a value in place without a visible frame.
- `TagInput` for a list of short values rather than free text.
- `RichTextEditor` for text that needs headings or lists.
