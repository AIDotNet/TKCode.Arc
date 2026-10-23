---
title: Chat thread
summary: A chat thread with grouped messages, reactions, read receipts, typing, and a composer with attachments.
---

<Demo name="SupportConversation" />

## When to use

`ChatThread` is the conversation surface of an application where people talk in turns: a support widget on a billing or
order page, a direct message panel in a CRM, or a team room in an internal tool. It owns the message list, the day
separators, the scroll behavior and the composer. The page around it supplies the messages and handles what the person
sends.

- **Customer support on a billing or order page**: an invoice question answered by a support agent. Messages group by
  person and time, and the customer's messages sit on the right with a delivery state.
- **Account conversations in a CRM**: a sales owner and a customer exchange messages about a renewal. Read receipts show
  which messages the other side has seen.
- **Team rooms in an internal tool**: an incident room where engineers post text, pictures and log files. Attachments show
  as a picture gallery or a file row that opens the file.
- **Assistant replies that arrive in pieces**: a bot streams its answer into a bubble. The thread appends each chunk to the
  Markdown body without re-parsing the reply from its start.
- **Messages that can fail**: a message in a checkout or support flow that the server does not accept. It dims, shows
  "Not delivered" and offers Retry on your last message.

## When not to use

- **A threaded discussion about one object**, such as a review comment on a contract clause: use `CommentThread`, where
  replies nest under a comment and can be resolved.
- **A history of events across many objects**, such as deployments or audit entries: use `Timeline`, which has no
  composer and no read receipts.
- **Only a multi-line field with no conversation**, such as a note on a record: use `Textarea`. For @mentions in a single
  field, use `MentionInput`.
- **A composer that sits beside your own message list**: use `ChatComposer` on its own. It has the same text box,
  attachment tray and send behavior without the list.
- **History that must be paged from a server by date**: the thread displays the messages it is given and does not load
  history. Load the page in your view model and add it to the collection.

## Usage

`Participants` lists the people in the conversation, and `CurrentUserId` marks which of them is you. Your messages sit on
the right in the accent color, and other people's messages sit on the left with their names. Messages are ordered from
oldest to newest, and the list stays pinned to the newest one.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ChatThread Label="Support conversation"
                    Participants="{Binding Participants}"
                    CurrentUserId="me"
                    ItemsSource="{Binding Messages}"
                    Height="560" />
</UserControl>
```

```csharp
var me = new ChatParticipant("me", "Alex Kim");
var sam = new ChatParticipant("sam", "Sam Rivera");

var messages = new ObservableCollection<ChatMessage>
{
    new("m1", "sam", "Hi Alex! Thanks for reaching out.", DateTimeOffset.Now)
};

var thread = new ChatThread
{
    Participants = [me, sam],
    CurrentUserId = me.Id,
    ItemsSource = messages,
    Label = "Support conversation",
    Height = 560
};
```

`ChatThread` derives from Avalonia's `ItemsControl`, so `ItemsSource` and `Items` take the collections a list does. The control builds its own rows, so you supply data, not item templates. The thread needs a bounded height from its parent.

Without an `ItemsSource`, the thread keeps its own list: messages sent from the composer are appended, and a reaction
toggles in place. Bind `ItemsSource` to your own collection when the messages come from a server.

### Anatomy

1. **Message list** (`Label` names it): the scrolling region. It follows the newest message while the reader is within
   48 px of the end.
2. **Day separator**: a 12 px label ("Yesterday", "Today" or a date) between two 1 px lines in the subtle border color.
3. **Author portrait**: 28 px, in the gutter beside other people's messages. Initials show when there is no `Avatar`.
4. **Meta line**: the author's name in 12 px medium weight and the time in 12 px muted text.
5. **Bubble**: 18 px corners, padded 8 px 13 px 9 px. Other people's bubbles use the muted surface (raised in dark mode);
   your bubbles use the accent color. A bubble takes up to 78% of the row (84% below 420 px wide) and at most 544 px.
6. **Attachments**: pictures form a gallery 240 px wide with 3 px gaps, showing up to four tiles and a "+N" scrim on the
   fourth. Other files show as a row with the name and the size.
7. **Reactions row**: one chip per emoji with its count, and a 28 px add-reaction button that opens the picker.
8. **Receipts line**: the status text and the readers' portraits, 16 px each, overlapping by 4 px.
9. **Typing row**: a 36 px bubble with animated dots, shown while `TypingParticipantIds` is not empty.
10. **Composer** (`IsComposerVisible`): a text box that grows up to six lines, the attach button and the send button.
11. **Jump pill**: 32 px high, 12 px above the bottom edge. It reads "3 new messages" when messages arrive out of view and
    "Jump to latest" when the reader is far up with nothing new.



### Binding to a view model

The view model owns the messages, their status and the receipts, and the thread displays them. This example uses CommunityToolkit.Mvvm.

```csharp
public interface ISupportApi
{
    Task<SendResult> SendMessageAsync(string text, int fileCount);
}

public sealed record SendResult(bool IsSuccess);

public partial class SupportChatViewModel : ObservableObject
{
    private readonly ISupportApi _api;

    public SupportChatViewModel(ISupportApi api)
    {
        _api = api;
        Participants =
        [
            new ChatParticipant("me", "Alex Kim"),
            new ChatParticipant("sam", "Sam Rivera")
        ];
    }

    public IReadOnlyList<ChatParticipant> Participants { get; }

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    private IReadOnlyDictionary<string, string> _readBy = new Dictionary<string, string>();

    [ObservableProperty]
    private IReadOnlyList<string> _typingParticipantIds = [];

    public async Task SendAsync(ChatDraft draft)
    {
        var message = new ChatMessage(Guid.NewGuid().ToString("N"), "me", draft.Text, DateTimeOffset.Now)
        {
            Attachments = draft.Attachments.Count > 0 ? draft.Attachments : null,
            Status = ChatMessageStatus.Sending
        };
        Messages.Add(message);

        var result = await _api.SendMessageAsync(draft.Text, draft.Files.Count);
        message.Status = result.IsSuccess ? ChatMessageStatus.Sent : ChatMessageStatus.Failed;
    }

    [RelayCommand]
    private async Task RetryAsync(ChatMessage message)
    {
        message.Status = ChatMessageStatus.Sending;
        var result = await _api.SendMessageAsync(message.Text ?? string.Empty, 0);
        message.Status = result.IsSuccess ? ChatMessageStatus.Sent : ChatMessageStatus.Failed;
    }
}
```

```xml
<arc:ChatThread Label="Support conversation"
                Participants="{Binding Participants}"
                CurrentUserId="me"
                ItemsSource="{Binding Messages}"
                ReadBy="{Binding ReadBy}"
                TypingParticipantIds="{Binding TypingParticipantIds}"
                RetryCommand="{Binding RetryCommand}" />
```

- The inputs (`Participants`, `ItemsSource`, `ReadBy`, `TypingParticipantIds`, `RetryCommand`) are one-way: the thread never writes them back. `IsPinned` and `UnreadCount` are read-only outputs.
- `SendRequested` is an event, not a command. The code-behind subscribes to it and passes the draft to `SendAsync`. The
  view model adds the message itself, so the thread does not append it a second time.
- Changing `Status` or `Text` on a message updates its bubble in place. Replace the collection only to add or remove messages.

### Grouping, time and culture

Messages from one person that are closer together than `GroupWindow` share a group, so only the first shows the name. The
default window is 5 minutes. A message from another person, or after a longer gap, starts a new group. 

`Culture` formats the times and day separators (default en-US), and `TimeZone` sets the zone they show in (default is the
local zone of the machine). Set both for the product's market.

```csharp
thread.Culture = CultureInfo.GetCultureInfo("en-GB");
thread.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
thread.GroupWindow = TimeSpan.FromMinutes(10);
```

### Sending and attachments

`SendRequested` runs when the person presses Enter in the composer or the send button. The `ChatDraft` carries the trimmed
`Text`, the chosen `Files` (local `IStorageFile` items) and the matching `Attachments`. A draft with no text and no files
does not send. Without an `ItemsSource` the thread appends the message itself. With one, the handler adds it and decides
its status, as the view model above does.

`IsAttachmentEnabled` (default true) shows the attach button and takes pasted and dropped files. `AcceptedFileTypes` limits the picker, and the tray takes at most 10 files per add. An `Image` attachment with a `Url` or `File` joins the picture gallery; other attachments show as a file row with their size.

`AttachmentClicked` runs when a picture or file is clicked. The thread opens the `Url` or local file unless the handler
marks the event as handled, so mark it handled when your app shows its own viewer.

### Status, read receipts and typing

`ChatMessage.Status` sets the text under your own last message. The states are:

| Status | Text shown | Set it when |
|---|---|---|
| `Sending` | "Sending" | The message is in flight and has not been accepted |
| `Sent` | "Sent" | The server accepted it (the default for your own messages) |
| `Delivered` | "Delivered" | The message reached the other side |
| `Read` | "Read" | The message was read; a receipt on or after it also shows this |
| `Failed` | "Not delivered" | The send failed. The bubble dims to 60% opacity |

`ReadBy` maps each participant id to the last message that person read; their portrait glides to a newer one when the map changes. Assign a new dictionary each time. `TypingParticipantIds` lists who is typing. Retry appears only when you subscribe to `RetryRequested` or set `RetryCommand`, and only on your last message. The command receives the failed `ChatMessage`.



### Reactions and the picker

The add-reaction button opens a picker of the emoji in `Reactions`. The default set is the six emoji of the Arc picker, and
`DefaultReactions` returns the same list in code. Picking an emoji, or clicking a chip, raises `ReactionToggled`. Without an
`ItemsSource` the thread toggles the reaction itself. With one, the application sets the message's `Reactions`:

```csharp
thread.ReactionToggled += (_, e) =>
{
    var reactions = new List<ChatReaction>(e.Message.Reactions ?? []);
    var at = reactions.FindIndex(r => r.Emoji == e.Emoji);
    if (at < 0)
    {
        reactions.Add(new ChatReaction(e.Emoji, 1, true));
    }
    else
    {
        var reaction = reactions[at];
        reactions[at] = reaction with { Count = reaction.Count + (reaction.Mine ? -1 : 1), Mine = !reaction.Mine };
    }
    e.Message.Reactions = reactions.Where(r => r.Count > 0).ToArray();
};
```



### Streaming a reply

A reply that arrives in pieces is added with no text, filled by `AppendToMessage` as chunks arrive and closed by
`CompleteMessage`. Each call returns `false` when no message has the id, so a late chunk for a removed message does nothing.
`IsMessageStreaming` reports whether a reply is open.

<Demo name="StreamingMarkdownReply" />

```csharp
messages.Add(new ChatMessage("r1", "arc", null, DateTimeOffset.Now));
thread.AppendToMessage("r1", "Here is a small helper that retries a request.");
thread.CompleteMessage("r1");
```

The Markdown body receives only the new text, so a long reply is not parsed again from its start on each chunk.

### Data shape and performance

Each `ChatMessage` raises `PropertyChanged`, so updating its properties updates its bubble. Give every message a stable `Id`, because `ReadBy`, streaming and the reaction callbacks find messages by it.

Rows are virtualized. A list of a thousand messages realizes fewer than 60 containers, and the thread keeps at most 64
rendered message bodies, so the Markdown cache does not grow with the history.

<Demo name="ThousandMessages" />

Messages that land while the reader is scrolled up are counted in `UnreadCount` and shown by the jump pill. Older messages added at the top are not anchored for you in this release, so test that path before you ship it.

### Composer on its own

`ChatComposer` is the thread's input, and it is public so it can stand alone. It grows to six lines and sends with Enter. `Text` is two-way. `Placeholder` (default
"Message") is also the text box's accessible name. `Sent` carries the `ChatDraft`. `Send()` sends from code, `FocusInput()`
moves focus to the text box and `BrowseAsync()` opens the file picker.

<Demo name="ComposerOnItsOwn" />

### Example: support conversation on a billing page

A support panel on the billing page of a SaaS product. The panel has a fixed height, so the conversation scrolls inside it
and the composer stays visible. The agent's reply carries the statement as a file row with its size. The receipt under the
customer's last message shows the agent has read it, and the typing row shows the agent is still writing. The placeholder
names the agent, which is clearer than the default "Message".

![A support conversation with a file attachment, a read receipt and a typing indicator](images/SupportConversation.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic"
             Width="600">
    <Border Height="600"
            CornerRadius="16"
            BorderThickness="1"
            ClipToBounds="True"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}">
        <arc:ChatThread Label="Support conversation"
                        CurrentUserId="me"
                        Placeholder="Reply to Sam">
            <arc:ChatThread.Participants>
                <generic:List x:TypeArguments="arc:ChatParticipant">
                    <arc:ChatParticipant Id="me" Name="Alex Kim" />
                    <arc:ChatParticipant Id="sam" Name="Sam Rivera" />
                </generic:List>
            </arc:ChatThread.Participants>
            <arc:ChatThread.ReadBy>
                <generic:Dictionary x:TypeArguments="x:String, x:String">
                    <x:String x:Key="sam">m4</x:String>
                </generic:Dictionary>
            </arc:ChatThread.ReadBy>
            <arc:ChatThread.TypingParticipantIds>
                <generic:List x:TypeArguments="x:String">
                    <x:String>sam</x:String>
                </generic:List>
            </arc:ChatThread.TypingParticipantIds>
            <arc:ChatMessage Id="m1" AuthorId="sam"
                             Text="Hi Alex, I can see the duplicate charge on invoice INV-2026-0412."
                             CreatedAt="2026-10-20T09:02:00+00:00" />
            <arc:ChatMessage Id="m2" AuthorId="me"
                             Text="Yes, the card was charged twice. Can you refund one of them?"
                             CreatedAt="2026-10-20T09:04:00+00:00"
                             Status="Read" />
            <arc:ChatMessage Id="m3" AuthorId="sam"
                             Text="Refund started. The statement copy is attached."
                             CreatedAt="2026-10-20T09:06:00+00:00">
                <arc:ChatMessage.Attachments>
                    <generic:List x:TypeArguments="arc:ChatAttachment">
                        <arc:ChatAttachment Id="f1" Name="statement-2026-09.pdf"
                                            Kind="File" Size="182400" />
                    </generic:List>
                </arc:ChatMessage.Attachments>
            </arc:ChatMessage>
            <arc:ChatMessage Id="m4" AuthorId="me"
                             Text="Thank you. When will it reach my account?"
                             CreatedAt="2026-10-20T09:08:00+00:00"
                             Status="Read" />
        </arc:ChatThread>
    </Border>
</UserControl>
```

The scene keeps its messages inline, as a static picture must. In the application they come from `ItemsSource`.

### Content guidelines

- **Label**: a short noun phrase that names the conversation ("Support conversation", "Project Atlas"). It is the list's
  accessible name, so describe the thread, not the product.
- **Placeholder**: names the recipient or the purpose ("Reply to Sam"). The default "Message" reads like a form label in a
  support widget.

- **Errors**: use the status words the thread shows and the Retry action. Do not add a second error text under the bubble.
- **Other languages**: set `Labels`; plural rules and names belong in its functions.

| Instead of | Write |
|---|---|
| Message | Reply to Sam |
| Conversation | Support conversation |
| Not delivered. Please try again later. | Not delivered. Retry. |
| Sam is typing... | Sam is typing |
| 1 new messages | 1 new message |

### Best practices

**Do**

- Keep the thread in a container with a fixed height, so the list has room to scroll and the composer stays at the bottom.
- Set `Status` to `Sending` when the request starts and to `Sent` or `Failed` when the server answers.
- Update the existing `ChatMessage` when it changes, so its bubble updates in place.
- Set `Culture` and `TimeZone` for the product's market, so times match the agent's clock.

**Avoid**

- Replacing the whole `ItemsSource` to change one message. Replacing the collection rebuilds every row.
- Leaving "Sam is typing" on screen after the reply arrived. Clear `TypingParticipantIds` when the event ends, or the reader
  waits for a message that is not coming.

- Expecting Retry on an earlier failed message. Retry shows only on your last message.
- Keeping the default `Reactions` for a product with its own vocabulary. Set the emoji the team actually uses.

### Common questions

**The list no longer follows new messages. Why?** The reader is more than 48 px from the end. New messages are counted in `UnreadCount`; the jump pill or `ScrollToBottom()` pins the list again.

**Why does Retry not appear on my failed message?** Retry needs a `RetryRequested` handler or a `RetryCommand`, and it
appears only on your last message. Resend the message or show the error in your own UI.

**Why do my messages show as plain text?** `MessageFormat` defaults to `Markdown`. Set it to `PlainText` on the thread, or set `Format` on one message.



## API reference

<!-- api: ChatThread -->

<!-- api: ChatMessage -->

## Accessibility

- The message list is a named list (`Label`, default "Conversation") with a polite live setting, so each new message is read without moving focus.
- Typing is announced in its own status region ("Sam is typing", "Sam and Lee are typing"). The dots are not announced.
- Reaction chips are toggle buttons. Read receipts name the people who read a message. The portraits are decorative.
- The attach, send and add-reaction buttons take their names from `Labels` ("Attach files", "Send", "Add reaction"). The
  text box is named by `Placeholder`, and the tray by `Labels.Attachments`.
- When the picker opens, its first emoji takes focus. Escape closes it and returns focus to the add-reaction button.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` (in the composer) | Sends the draft when it has text or files; ignored while an input method is composing |
| `Shift` + `Enter` (in the composer) | Adds a new line |
| `Tab` / `Shift+Tab` | Moves through the composer, the attachments, and each message's add-reaction and Retry buttons |
| `Enter` or `Space` (on a button) | Opens the picker, picks an emoji, toggles a chip or presses Retry |
| `Left` / `Right` (in the picker) | Moves between emoji and wraps at both ends |
| `Escape` (in the picker) | Closes the picker and returns focus to the add-reaction button |

The add-reaction button is visible while the pointer is over a message or a control in it has focus, so `Tab` reaches it.

## Motion

`ScrollToBottom()` glides to the end unless you pass `smooth: false`. The table shows each change:

| Change | Full | Reduced | Off |
|---|---|---|---|
| A message arrives | Rises in from its side with a small scale; the list moves up | Short fade | Immediate |
| Content grows while pinned | The list follows the newest message on a spring | Immediate | Immediate |
| Read receipt moves | The portrait glides to the newer message | Short fade | Immediate |
| Reaction added or removed | The chip pops in and the count rolls | Short fade | Immediate |

| Typing indicator | The dots loop while visible | Dots stop | Dots stop |
| Jump pill appears | Rises from below | Short fade | Immediate |



## Related

- `ChatComposer` for a composer with attachments on its own.
- `CommentThread` for threaded replies on one object, with resolve and edit.
- `Timeline` for a history of events with no composer.

| Component | Choose it when |
|---|---|
| `ChatComposer` | You need the text box and attachment tray without the message list |
| `CommentThread` | Replies nest under one object, such as a review comment, and can be resolved |
| `Timeline` | The history is a list of events across many objects, not a conversation |
| `Textarea` | A note or description is written in several lines, with no replies |
| `MentionInput` | A field completes @mentions while people type a single message |

