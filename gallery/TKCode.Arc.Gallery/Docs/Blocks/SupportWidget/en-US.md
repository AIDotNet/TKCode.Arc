---
title: Support widget
summary: The floating help widget a customer opens from the corner of a site: a greeting, article search and recent articles, a chat with the team that shows typing and replies, and the history of earlier conversations.
---

<Demo name="Preview" />

## When to use

`SupportWidget` is the help bubble in the corner of a site or app. Pressed, it opens a panel with a greeting, the faces
of who will answer and how fast, a search over help articles and the most recent ones, a "Send us a message" button that
starts a chat, and the history of earlier conversations. A chat shows the team typing and their reply.

- **A marketing site or web app** where visitors need help without leaving the page.
- **An in-app help entry point** that first offers articles, then a person.

## When not to use

- **A full help site**: use `HelpCenter`.
- **The agent's side**: use `SupportConversation`.
- **Announcements or tips**: use `AnnouncementBar` or `Toast`.

## Usage

```xml
<Panel>
    <!-- the page -->
    <arc:SupportWidget x:Name="Widget" TeamName="Lumen support" SimulatesReplies="False" />
</Panel>
```

```csharp
Widget.MessageSent += (_, e) => api.Send(e.Conversation.Id, e.Message.Text);

// When the team answers:
conversation.Messages.Add(new ChatMessage("r1", "kofi", "Happy to help!", DateTimeOffset.Now));
```

The widget fills the space it is given and keeps itself in the bottom-right corner, so place it over the page. `Agents`
are the people who answer, `Articles` feed the search and the recent list, and `Conversations` is the history, newest first.
With `SimulatesReplies` (the default) the first agent types and answers on their own, which is right for a preview and
wrong for production. A widget with no articles when it first loads shows a sample.

### Closed

<Demo name="Closed" />

`IsOpen` is two-way; `Open()`, `Close()`, `StartConversation()`, `OpenChat(conversation)`, `OpenArticle(article)`,
`GoBack()` and `SendMessage(text)` drive it from code. `Screen` says what the panel shows.

## API reference

<!-- api: SupportWidget -->

<!-- api: SupportWidgetScreen -->

<!-- api: SupportWidgetArticle -->

<!-- api: SupportWidgetConversation -->

<!-- api: SupportWidgetMessageEventArgs -->

## Accessibility

- The launcher is named "Open help" or "Close help"; the panel is a group named "Help" and its screens have headings.
- Opening moves focus to the search field; closing returns it to the launcher. Replies from the team are announced.
- Rows in the lists are buttons named by their titles; unread conversations say so.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` / `Space` on the launcher | Open or close the panel. |
| `Escape` | From a chat or an article, go back; otherwise close the panel. In the search field, clear it first. |
| `Enter` in the chat composer | Send the message. |

## Motion

- The panel scales in from the launcher's corner and out again; the launcher's icon pops between a bubble and a chevron.
- Screens slide sideways, forward into a chat or an article and back out; the typing indicator and new messages come from `ChatThread`.
- Reduced motion uses fades only; with motion off changes are immediate.

## Responsive behavior

- The panel is 380 × 560 px (`PanelWidth`, `PanelHeight`) and sits 20 px from the corner (`CornerMargin`).
- On a phone-sized page give the widget the full screen and set a smaller `PanelWidth` through the theme, or open `HelpCenter` instead.

## Related

- `HelpCenter` for the full help site.
- `ChatThread` for the chat on its own.
- `SupportConversation` for the agent's view.
