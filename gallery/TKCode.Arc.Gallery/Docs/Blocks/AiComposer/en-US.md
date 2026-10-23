---
title: AI composer
summary: The prompt box of an AI app: a text area that grows, slash commands and @ mentions that become tokens, attachments that upload in place, tool toggles, a model and effort menu, a character counter and a send button that becomes a stop button.
---

<Demo name="Preview" />

## When to use

The AI composer is the box people write their prompt in. It is one rounded surface with a text area on top and a toolbar under
it, and it takes care of everything between typing and sending: commands, files, tools, the model and the limit.

- **A chat or assistant app**: the box at the bottom of the conversation, with a stop button while a reply is generated.
- **A prompt field in a tool**: "/" commands for the actions of your product, "@" mentions for the files or documents it can read.
- **A form that accepts files with the prompt**: pick, paste or drop files; they upload in place and the message carries only the ones that are ready.
- **A model choice next to the prompt**: the toolbar holds a model pill with an effort setting, so the choice stays where the work happens.

## When not to use

- **A short single-line field**: use `Input`, or `SearchField` for searching.
- **A comment box with @people**: use `MentionInput`, which brings people, channels and serialization.
- **A text editor with your own tokens and your own surface**: build on `TokenTextEditor`, the editor inside the composer.
- **Choosing a model on a settings page**: use `ModelPicker`; the composer's pill is for choosing while writing.
- **Running the model**: the composer only reports the message. Your code calls the model and says when a reply is being generated.

## Usage

`AiComposer` derives from Avalonia's `TemplatedControl`. Give it what the person can use (commands, context items, tools, models)
and handle `Sent`. It clears itself after a send; the message holds the text, the tokens, the ready attachments, the model, the effort
and the state of each tool.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiComposer Placeholder="Ask a question, type / for actions or @ for a file">
        <arc:AiComposer.Commands>
            <arc:AiCommand Id="recap" Name="recap" Description="Boil a doc or file down to its main points" />
            <arc:AiCommand Id="rewrite" Name="rewrite" Description="Rewrite in a clearer or friendlier tone" />
        </arc:AiComposer.Commands>
        <arc:AiComposer.ContextItems>
            <arc:AiContextItem Id="style-guide" Label="style-guide.pdf" Hint="Brand, 3.1 MB" Group="Files" />
        </arc:AiComposer.ContextItems>
    </arc:AiComposer>
</UserControl>
```

```csharp
composer.Sent += (_, e) =>
{
    var message = e.Message;               // Text, Tokens, Attachments, Model, Effort, Tools
    StartReply(message);
    composer.IsGenerating = true;          // the send button becomes a stop button
};
composer.StopRequested += (_, _) => CancelReply();
```

### Commands and mentions

"/" at the start of a line opens the list of `Commands` (filtered by the start of the name); "@" at the start of a line or after a space
opens the list of `ContextItems`, grouped by their `Group` in the order of `MentionGroups` ("Files" then "Docs" by default) and
filtered by the label or the hint. A "(" before the "@" does not open it. Up and Down move (and wrap), Enter or Tab inserts, Escape
closes, and the list never takes the focus. An insertion becomes a token followed by a space; `Value.Tokens` lists them with their
offsets. With no commands or no items, the symbol is plain text.

`SetPrompt(text)` fills the box for a suggestion: the commands and mentions it knows become tokens, the caret goes to the end and the
focus to the text. `InsertToken(kind, id, label)` adds one at the caret.

<Demo name="Suggestions" />

### Attachments

The plus button opens a menu with "Upload file", your `AttachActions` (a cloud drive, a repository, a screen capture, with a separator
if you ask) and, when there are context items, "Mention a file or doc". Files can also be pasted or dropped on the composer. A paste of
at least `LongPasteCharacters` characters or `LongPasteLines` lines becomes a "Pasted text" attachment instead of text.

Each file becomes a chip that uploads in place: a ring with the percentage while it runs, a check when it finishes, then a remove button. A file over
`MaxFileSize` (30 MB by default) or not matching `AcceptedFileTypes` (the syntax of `FileDropzone.Accept`) gets an error chip with its
reason; it can be removed and does not stop sending. `UploadHandler` does the upload: report progress, honor the token, throw to refuse.
Without a handler a file is ready at once. `IsAttachmentEnabled` turns the plus, pasting and dropping off.

<Demo name="Attachments" />

### Tools, model and effort

Each `AiComposerTool` in `Tools` is a toggle in the toolbar (an icon and a label that hides in a narrow composer); its `IsChecked` is two-way
and `ToolToggled` reports a change. When `Models` is not empty, a pill shows the selected model and its effort; its menu has the models,
a separator and a row of effort levels that apply at once. `SelectedModel` and `Effort` are two-way, `ModelChanged` and `EffortChanged`
report a change, and an effort the model does not offer moves to the nearest level it does.

### The limit

`MaxLength` (4000 by default; null means none) adds a counter: from `CounterThreshold` (80%) it shows how many characters are left, from
`WarningThreshold` (95%) it turns to the warning color, and over the limit it shows how many are over and sending is blocked. The text is
never cut.

<Demo name="Limit" />

### Sending and stopping

The send button is empty (nothing to send), ready, blocked (an upload is still running, or the text is over the limit; its name says
which) or generating. Enter sends; Shift+Enter adds a line; Enter does nothing while an input method is composing, while a list is
open, or while a reply is generated (the box stays editable, nothing is queued). While `IsGenerating` is true the button is a stop
square inside a ring that follows `GenerationProgress` and never goes backwards; press it, or Escape, to raise `StopRequested`.
Your code ends a reply by setting `GenerationProgress` to 1 and then `IsGenerating` to false.

`SendCommand` and `StopCommand` do the same for MVVM. `SendState` tells which of the four states the button is in.

### A minimal composer

<Demo name="Minimal" />

### Wording

Every word comes from `Labels` (`AiComposerLabels`): the placeholder, the names of the buttons, the counter, the chip texts, the drop
cover and the announcements. Replace the instance to translate or reword; formatted texts are functions.

### Content guidelines

- **Placeholder**: say what the box takes, in one line: "Ask a question, type / for actions or @ for a file".
- **Commands**: a short verb ("recap", "rewrite") and one line that says what it does.
- **Context items**: a name people recognize and a hint that tells apart two with the same name (a path, a source).
- **Errors on chips**: a few words ("Over 30 MB", "Type not accepted").

### Best practices

- Put the composer at the bottom of the conversation and give it the width of the thread; it adapts to its own width (compact below 680 px,
  narrower below 560 and 400).
- Set `IsGenerating` as soon as you start a reply, and always handle `StopRequested`.
- Validate on your server too: the composer limits what it sends, it is not a security boundary.
- Do not queue messages silently while a reply runs; the composer does not.

### Common questions

**Why did the box clear after sending?** It always clears the text, the tokens and the attachments; tools, model and effort stay. Keep the
message if you need to restore it.

**Where is the voice input?** Not part of this version.

## API reference

<!-- api: AiComposer -->

<!-- api: AiCommand -->

<!-- api: AiContextItem -->

<!-- api: AiAttachment -->

<!-- api: AiComposerTool -->

<!-- api: AiComposerAction -->

### AiComposerMessage

| Property | Description |
|---|---|
| `Text` / `Tokens` / `Value` | The prompt, the tokens in it and both together |
| `Attachments` | The attachments that are ready; failed or still uploading ones are left out |
| `Model` / `Effort` | The selected model and effort |
| `Tools` | For each tool id, whether it was on |

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `Sent` | The person sends the prompt | `Message` |
| `StopRequested` | The person stops a reply being generated | |
| `AttachmentAdded` / `AttachmentRemoved` | An attachment is added, removed or its upload cancelled | `Attachment` |
| `ToolToggled` | A tool is turned on or off | `Tool` |
| `ModelChanged` / `EffortChanged` | The person picks a model or an effort | The old and new value |

## Accessibility

- The text area has an accessible name ("Message"); the lists of commands and mentions are lists whose rows are named by their title and hint,
  and the highlighted row is reported through the text area, so the focus stays where the person types.
- The send button's name says what it does or why it cannot: "Send message", "Type a message to send", "Wait for uploads to finish",
  "Shorten the message by N characters", "Stop generating".
- The toolbar buttons are named; tool toggles report whether they are pressed; the plus button reports that it opens a menu.
- A polite live region announces the number of suggestions, tools turned on or off, model and effort changes, finished and failed uploads, removals, long
  pastes, a sent message and a stop.
- Chips have named buttons ("Cancel upload of report.pdf", "Remove report.pdf"); when the last chip is removed the focus moves to the text.
- Focus goes where the person would look next: back to the plus button after Escape in its menu, to the text after choosing an entry, back to the model pill after a choice.
- Status never relies on color alone: errors add an icon and a reason, the counter adds a number.
- Reduced motion keeps every state and announcement.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` | Sends (not while composing, a list is open, or a reply is generated) |
| `Shift+Enter` | Adds a line |
| `/` at the start of a line, `@` after a space | Opens the list of commands or mentions |
| `ArrowUp` / `ArrowDown` in a list | Moves between rows; wraps around |
| `Enter` or `Tab` in a list | Inserts the row as a token |
| `Escape` | Closes a list or menu; with none open, stops a reply being generated |
| `Enter`, `Space` or `ArrowDown` on the plus or the model pill | Opens its menu |
| `ArrowLeft` / `ArrowRight` on the effort row | Changes the effort |
| `Tab` | Moves through the plus, the tools, the counter, the model, the send button and the chips |

## Motion

- The text area grows line by line from `MinLines` to `MaxLines` (1 to 8) in a 160 ms ease, then scrolls.
- The border deepens when the composer has focus (160 ms); the drop cover fades in when files are dragged over.
- Chips come in at a scale of 0.92 while fading; the ring becomes a check and then a remove button on a spring.
- The send button's arrow turns into a stop square on a spring while the ring around it fades in; the ring follows the progress and never goes back.
- Menus and the suggestion list grow from their button or caret; the list's height follows its rows on a spring.
- The counter and the model pill's text take the width they need on a spring.
- Reduced motion keeps 120 ms fades and drops movement and springs. Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Height of the box | 160 ms ease | Immediate | Immediate |
| Border and hover colors | 160 ms | 120 ms | Immediate |
| Send to stop | Shape change on a spring, ring fades in | Swap with a 120 ms fade | Immediate |
| Chip enters or leaves | Scale and fade | 120 ms fade | Immediate |
| Menus and the suggestion list | Grow from the anchor | 120 ms fade | Immediate |

## Related

- `ModelPicker` for choosing a model on a settings page.
- `TokenTextEditor` for the text area with tokens by itself.
- `MentionInput` for comments with people and channels.
- `AgentMemory` for what the assistant remembers.

| Component | Choose it when |
|---|---|
| `ModelPicker` | The model choice is not tied to writing a prompt |
| `TokenTextEditor` | You need the editor, not the whole box |
| `MentionInput` | The mentions are people and channels in a comment |
| `Textarea` | The text has no commands, files or tools |
