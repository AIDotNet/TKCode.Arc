---
title: Rich text editor
summary: A lightweight editor with markdown shortcuts, a floating toolbar, a slash menu, and HTML and markdown output.
---

## When to use

- Comments, notes, posts and short or medium documents that need bold, italic, lists, quotes and code blocks.
- Content that is saved as HTML or Markdown and read back into the editor.
- Writing that needs the editor's own undo history, which is kept separate from the platform's undo.

## When not to use

- Plain multi-line text with no formatting: use `Textarea`.
- Mentions of people or tags inside a text field: use `MentionInput`.
- Single-line text that is edited in place on a page: use `InlineEdit`.
- Source code or a read-only code listing: use `CodeBlock`.

## Usage

Set `Markdown` for the initial content. Both `Markdown` and `Html` are two-way: assigning either one reloads the
editor, and both follow every edit.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RichTextEditor Markdown="## Notes&#10;&#10;Type **Markdown** as you go, or `/` for blocks." />
</UserControl>
```

### Placeholder and block hint

`Placeholder` shows in an empty editor. `BlockHint` shows on the empty line that holds the caret.

```xml
<arc:RichTextEditor Placeholder="Leave a comment"
                    BlockHint="Type / for blocks" />
```

### Read only

With `IsReadOnly` set, editing, the toolbar and the shortcuts are off. Text can still be selected and copied.

```xml
<arc:RichTextEditor IsReadOnly="True"
                    Markdown="## Release checklist&#10;&#10;1. Freeze the branch&#10;2. Run the **full** test suite" />
```

### Reading the content

`ContentChanged` runs after every edit. Its `Value` holds `Html`, `Markdown`, `Text` and `IsEmpty`. The methods
`GetHtml()`, `GetMarkdown()` and `GetText()` read the content at any time.

```csharp
editor.ContentChanged += (_, e) =>
{
    draft.Markdown = e.Value.Markdown;
    canPost = !e.Value.IsEmpty;
};

var html = editor.GetHtml();
```

### Your own toolbar

`Formats` reports the marks and block type at the caret, and `CanUndo` and `CanRedo` report the history. Use them
to set the state of your own buttons. `ToggleMark`, `SetBlockType`, `SetLink` and `EditLink` drive the editor.

```csharp
editor.PropertyChanged += (_, e) =>
{
    if (e.Property == RichTextEditor.FormatsProperty)
    {
        boldButton.Variant = editor.Formats.IsBold ? ButtonVariant.Secondary : ButtonVariant.Ghost;
    }
};

boldButton.Click += (_, _) => editor.ToggleMark(RichTextMark.Bold);
headingButton.Click += (_, _) => editor.SetBlockType(RichTextBlockType.Heading2);
```

Bind `UndoCommand` and `RedoCommand` to buttons, or call `Undo()` and `Redo()` directly.

## API reference

<!-- api: RichTextEditor -->

## Accessibility

- The editing surface is a multi-line text control. It is named "Editor" by default; set `AutomationProperties.Name` to
  name it for your form.
- Keyboard: Mod+B, Mod+I, Mod+Shift+X, Mod+E and Mod+K apply marks and links. Mod+Z and Mod+Shift+Z undo and redo.
  Alt+F10 moves focus into the floating toolbar, and Escape returns it to the text.
- Editing results such as "Formatted", "Link added" and "Undone" are announced politely.
- Limitation: the editor does not expose character or line navigation to screen readers. They read the whole text
  as one value.

## Motion

- The floating toolbar slides to follow the selection. It morphs in size when it changes between the format buttons
  and the link field, and the two faces slide past each other.
- The block menu opens from the `/` position and its highlight slides between rows as the list is filtered.
- Reduced motion moves and resizes the toolbar in one step and cross-fades its two faces. The block menu only fades
  and does not scale or move.

## Related

- `Textarea` for plain text without formatting.
- `MentionInput` for text that mentions people or tags.
- `CodeBlock` for displaying code rather than writing it.
