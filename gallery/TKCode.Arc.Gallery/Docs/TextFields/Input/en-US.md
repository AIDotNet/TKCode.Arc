---
title: Input
summary: A single line field with clear labels and useful states.
---

## When to use

- Single-line values such as a name, an email address or a URL.
- Validation copy that follows the value while it is typed.

## When not to use

- Several lines of text: use `Textarea`.
- Values with a fixed shape: use `PasswordField`, `SearchField` or `NumberField`.
- Editing text in place on a page: use `InlineEdit`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Input Label="Email"
               Placeholder="you@example.com"
               Description="We never share it." />
</UserControl>
```

### Helper and error copy

Derive the copy from state and change the string. Only the words that change move, and numbers roll digit by
digit, so do not swap between separate messages.

```xml
<arc:Input Label="Username"
           Text="{Binding UserName}"
           Description="{Binding UserNameHint}"
           ErrorMessage="{Binding UserNameError}" />
```

```csharp
input.TextChanged += (_, _) =>
{
    var text = input.Text ?? string.Empty;
    input.Description = $"3 to 20 characters. {Math.Max(0, 20 - text.Length)} left.";
    input.ErrorMessage = text.Length is > 0 and < 3 ? "Use at least 3 characters." : null;
};
```

A non-empty `ErrorMessage` makes the field invalid. While it is empty, a validation error from the `Text`
binding shows instead.

### Sizes

`Size` sets the frame height: `Small` is 36, `Medium` is 44 (the Arc look) and `Large` is 50.

```xml
<arc:Input Label="Small" Size="Small" />
<arc:Input Label="Large" Size="Large" />
```

### Prefix and suffix

```xml
<arc:Input Label="Price" Text="49">
    <arc:Input.Prefix>
        <TextBlock Text="$" />
    </arc:Input.Prefix>
    <arc:Input.Suffix>
        <TextBlock Text="USD" />
    </arc:Input.Suffix>
</arc:Input>
```

## API reference

<!-- api: Input -->

## Accessibility

- `Label` names the field. With `IsLabelVisible="False"` the label is hidden but still names it.
- The description and the error are the field's help text.
- Animated words are hidden from assistive technology, which reads a plain copy instead.
- Caret, selection, IME, undo and clipboard shortcuts are the native `TextBox` behavior.

## Motion

- Helper and error rows open their height on a smooth spring. Changed words rise in and unblur, and numbers roll
  digit by digit.
- Reduced motion shows the rows at full height and swaps words with an instant fade. Off jumps to the end state.
- Hover and focus only darken the border; nothing changes size.

## Related

- `Textarea` for several lines, `PasswordField` for secrets and `SearchField` for search.
- `Field` and `FieldFrame` give any editor or trigger the same label, frame and message rows.
