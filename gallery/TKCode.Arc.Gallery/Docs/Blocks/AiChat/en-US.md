---
title: AI chat
summary: A chat with an assistant that fills its container: a title menu, a model button and a new chat button on top, a thread with streamed Markdown replies, collapsible reasoning, tool chips, numbered citations and message actions, and a message box at the bottom.
---

<Demo name="Preview" />

## When to use

The AI chat is the whole conversation surface of an assistant: the list of past chats, the thread, the model choice and the box
where people write. Replies arrive as a stream, show what the model did on the way (reasoning, searches, queries) and cite
their sources.

- **The main assistant of a product**: a page or a drawer where people ask, follow up, retry and rate.
- **A multi-turn helper inside a dashboard card**: it adapts to its own width, down to a phone-sized column.
- **A place where answers must be checked**: citations preview their source on hover, and tool chips open to show the search
  terms or the query that was run.
- **A conversation that is edited**: editing a message or retrying a reply makes a new version of the turn; the person can go
  back to the earlier one.

## When not to use

- **Only a prompt box**: use `AiComposer`.
- **A person-to-person chat** with participants and read receipts: use `ChatThread`.
- **One question and one long answer**: a page of text with `Markdown` is simpler.
- **Choosing a model on a settings page**: use `ModelPicker`.
- **Running the model**: the chat does not call anything by itself. It asks your `IAiChatResponder`, or it waits for your code to fill the reply.

## Usage

`AiChat` derives from Avalonia's `TemplatedControl` and fills its container, so give it a height. Set a `Responder` to answer, give it
`Models` and a conversation, and handle the events you need.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiChat Height="640" ColumnWidth="760" GreetingText="What should we look at today?">
        <arc:AiChat.Models>
            <arc:AiModel Id="orbit-pro" Name="Orbit Pro 3" Note="Northwind Labs" />
            <arc:AiModel Id="quill-2" Name="Quill 2.4" Note="Fernwood AI" />
        </arc:AiChat.Models>
    </arc:AiChat>
</UserControl>
```

### Answering with a responder

An `IAiChatResponder` receives the messages of the shown branch and returns a stream of events. The chat shows each event as it
arrives; you never touch the views.

```csharp
public sealed class MyResponder : IAiChatResponder
{
    public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AiChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AiChatReasoningDelta("Compare the stage timings before and after the jump.");
        yield return new AiChatReasoningEnd();
        yield return new AiChatToolUpdate("search", AiChatToolKind.Search, AiChatToolStatus.Running, "Searching the docs");
        // ... the same id again with Status Done, a summary, the queries and the sources
        yield return new AiChatSourcesEvent(sources);              // before the text that cites them
        await foreach (var chunk in model.StreamAsync(request, cancellationToken))
        {
            yield return new AiChatTextDelta(chunk);                // any size; the chat shows whole words on a steady clock
        }
    }
}
```

The events are `AiChatReasoningDelta` and `AiChatReasoningEnd` for the reasoning, `AiChatToolUpdate` for a tool call (updates with the same id change the
same tool), `AiChatSourcesEvent` for the sources, `AiChatTextDelta` for the reply and `AiChatErrorEvent` for a failure. Throwing an exception does the
same as the error event. The token is cancelled when the person stops the reply.

<Demo name="Empty" />

### The thread

A conversation is an `AiChatConversation` with a title and its `Turns`. Each `AiChatTurn` has versions; a version holds the prompt (`AiChatUserMessage`: text and
attachments), the reply (`AiChatReply`) and the turns that followed it. What is shown is the **visible branch**: the selected version of each turn, then into its
continuation. The responder receives only that branch.

A reply carries its status (thinking, streaming, done, stopped or failed), the reasoning, the tool calls (`AiChatTool`), the sources (`AiSource`), the text
and the person's feedback. Text is the Markdown subset: paragraphs, lists, short headings, bold, inline code, code blocks and `[1]` citations numbered from the
sources in the order they arrived.

### Reasoning, tools and citations

While the model thinks, the head of the reply shows a shimmering "Thinking"; when it is done it reads "Thought for 3s" and opens to the reasoning. Each tool call is
a chip: running (a spinner, not yet openable), done (an icon, the marks of the sources and a short summary such as "3 sources") or failed. A search chip opens to its search
terms and numbered sources; a query chip to its statement and result; a custom tool to your own template (`ToolDetailTemplate`).

A `[1]` in the text becomes a small numbered pill. Pointing at it or focusing it shows a card with the site, the title and an excerpt; pressing it raises
`CitationInvoked`, and unless you handle the event the source's `Url` opens.

### Versions

Retry on a finished reply adds a version of the turn; editing a message and sending it adds a version too. A switcher ("2 / 2") appears in the action bar, and the turns after a
version belong to it, so going back to version 1 brings its follow-ups back. Retry on a failed reply runs again in place and adds no version.

<Demo name="Versions" />

### Stopping and failing

While a reply is generated the send button is a stop button and Escape in the box stops it. A stopped reply keeps what was shown and says "Stopped" (or "Stopped before
answering"); a failed one shows an error card with a Retry button. Both keep their action bar except a failed reply, which has none.

### Without a responder

With no `Responder`, a sent message only appears in the thread and raises `MessageSent`; your code fills the `AiChatReply` it gets, and the view follows its changes.

<Demo name="HostDriven" />

### Conversations, models and new chats

The title button opens the list of `Conversations`, grouped by `Group`, with "New chat" first. Opening one raises `ConversationOpened`; each conversation keeps its own scroll
position and finished replies are not played again. The model button opens `Models`; `SelectedModel` is two-way and `ModelChanged` reports the change. New replies use the
selected model; replies that exist keep the mark of the model that wrote them. "New chat" raises `NewChatRequested`; if nothing handles it, the chat clears
`CurrentConversation` and shows the empty chat with its greeting and up to three `Suggestions`. The first message in an empty chat creates a conversation
(`ConversationCreated`) titled from its first six words.

### Width

The chat follows its own width: at 480 and below the bubbles widen and source domains hide, at 360 and below the buttons tighten. `ColumnWidth` (760) sets the width of the column of turns.

<Demo name="Narrow" />

### Wording

Every word comes from `Labels` (`AiChatLabels`): replace the instance to translate or reword; formatted texts are functions. `GreetingText` and `PlaceholderText` win over the labels when set.

### Content guidelines

- **Greeting**: a question that invites a task ("What should we look at today?").
- **Suggestions**: three at most, each a real task phrased as the person would say it.
- **Tool labels**: a verb in the present while running ("Searching the docs") and in the past when done ("Searched the docs"); summaries are short ("3 sources", "212 rows", "Timed out").
- **Sources**: a title people recognize, the domain, and an excerpt of one or two sentences.

### Best practices

- Send the sources before the text that cites them, and number citations from 1 in that order.
- Keep tool summaries short; put detail in the panel.
- Let the person stop: always pass the cancellation token on.
- Do not hide failures: an error event gives the person a Retry that runs again in place.

### Common questions

**Why is a sent message not answered?** Without a `Responder` the chat waits for your code to fill the reply.

**Does switching conversations stop a reply?** No. A running reply keeps running, and finished replies are shown at once when you come back.

**Can I show my own tool detail?** Yes: a tool of kind `Custom` is drawn with `ToolDetailTemplate`, with the tool as the data context.

## API reference

<!-- api: AiChat -->

<!-- api: AiChatConversation -->

<!-- api: AiChatTurn -->

<!-- api: AiChatTurnVersion -->

<!-- api: AiChatUserMessage -->

<!-- api: AiChatReply -->

<!-- api: AiChatTool -->

<!-- api: AiSource -->

<!-- api: AiChatSuggestion -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `MessageSent` | The person sends a message, edited ones included | `Conversation`, `Turn`, `Prompt` |
| `ReplyCompleted` | A reply ends: done, stopped or failed | `Reply` |
| `FeedbackChanged` | A reply is rated or the rating is taken back | `Reply`, `Value` |
| `ModelChanged` | The person picks another model | The old and new model |
| `NewChatRequested` | The person asks for a new chat; set `Handled` to do it yourself | |
| `ConversationOpened` | A conversation is opened from the menu | `Conversation` |
| `ConversationCreated` | The first message of an empty chat creates a conversation | `Conversation` |
| `CitationInvoked` | A citation or a source row is pressed; set `Handled` to open it yourself | `Item` |

### The responder

| Type | Description |
|---|---|
| `IAiChatResponder` | `RespondAsync(request, cancellationToken)` returns the reply as a stream of events |
| `AiChatRequest` | The conversation, the selected model and the messages of the shown branch, ending with the prompt to answer |
| `AiChatReasoningDelta` / `AiChatReasoningEnd` | More reasoning text; the end of the reasoning with an optional duration |
| `AiChatToolUpdate` | A tool call started or changed (same `Id` updates the same tool) |
| `AiChatSourcesEvent` | The sources the text will cite |
| `AiChatTextDelta` | More of the reply text, of any size |
| `AiChatErrorEvent` | The reply failed, with the message to show |

## Accessibility

- The chat is a region named "AI chat"; the thread is a list of turns, and each message and reply is named by its author.
- The title button is named "Chats, current: …" and the model button "Model: …"; both report that they open a menu, which is a menu with a checked item.
- A polite live region announces that a reply started ("Orbit Pro 3 is replying"), that it finished, was stopped or failed, copies, ratings, version changes, a new
  model and the conversation opened. Streamed words are not announced one by one.
- The reasoning button reports whether it is expanded; tool chips report their state and cannot be opened while running; the version switcher is a group named "Version 2 of 2".
- Citations take focus in reading order, open their card on focus and close it with Escape without moving focus.
- Action buttons are named and have tooltips; they appear on hover and keyboard focus and take no extra space.
- Focus returns where the person was: to the title button after choosing a conversation, to Edit after sending or cancelling an edit, into the box after "New chat".
- Reduced motion keeps every state and announcement.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Title button, model button, new chat, the controls of the thread in reading order, the message box |
| `Enter` or `Space` on a button | Presses it; on the title or model button, opens the menu |
| `ArrowUp` / `ArrowDown` in a menu | Moves between rows; letters jump to a matching row |
| `Enter` in the message box | Sends (not while a reply is generated or an input method is composing) |
| `Shift+Enter` | Adds a line |
| `Escape` in the message box | Stops the reply being generated |
| `Enter` / `Escape` in the editor of a message | Sends the edit / cancels it |
| `Escape` on a citation | Closes its card without moving focus |

## Motion

- Streamed text is released in whole words on a 34 ms clock (about three words a step), and each step fades in over 150 ms; a finished reply is shown at once.
- Reasoning and tool details open on a spring with their text fading in over 160 ms and out over 100 ms.
- Chips and labels take the width they need on a spring; "Thinking" shimmers until the reasoning ends.
- The thread follows new content at the bottom by closing a fifth of the remaining distance every frame; scrolling up stops it, and a jump button brings it back.
- Switching conversations crossfades the thread; versions fade in at the new height.
- Reduced motion drops springs and movement and keeps the words batched without fading; Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| New words | Whole words every 34 ms, 150 ms fade | Same batches, no fade | Same batches, no fade |
| Reasoning, tool details | Height spring, fades | 120 ms fades | Immediate |
| Following the bottom | Chases the end | Jumps to the end | Jumps to the end |
| Conversation switch | 100 / 160 ms crossfade | 120 ms crossfade | Immediate |
| Shimmer, spinner | Moving | Still | Still |

## Related

- `AiComposer` for a prompt box without a thread.
- `ModelPicker` for choosing a model on its own.
- `AgentMemory` for what the assistant remembers.
- `Markdown` for the text of a reply on its own.

| Component | Choose it when |
|---|---|
| `AiComposer` | You need the box, not the whole conversation |
| `ChatThread` | The chat is between people |
| `ModelPicker` | The model choice lives on a settings page |
| `Markdown` | You only render text |
