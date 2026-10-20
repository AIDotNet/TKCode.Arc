---
title: Tag input
summary: Turn short text values into removable tags.
---

## When to use

- Free short values such as keywords, labels, topics or email addresses.
- Lists that the user builds by typing, where each entry is added with Enter or a comma.
- Lists that need a limit, using `MaxTags`.

## When not to use

- Values chosen from a fixed list: use `MultiSelect` or `ChipGroup`.
- A single value: use `Input`.
- Mentions of people or channels inside running text: use `MentionInput`.
- Several lines of free text: use `Textarea`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TagInput Label="Tags"
                  Placeholder="Add a tag"
                  Description="Press Enter or a comma to add a tag." />
</UserControl>
```

### Initial tags

`Value` holds the tags. It is an `IReadOnlyList<string>`, so set it in code or bind it to a collection.

```csharp
var tags = new TagInput
{
    Label = "Tags",
    Value = ["design", "motion", "react"]
};
```

### Limit and errors

`MaxTags` refuses adds past the limit. The field shakes and the limit is announced. An `ErrorMessage` marks the
field invalid and shows the error row.

```csharp
var skills = new TagInput
{
    Label = "Skills",
    Description = "Up to 5 skills.",
    MaxTags = 5,
    Value = ["Figma", "Avalonia", "C#", "Motion"]
};
```

```xml
<arc:TagInput Label="Labels" ErrorMessage="Add at least one label." />
```

### Pasted lists

Pasting text that contains commas, new lines or tabs splits it into tags. The last piece stays in the draft.

### Binding and events

`Value` is two-way. `ValueChanged` reports the old and new lists, plus the `Added` and `Removed` tags.

```xml
<arc:TagInput Label="Keywords" Value="{Binding Keywords, Mode=TwoWay}" />
```

```csharp
tags.ValueChanged += (_, e) =>
{
    Console.WriteLine($"Added: {string.Join(", ", e.Added)}; removed: {string.Join(", ", e.Removed)}");
};

tags.AddTag("release");
tags.FocusEditor();
```

## API reference

<!-- api: TagInput -->

## Accessibility

- `Label` names the field. With `IsLabelVisible="False"` the label is hidden but still names it.
- Each tag has a remove button named "Remove <tag>".
- Adding, removing, picking and duplicates are announced politely. A duplicate is not added: the existing tag pulses.
- Backspace at the start of the input picks the last tag. Delete removes the picked tag, and ArrowLeft and ArrowRight
  move the selection between tags and the input. Escape clears the selection.
- A draft is turned into a tag when the field loses focus.

## Motion

- A new tag fades in with a blur and a small scale where it was typed. A removed tag leaves, and the others glide to
  their new places.
- The selection ring slides between tags, and the shell height follows when tags wrap onto a new row.
- A duplicate pulses the existing tag once. A refused add shakes the field.
- Reduced motion removes the layout movement, the scale and the blur, and keeps a short fade. Off motion jumps to the end state.

## Related

- `MultiSelect` or `ChipGroup` when the values come from a fixed list.
- `Input` for a single value.
- `MentionInput` for @people and #channel references in running text.
