---
title: Token text editor
summary: A multi-line text editor whose tokens, such as commands and mentions, behave as single units, with triggers that open a query for your own list.
---

<Demo name="Preview" />

## When to use

The token text editor is the writing surface behind a prompt box, a comment field or a template editor: plain text in which
some pieces are more than text. A `/recap` command or an `@file` mention is one unit that you move over, delete and
select as a whole, and that your code can read back with its meaning.

- **A prompt box with commands and mentions**: "/" at the start of a line picks a command, "@" picks a file or a person.
- **A template editor with variables**: a variable such as the name of a customer, written between double braces, that must not be edited letter by letter.
- **A comment field**: mentions that notify someone, with the text kept readable.
- **Your own mention control**: `MentionInput` is built on this editor; use the editor directly when the tokens are not people and channels.

## When not to use

- **Plain multi-line text**: use `Textarea`.
- **@people and #channels with a ready-made list**: use `MentionInput`, which brings its own suggestion list and serialization.
- **Rich text** (bold, links, images): the tokens are the only formatting; the text is plain.
- **A single short value**: use `Input`, or `TagInput` for a list of values.

## Usage

`TokenTextEditor` derives from Avalonia's `TemplatedControl`. It holds a real `TextBox` with transparent text and paints the
same text behind it with the tokens tinted, so the caret, selection, input methods, undo and clipboard are the platform's own.
It draws no frame: put it in your own surface.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TokenTextEditor Placeholder="Type / for a command or @ for a file" MinLines="2" MaxLines="5">
        <arc:TokenTextEditor.Triggers>
            <arc:TokenTrigger Symbol="/" Kind="command" Position="LineStart" />
            <arc:TokenTrigger Symbol="@" Kind="mention" Position="AfterWhitespaceOrLineStart" />
        </arc:TokenTextEditor.Triggers>
    </arc:TokenTextEditor>
</UserControl>
```

### Triggers

A `TokenTrigger` is a symbol that opens a query: `Symbol`, the `Kind` of token it makes and where it may start
(`Position`): `Anywhere`, `AfterWhitespaceOrLineStart` (the start of the text, a line, or after a space) or `LineStart`
(only as the first character of a line). A query ends at a space unless `AllowSpacesInQuery` or `MaxQuerySpaces` allows
some, and another trigger's symbol ends it too. `AllowAfterOpeningBracket` lets a mention start right after a "(".

The editor shows no list. It raises `TriggerChanged` with the state of the query (the trigger, the text typed after it, and
the rectangles of the symbol and the caret to place your list); you filter your items and answer with `InsertToken(kind, id, label)`.
That replaces the typed query with the token and a space and leaves the caret after it. `CancelTrigger()` tells the editor your
list was dismissed.

<Demo name="Preview" />

### Tokens

`Tokens` lists the `TokenSpan`s of the current text: the `Kind`, `Id` and `Label` you gave and the `Start` and `End` offsets. They
move when the text before them changes and turn into plain text when someone edits inside one. Set a whole value
with `SetTextWithTokens(text, tokens)`, add one at the caret with `InsertTokenAtCaret`, and read changes in
`TextChanged`.

Arrow keys hop over a token, Backspace right after it and Delete right before it remove all of it, a click inside lands
on the nearest edge, and Shift with the arrows selects letter by letter (set `ExpandSelectionToTokens` to select whole tokens).
Undo and redo bring a removed token back with the text.

### Styling tokens

Every kind can have its own colors: add a `TokenStyle` with the `Kind`, a `Fill` and a `Foreground` to `TokenStyles`. Without one the
editor's `TokenFill` and `TokenForeground` apply. `TokenCornerRadius`, `TokenPadding` and `TokenOutset` shape the box; a token
that wraps over a line break is sliced by default (`TokenDecorationBreak`).

### Height and submitting

The height follows the text from `MinLines` to `MaxLines` lines and then the text scrolls. Enter raises `SubmitRequested` with the
text and tokens; Shift+Enter adds a line, and nothing is submitted while an input method is composing. Set `SubmitOnEnter` to false
to make Enter add a line. Mark the event handled to keep the text; clear it yourself when you want to start over.

<Demo name="Growth" />

### Reading the state

`TriggerState` is the current query (or null), `CaretIndex` the caret position and `IsComposing` whether an input method is
composing. `InsertText`, `Select`, `Clear`, `Undo` and `Redo` do what they say.

## API reference

<!-- api: TokenTextEditor -->

<!-- api: TokenTrigger -->

<!-- api: TokenStyle -->

### TokenSpan

| Property | Description |
|---|---|
| `Kind` | The kind you gave the token, such as `command` or `mention` |
| `Id` | Your identifier for what the token stands for |
| `Label` | The text after the symbol |
| `Start` / `End` | The range of the token in the text, symbol included |

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `TextChanged` | The text or the tokens changed | `OldText`, `OldTokens`, `Text`, `Tokens` |
| `TriggerChanged` | A query opens, changes or closes | `PreviousState`, `State` (null when closed) |
| `TokenInserted` | `InsertToken` made a token | `Token` |
| `SubmitRequested` | Enter without Shift, with no input method composing | `Text`, `Tokens`; set `Handled` to keep the text |

## Accessibility

- It is a text box for assistive technology: it keeps the platform's caret, selection and input method behavior, and the text it
  reads is the plain text, tokens included.
- While your list is open, set `SuggestionsExpanded` so the editor reports itself as expanded; it raises `CollapseRequested`
  when assistive technology asks to close it. Announce the number of matches in your own live region.
- Tokens are not read as separate controls: give them a clear label ("recap", not an id).
- Token colors keep their contrast in light and dark themes; do not carry a meaning by color alone.

### Keyboard

| Keys | Action |
|---|---|
| Typing a trigger symbol | Opens a query where the trigger's position rule allows it |
| `ArrowLeft` / `ArrowRight` | Moves the caret, hopping over a token in one step |
| `Backspace` after a token / `Delete` before it | Removes the whole token |
| `Shift` + arrows | Selects letter by letter |
| `Enter` | Raises `SubmitRequested` |
| `Shift+Enter` | Adds a line |
| `Ctrl/Cmd+Z`, `Ctrl/Cmd+Shift+Z` | Undo and redo, tokens included |

## Motion

- The height follows the text from `MinLines` to `MaxLines` in a short ease (160 ms by default; set `HeightMotion` to change it).
- Tokens are painted, not animated: a token appears the moment it is inserted and its tint follows the theme.
- Reduced motion and Off apply height changes at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Height grows or shrinks | Short ease | Immediate | Immediate |
| Token inserted or removed | Immediate | Immediate | Immediate |

## Related

- `MentionInput` for @people and #channels with a built-in list.
- `Textarea` for plain multi-line text.
- `TagInput` for a list of short values.

| Component | Choose it when |
|---|---|
| `MentionInput` | You need people and channels with suggestions out of the box |
| `Textarea` | The text has no tokens |
| `TagInput` | The value is a list of tags |
| `AiComposer` | You need a full prompt box: attachments, tools, model and send |
