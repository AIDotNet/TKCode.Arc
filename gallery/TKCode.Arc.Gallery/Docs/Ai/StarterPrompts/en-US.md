---
title: Starter prompts
summary: A greeting and starter cards for the empty state of an AI app, with category tabs, a shuffle that crossfades the cards, and a composer that a picked card fills.
---

<Demo name="Preview" />

## When to use

- The empty state of a chat, copilot or agent, before the first message.
- Teaching what an assistant can do with connected tools, such as mail, a code repository or a docs space.
- Onboarding a new workspace with a few tailored, ready to send prompts.

## When not to use

- Inside a running conversation; use follow-up suggestion chips in the thread instead.
- A command launcher with search; use a command palette.
- A single inline suggestion in a text field; use ghost text.

## Usage

### The empty state

With no data the control shows the All tab and the composer, with no cards and no greeting. Give it a greeting, a quiet
line, tabs and starters (the preview's 24 are invented, over Write, Code, Analyze and Plan) and it shows four at a time.
Picking a card places its full `Prompt` in the composer, focuses it and raises
`PromptSelected`; Enter sends and raises `SubmitRequested`. Shuffle shows the next starters of the tab with a short
staggered crossfade, and always brings at least one card that was not on screen.

```xml
<arc:StarterPrompts Greeting="Good morning, Maya" ModelName="Qwen3.8-Max" />
```

The card's title is what people scan; the prompt is what the composer shows. Keep titles short and verb first, and put
the full, specific instruction in `Prompt`.

### Your own starters and tabs

<Demo name="OwnStarters" />

Add `StarterPromptCategory` items to `Categories` (an id, a label and an optional 24 × 24 stroked `Icon`) and
`StarterPrompt` items as the content (`Id`, `Category`, `Title`, `Description`, `Prompt`, `App`). All is added in front
and a tab with no starters is hidden. `Count` sets how many cards are on screen; the grid keeps that many slots, so a
tab with fewer starters leaves its empty slots in place and nothing below moves.

`App` marks a card that uses a connected app: its mark is drawn in a tinted square and its name reads beside it and in
the card's accessible name. The presets (`Mail`, `Repository`, `Docs`, `Issues`, `Calendar`) are neutral glyphs; for a
real integration pass its own name and mark:

```csharp
var gmail = new StarterPromptApp("Gmail", myGmailGeometry);
```

A `StarterPromptApp` with no `Mark` shows the first letter of its name.

### Sending

<Demo name="Sending" />

`SubmitRequested` hands over the trimmed `Message`, the `Prompt` it came from (null when the person wrote it) and a
`CancellationToken`. Put your request's task in `Sending`: the footer shows Sending and the send button turns into
Stop until the task completes. When it completes the composer empties and the footer shows Sent. When it throws, the
footer shows an alert icon, the exception's message and Retry, and the text stays. Stop and Escape cancel the token
and keep the text; so does the control leaving the window.

```csharp
prompts.SubmitRequested += (_, e) =>
    e.Sending = chat.SendAsync(e.Message, e.CancellationToken);
```

Without a handler that sets `Sending`, the send is simulated and the footer says so. `SendStatus` and `SendError`
report the state, `Submit()` and `StopSending()` act like the keys, and `Text` is the composer's text (two-way).

### Your own composer

<Demo name="OwnComposer" />

`ShowsComposer="False"` hides the built-in composer, so the cards can feed a composer your app already has. A pick
still sets `Text` and raises `PromptSelected` with the starter.

### Loading

`IsLoading` shows placeholder cards with the same size and rhythm as the real ones; they breathe slowly and cannot be
picked, and Shuffle is disabled. When it turns off, the starters fade in inside the same frames.

### Narrow containers

<Demo name="Narrow" />

The cards sit in two columns from 560px of the control's width and in one column below. The tabs scroll inside
themselves with edge fades, Shuffle becomes an icon under 440px, and the padding, the corner radius and the greeting
step down under 480px. The control fills its container and its width never follows the cards or the text.

### Your own words

`Greeting`, `Description` (null hides it), `Placeholder` and `ModelName` with `ModelMark` are properties. Every other
word lives in `Labels`:

```csharp
prompts.Labels = new StarterPromptsLabels
{
    All = "Alle",
    Shuffle = "Mischen",
    Send = "Nachricht senden",
    CardName = (title, app, category) => app is null ? title : $"{title}, mit {app}",
};
```

### Content guidelines

- Titles are a few words and start with a verb ("Plan my week"); descriptions add the detail that makes it specific.
- Set `App` only when the prompt really uses that integration, so the mark means something.
- Keep greetings short and personal; one quiet line under it is enough.

### Best practices

- Use it as the first screen of a new chat or agent thread, then swap it for the conversation once a message is sent.
- Honor the cancellation token so Stop really stops the request.
- Keep the pool larger than `Count` so Shuffle has something to show; with fewer starters it is disabled.

### Common questions

**Does it render every starter?** No. Only the cards on screen exist; the pool can hold hundreds of starters.

**Can I select a tab from code?** Yes. `SelectedCategory` is two-way; an id that names no visible tab shows All.

## API reference

<!-- api: StarterPrompts -->

<!-- api: StarterPrompt -->

<!-- api: StarterPromptCategory -->

<!-- api: StarterPromptApp -->

<!-- api: StarterPromptsLabels -->

<!-- api: StarterPromptsSubmitEventArgs -->

<!-- api: StarterPromptSelectedEventArgs -->

## Accessibility

- The greeting is a heading. Each card is a button whose name reads its title, the connected app and the category
  ("Plan my week, uses Calendar, Plan").
- A polite live region announces picks, shuffles, sending, sent and errors, and says when a send was simulated.
- Status is never color alone: Sent shows a check and a label, a failure an alert icon and the message.
- The send button is named Send message, and Stop sending while a message is in flight.
- Every word is in `StarterPromptsLabels` or a property.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the tabs, Shuffle, each card and the composer |
| Left, Right | Move between the category tabs |
| Enter, Space | On a card, fill the composer with its prompt and focus it |
| Enter | In the composer, send the message |
| Shift+Enter | In the composer, add a new line |
| Escape | While sending, stop the send and keep the text |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Shuffle or tab change | Each card's content fades out in 100ms, then the new content fades in over 160ms rising 2px; the cards start 35ms apart (about 100ms in all) inside fixed frames | At once | At once |
| Starters arrive after loading | The content fades in the same way, staggered | At once | At once |
| Picking a card | The composer is focused and the new text fades in over 160ms | At once | At once |
| Footer status | Crossfades in place: out 100ms, in 160ms | A 120ms fade | At once |
| Send and stop icon | Crossfade over 160ms; nothing moves or resizes | At once | At once |
| Loading placeholders | Breathe once every 1.6s | Still | Still |
| Tab selection | The pill glides on the tabs' spring | At once | At once |

## Related

- `AgentStatus` for the live status of an agent once a run starts.
- `ToolCall` for the tool calls an agent makes.
- `Tabs` and `SegmentedControl` for switching views elsewhere.
- `EmptyState` for a useful next step when there is nothing to show yet.
