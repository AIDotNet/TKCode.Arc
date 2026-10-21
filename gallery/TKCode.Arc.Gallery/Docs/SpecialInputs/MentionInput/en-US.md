---
title: Mention input
summary: A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.
---

<Demo name="CommentBox" />

## When to use

- Comment and reply boxes where a mention must notify a person by id.
- Chat and task editors that link a channel or project inside running text.
- Editors that store mentions as structured data, not as plain text.

## When not to use

- Plain multi-line text with no references: use `Textarea`.
- A list of values that are not part of a sentence: use `TagInput`.
- Titles, lists and links inside the text: use a rich text editor.
- Single-line text such as a name or a search query: use `Input` or `SearchField`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MentionInput Label="Comment"
                      Placeholder="Leave a comment. Use @ to mention, # for a channel"
                      MinLines="1"
                      MaxLines="8" />
</UserControl>
```

The field has no visible label. `Label` only names the field for assistive technology.

### People and channels

`People` and `Channels` supply the suggestions. Leave one empty to turn off that trigger. Each person and channel has
an id, a name and optional details.

```csharp
var people = new[]
{
    new MentionPerson("u_maya", "Maya Chen") { Role = "Product designer" },
    new MentionPerson("u_jordan", "Jordan Lee") { Role = "Engineering manager" }
};

var channels = new[]
{
    new MentionChannel("c_design", "design") { Description = "Critiques, reviews and handoff", Members = 48 }
};

var comment = new MentionInput
{
    Label = "Comment",
    People = people,
    Channels = channels,
    MaxSuggestions = 6
};
```

### Prefilled value

`Value` is a `MentionValue`: plain text plus the mentions with their offsets. Editing around a mention moves it, and
editing inside it removes it.

<Demo name="Prefilled" />

```csharp
var text = "Thanks @Maya Chen. Can @Jordan Lee review it in #design?";
comment.Value = new MentionValue(text,
[
    new Mention(MentionKind.Person, "u_maya", "Maya Chen", 7, 17),
    new Mention(MentionKind.Person, "u_jordan", "Jordan Lee", 24, 34),
    new Mention(MentionKind.Channel, "c_design", "design", 47, 54)
]);
```

### Submit on Enter

With `IsSubmitOnEnter`, Enter submits and Shift+Enter adds a line. Enter never submits while an input method is
composing text.

<Demo name="SubmitOnEnter" />

```csharp
message.IsSubmitOnEnter = true;
message.Submitted += (_, e) =>
{
    Console.WriteLine($"{e.Value.Text} ({e.Value.Mentions.Count} mentions)");
    message.Clear();
};
```

### Size and placement

`MinLines` and `MaxLines` set the height range. `SuggestionPlacement` sets where the list opens: `Auto` picks the side
with more room, `Top` and `Bottom` force a side.

<Demo name="Growth" />

<Demo name="Placement" />

### Commands and events

`Insert`, `OpenSuggestions` and `Clear` act on the field from code. `ValueChanged` and `MentionAdded` report edits.

```csharp
button.Click += (_, _) => comment.OpenSuggestions(MentionKind.Person);

comment.MentionAdded += (_, e) => Console.WriteLine($"Added {e.Mention.Text}");
```

### Disabled

The whole field fades to 55% and takes no input.

<Demo name="Disabled" />

### Serialized output

`MentionSerializer.Serialize` turns the value into storage text: `<@id>` for people and `<#id>` for channels.

<Demo name="Serialized" />

## API reference

<!-- api: MentionInput -->
<!-- api: MentionPerson -->
<!-- api: MentionChannel -->

## Accessibility

- The text box is a combo box with list autocomplete, named by `Label`.
- Suggestions are a list named People or Channels. The number of results, such as "3 people" or "No matches", is announced politely.
- Mention colors are drawn on a hidden layer, so assistive technology reads plain text. Escape closes the suggestions only.

## Motion

- The field grows and shrinks between `MinLines` and `MaxLines` as the content changes.
- The suggestion panel opens from the caret with a small scale, and the highlight glides between rows.
- The panel height springs when the result count changes.
- Reduced motion jumps the height and the highlight, and the panel only fades. Off motion shows and hides it at once.

## Related

- `Textarea` for plain multi-line text.
- `TagInput` for a list of short values that are not part of a sentence.
- `Input` for a single line.
