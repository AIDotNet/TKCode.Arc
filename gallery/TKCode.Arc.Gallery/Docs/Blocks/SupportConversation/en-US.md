---
title: Support conversation
summary: A support agent's view of one customer conversation: the messages, a composer for replies or internal notes with saved replies, the status, and a panel with the customer's details and the team's notes.
---

<Demo name="Preview" />

## When to use

`SupportConversation` is the agent's side of one customer conversation. The messages run down the middle, the composer
writes a reply the customer sees or an internal note only the team sees (on a warm tint so the two are never confused),
saved replies are one press away, and the status switch says whose turn it is. The panel on the right shows who the
customer is: plan, location, value, tags and the team's notes.

- **A help desk or shared inbox**, opened from a list of conversations.
- **Customer success tools** where context about the account matters as much as the messages.

## When not to use

- **The customer's side of a chat**: use `SupportWidget` or `ChatThread`.
- **A queue of many conversations**: use `InboxTriage` to sort them, then open one here.
- **Team chat**: use `ChatThread` on its own.

## Usage

```xml
<arc:SupportConversation x:Name="Conversation" Subject="Charged twice in March" AgentId="agent" Status="Open" />
```

```csharp
Conversation.ReplySent += (_, e) => api.Deliver(e.Text, e.Mode);
Conversation.Customer = new SupportCustomer { Name = "Mara Lindqvist", Email = "mara@example.com", Plan = "Team" };
Conversation.Participants.Add(new ChatParticipant("cust", "Mara Lindqvist"));
Conversation.Messages.Add(new ChatMessage("m1", "cust", "Hi! I was charged twice.", DateTimeOffset.Now));
Conversation.CannedReplies.Add(new SupportCannedReply("Refund issued", "Hi {name}, the refund is on its way."));
```

A reply joins `Messages` as the agent's message and moves an open conversation to pending; a note joins `Notes`. Both
raise `ReplySent` with the text, the mode and the added item. `{name}` in a saved reply becomes the customer's first
name. Messages render as Markdown. A conversation with no messages when it first loads shows a sample.

### Narrow

<Demo name="Narrow" />

`Send()`, `InsertCannedReply(reply)` and `OpenCannedReplies()` do from code what the composer does.

## API reference

<!-- api: SupportConversation -->

<!-- api: SupportCustomer -->

<!-- api: SupportNote -->

<!-- api: SupportCannedReply -->

<!-- api: SupportConversationStatus -->

<!-- api: SupportComposerMode -->

<!-- api: SupportReplyEventArgs -->

<!-- api: SupportStatusChangedEventArgs -->

## Accessibility

- The customer's name is the heading; the status and the composer mode are segmented controls with names.
- The composer's name follows its mode ("Reply" or "Internal note"), so a screen reader user knows who will see the text. Sending and status changes are announced.
- Notes are named "Note by …" with their text; the facts are label and value pairs.

### Keyboard

| Keys | Action |
|---|---|
| `Ctrl+Enter` in the composer | Send the reply or add the note. |
| `/` in an empty composer | Open the saved replies. |
| `↑` / `↓`, `Enter`, `Escape` in saved replies | Choose one or close the list. |

## Motion

- New messages arrive in the thread with its own motion (they rise in, receipts glide to the latest read message).
- The composer's tint and the segmented controls change with short fades; the saved replies open in a popover that scales in.
- Reduced motion keeps fades only; with motion off changes are immediate.

## Responsive behavior

- The block is 620 px tall unless its container gives it a height (`DefaultHeight`).
- At 820 px and wider the customer panel sits on the right (300 px, `SidePanelWidth`). Below that (`:narrow`) it moves behind the panel button in the header and covers the messages when shown.

## Related

- `ChatThread` for the message list on its own.
- `InboxTriage` for the queue of conversations.
- `SupportWidget` for the customer's side.
