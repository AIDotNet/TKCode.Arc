---
title: Chat thread
summary: A chat thread with grouped messages, reactions, read receipts, typing, and a composer with attachments.
---

## When to use

- A support widget, a direct message or a team chat between people who are online together.
- Messages that group by person and time, with day separators and read receipts.
- A composer that sends text and files to the conversation.

## When not to use

- Threaded review on one object, where replies nest under a comment: use `CommentThread`.
- A history of events across many objects: use `Timeline`.
- Only a multi-line input with no conversation: use `Textarea`, or `ChatComposer` for a composer with attachments.

## Usage

`Participants` lists the people in the conversation, and `CurrentUserId` marks which of them is you. Your messages sit
on the right in the accent color. Messages are ordered from oldest to newest.

```xml
<arc:ChatThread Label="Support conversation"
                Participants="{Binding Participants}"
                CurrentUserId="me"
                ItemsSource="{Binding Messages}"
                Height="560" />
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

Without an `ItemsSource`, the thread keeps its own list, and messages sent from the composer are appended to it. Bind
`ItemsSource` to your own collection to store the messages yourself.

### Sending and attachments

`SendRequested` gives you a `ChatDraft` with the trimmed text, the files and their attachments. Without an
`ItemsSource`, the thread also appends the message. With one, add it yourself. `IsAttachmentEnabled` and
`AcceptedFileTypes` control the attach button.

```csharp
thread.SendRequested += (_, e) =>
{
    messages.Add(new ChatMessage($"s-{messages.Count + 1}", me.Id, e.Draft.Text, DateTimeOffset.Now)
    {
        Attachments = e.Draft.Attachments.Count > 0 ? e.Draft.Attachments : null,
        Status = ChatMessageStatus.Sent
    });
};
```

### Status, read receipts and typing

`Failed` on your own message shows a Retry action, and `RetryRequested` runs when it is pressed. `ReadBy` maps each
participant to the last message they read, and `TypingParticipantIds` lists who is typing.

```csharp
thread.ReadBy = new Dictionary<string, string> { ["sam"] = "m6" };
thread.TypingParticipantIds = ["sam"];
thread.RetryRequested += (_, e) => e.Message.Status = ChatMessageStatus.Sent;
```

### Reactions and streaming

`ReactionToggled` reports the reaction that the reader picked. Use `AppendToMessage` and `CompleteMessage` to stream a
reply: the Markdown body receives only the new text.

```csharp
thread.ReactionToggled += (_, e) => ToggleReaction(e.Message, e.Emoji);

messages.Add(new ChatMessage("r1", "arc", null, DateTimeOffset.Now));
thread.AppendToMessage("r1", "Here is a small helper that retries a request.");
thread.CompleteMessage("r1");
thread.ScrollToBottom();
```

## API reference

<!-- api: ChatThread -->

<!-- api: ChatMessage -->

## Accessibility

- The message list is a named list that announces only new messages. Typing is announced separately, such as
  "Sam is typing".
- Reaction chips are toggle buttons. Read receipts name the people who have read a message. The avatars are decorative.
- Enter sends and Shift + Enter adds a line. Tab reaches each message's add-reaction button.
- In the reaction picker, Left and Right move between emoji and wrap at the ends. Escape closes it and returns focus to
  the button.

## Motion

- New messages rise in from their side with a small scale. The list moves up by the height that was added.
- A reader's avatar slides to the newest message they have read.
- Reaction chips pop in, and their counts roll. The reaction row and the attachment tray open on a spring.
- The typing dots loop only while they are visible. A jump-to-latest pill appears when new messages arrive while you are
  scrolled up.
- Reduced motion uses short fades, stops the typing dots and scrolls to the bottom at once. Off jumps to the final state.

## Related

- `ChatComposer` for a composer with attachments on its own.
- `CommentThread` for threaded replies on one object.
- `Timeline` for a history of events.
