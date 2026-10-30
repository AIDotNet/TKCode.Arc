---
title: Agent handoff
summary: A support chat where an AI assistant hands off to a person: a card shows the summary it passes to the team and the typical wait, then the person joins, with a thin timeline marking each step.
---

<Demo name="Preview" />

## When to use

`AgentHandoff` is a support conversation where an AI assistant answers first and hands off to a person. When it
transfers, a card in the thread shows the summary it passes to the team and the typical wait. The person then joins and
takes over, and a thin timeline on the left marks each step: the assistant, the handoff, the person joining and the
resolution.

- **A support widget** where the AI answers first and escalates to a human agent.
- **Showing customers what the assistant shared** with the team, so they do not repeat themselves.
- **Marketing or docs pages** that explain an AI to human handoff flow.

## When not to use

- **A general chat with one assistant** and no handoff: use `ChatThread`.
- **An agent's internal steps or tool calls**: use `AgentTimeline` or `ToolCall`.
- **A full help desk inbox for agents**: build it from list and detail layouts instead.

## Usage

With no `Stage` and no simulation the card is an empty chat: the header, the assistant's step and nothing else. Wire
it to your support backend by setting `Stage` and the data, and answering the events:

```csharp
chat.Title = "Order NW-48213";
chat.Subtitle = "Northwind Home support";
chat.Summary = new AgentHandoffSummary(
    "Desk arrived with a cracked leg. Customer wants a full refund.",
    new AgentHandoffField("Order", "NW-48213"),
    new AgentHandoffField("Asks for", "Full refund"));
chat.Agent = new AgentHandoffAgent("Jordan Reyes", "Support specialist", "14:05");
chat.ShowsComposer = true;
chat.AllowsRequestHuman = true;
chat.SendRequested += (_, e) => backend.Send(e.Text);
chat.HumanRequested += (_, _) => backend.Escalate();

// Then follow the backend.
chat.Stage = conversation.Stage;
```

Keep the assistant's messages in `Messages` and everything after the handoff card in `AgentMessages`: notes the customer
adds while waiting and the conversation with the person. The join step is placed before the person's first message.

### Stages

<Demo name="YourBackend" />

Drive `Stage` from your backend: `Transferring` when you create the ticket, `Connected` when a person accepts,
`Resolved` when they close it and `Unavailable` when the queue times out.

| Stage | Header status | Handoff card |
|---|---|---|
| `Assistant` | A sparkle and "AI assistant" | Not shown; Talk to a person can sit under the last answer |
| `Transferring` | A spinner and "Transferring" | "Transferring you to a person", the typical wait (`ReplyEstimate`), Cancel |
| `Connected` | A person and "With Jordan" | "Jordan joined from Help desk", the person, the summary |
| `Resolved` | A check and "Resolved" | "Resolved by Jordan"; a last step closes the timeline |
| `Unavailable` | An alert and "No one available" | "No one is available right now", Try again |

### Actions and the composer

Each affordance shows when you turn it on, and raises a bubbling routed event:

- `AllowsRequestHuman`: a Talk to a person reply under the assistant's last finished answer, raising `HumanRequested`.
- `AllowsCancelTransfer`: Cancel on the card while transferring, raising `CancelTransferRequested`.
- `AllowsRetry`: Try again on the card when nobody is available, raising `RetryRequested`.
- `ShowsComposer`: the composer. Enter or the send button raises `SendRequested` with the trimmed text; append the
  message to `Messages` or `AgentMessages` yourself. The placeholder follows the stage ("Message Jordan") unless you set
  `Placeholder`.

### Streaming and typing

Stream assistant text by updating the message's `Text` with `IsStreaming` set; the card fades each appended chunk in.
Set `Typing` to `Assistant` for a short shimmering Thinking line, or to `Agent` for typing dots.

```csharp
var reply = new AgentHandoffMessage(AgentHandoffSender.Assistant, string.Empty) { IsStreaming = true };
chat.Messages.Add(reply);
await foreach (var chunk in stream)
{
    reply.Text += chunk;
}
reply.IsStreaming = false;
```

### Narrow containers

<Demo name="Narrow" />

The card fills its container up to 560px and holds up at 360px with tighter padding and wider bubbles. It has a fixed
height (600 by default; set `Height`) and the thread scrolls inside, pinned to the newest message, so the page never
shifts as the thread grows. Long names and meta lines truncate with an ellipsis; message text wraps.

### Marks and avatars

`AssistantMark` and `ProviderMark` take a 24 × 24 stroked glyph (a Lucide icon or your own path) for the assistant on
the first step and the help desk next to the handoff card; `AssistantMarkBrush` and `ProviderMarkBrush` color them. The
person's avatar shows their initials, or the photo in `AgentHandoffAgent.Avatar`.

### The simulation

`AgentHandoffSimulation` plays a scripted chat for demos and prototypes, as in the preview above. Give it the words in
an `AgentHandoffScript` (a line left null is skipped) and set it as the card's `Simulation`: the card plays it while
`Stage` is unset, from the top each time it is shown, with the composer and the Talk to a person, Cancel and Try again
actions working. `Scenario="Unavailable"` stops when nobody picks the transfer up, so Try again shows; changing the
scenario starts the story over. It exposes `Stage`, `Messages`, `AgentMessages`, `Typing`, `Summary`, `Agent` and
`Notice`, and takes `RequestHuman`, `CancelTransfer`, `Retry` and `Send`. Setting `Stage` hands the card back to your
data. Do not use it outside demos.

```xml
<arc:AgentHandoff Title="Order 1042" AssistantModel="Your model">
    <arc:AgentHandoff.Simulation>
        <arc:AgentHandoffSimulation Scenario="Unavailable" />
    </arc:AgentHandoff.Simulation>
</arc:AgentHandoff>
```

```csharp
chat.Simulation = new AgentHandoffSimulation
{
    Script = new AgentHandoffScript
    {
        CustomerOpening = "My order arrived damaged.",
        AssistantAnswer = "Sorry about that. I can send a replacement part.",
        CustomerRequest = "Can I talk to a person?",
        AssistantHandoff = "Of course, I'll pass this to our team with a summary.",
        Summary = new AgentHandoffSummary("Order arrived damaged; wants a refund."),
        Agent = new AgentHandoffAgent("Sam Lee", "Support specialist"),
        AgentGreeting = "Hi, I'm Sam. I've read the summary.",
    },
};
```

### Your own words

`Labels` holds every word the card shows or says. Replace the object to localize or reword:

```csharp
chat.Labels = new AgentHandoffLabels
{
    Transferring = "Übergabe",
    TalkToPerson = "Mit einer Person sprechen",
    With = name => "Mit " + name,
};
```

### Best practices

- Pass `Summary` with the same facts you send to the help desk, so the customer sees what was shared.
- For very long threads pass only the recent messages; the card is meant for a live conversation, not an archive.
- Use `Notice` for a one-off result such as "Refund issued"; it is announced once.

## API reference

<!-- api: AgentHandoff -->

<!-- api: AgentHandoffMessage -->

<!-- api: AgentHandoffLabels -->

<!-- api: AgentHandoffSimulation -->

<!-- api: AgentHandoffScript -->

## Accessibility

- The thread is a focusable log named "Conversation", so keyboard users can scroll it; it does not announce every
  streamed chunk.
- Stage changes and `Notice` are announced once through a polite status region, including that the demo is simulated.
- Every status pairs an icon with a label: Transferring, With Jordan, Resolved, No one available. Color is never the
  only signal.
- The summary toggle reports expanded or collapsed. The typing dots are named, such as "Jordan is typing".
- Marks and avatars on the timeline are decorative next to their visible labels.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the thread, Talk to a person, the card's action, the summary toggle and the composer |
| Up, Down, Page Up, Page Down, Home, End | Scroll the focused thread |
| Enter | Sends the composer message, or presses the focused button |
| Space | Toggles the summary when the summary button has focus |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Assistant reply streams | Chunks of 3 to 8 words about every 34ms; each chunk only fades in over 140ms | The reply appears whole | The reply appears whole |
| A row arrives or a line wraps | The thread follows its measured height on a bounce-free 0.24s spring, pushing up without a snap | At once | At once |
| Transferring | A small accent dot travels down the timeline beside the handoff card | No travel dot | No travel dot |
| Status labels change | Old label fades out over 100ms, new label fades in over 160ms and rises 2px | Short crossfade | At once |
| Typing dots, spinner | Breathe and turn | Hold still | Hold still |
| The simulated chat | Plays and starts over | Plays once | Plays once |

Loops pause off screen.

## Related

- `ChatThread` for a chat with one assistant or between people.
- `AgentTimeline` for the steps of a multi-agent run.
- `ToolCall` for the tool calls an agent makes.
