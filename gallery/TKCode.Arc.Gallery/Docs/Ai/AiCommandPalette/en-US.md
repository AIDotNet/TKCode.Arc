---
title: AI command
summary: A Cmd+K command palette with an AI mode: search pages and actions, or press Tab to ask, and the answer streams in place with numbered sources and next steps that run from the list.
---

<Demo name="Preview" />

## When to use

`AiCommandPalette` is a command palette that can also answer questions. Typing searches your pages and actions; Tab
turns the query into a question for the model, and the answer streams inside the palette with numbered sources. The
next steps the model suggests, such as filing an issue or opening a doc, are rows you run with Enter, right there.

- **A global Cmd+K** in a product where people both jump to pages and ask about their workspace.
- **Search that falls back to an answer**: when nothing matches, the last row asks the model about the query.
- **Answers that lead to an action** in another tool, like filing an issue or posting a summary to a channel.

## When not to use

- **A long conversation with follow-ups**: use a chat thread; the palette answers one question at a time.
- **Plain navigation with no AI**: use the `CommandPalette` block.
- **Suggestions while typing in a form field**: use an autocomplete input.

## Usage

Give the palette your pages and actions as `AiCommandPaletteItem`s and answer questions with `Responder`. Without
either it is an empty palette, and a question gets no answer. `ModelName` is null by default.

```csharp
palette.ItemsSource = commands;
palette.ItemSelected += (_, e) => Navigate(e.Item.Id);
palette.Responder = async context =>
{
    var reply = await api.AskAsync(context.Question, context.CancellationToken);
    context.SetSources(reply.Sources);          // the chips land first
    await foreach (var delta in reply.Stream)
    {
        context.Append(delta);                   // cite sources as [1], [2]
    }
    return new AiCommandPaletteAnswer
    {
        Actions = [new AiCommandPaletteAction("issue", "Create issue in Linear", "ENG", ArcIcons.CircleDot,
            token => CreateIssueAsync(context.Question, token))],
    };
};
```

### Your pages and actions

<Demo name="Wired" />

Each item has a label, an optional `Group` that sections the results, a 16px `Icon`, quiet `Meta` text, a `Shortcut`
shown as key caps and extra `Keywords`. Items marked `IsSuggested` are listed while the query is empty. The search is a
synchronous word match: every word of the query has to start a word of the label, group, meta or keywords, so it suits a
few hundred rows; filter on your server beyond that. Choosing a row raises `ItemSelected` and runs the item's `Command`.

### Asking

`Responder` receives an `AiCommandPaletteAskContext` with the question and a cancellation token. Stream text with
`Append` in pieces of any size, and call `SetSources` before the text that cites them. Resolve with an
`AiCommandPaletteAnswer` holding the suggested actions (and the whole `Text` if you did not stream it). Throw to show
the failed state with a "Try again" row. The token is cancelled when the person asks again, goes back to search or the
palette leaves the window.

Citations are written as `[1]`, `[2]` in the text and become small buttons; hovering or focusing one lights its source
chip, and the other way round. Choosing either raises `SourceSelected`; a source with an `Href` opens it with the system
launcher unless you handle the event.

### Suggested actions

Each `AiCommandPaletteAction` runs in place: the row shows a spinner and its running label, then a check that draws once
and the label `Run` returned ("Created ENG-2481"), or an alert and the error label, where Enter retries.
`ActionCompleted` reports the result. Set `DoneMeta` to say what happened, for example that a simulated action sent
nothing.

### As a dialog

<Demo name="Dialog" />

`AiCommandPaletteDialog` shows the palette over the page, near the top of the window, over a dimmed scrim. Cmd+K (Ctrl+K
elsewhere) toggles it from anywhere in the window, `IsOpen` is two-way, and `arc:AiCommandPaletteDialog.Opens` on a button
opens it. Escape in an empty search, a press on the scrim and choosing a page close it. Put your own palette inside:

```xml
<arc:AiCommandPaletteDialog IsOpen="{Binding IsPaletteOpen}">
    <arc:AiCommandPalette ItemsSource="{Binding Commands}" />
</arc:AiCommandPaletteDialog>
```

Inline, `IsElevated` adds the floating shadow and `IsHotkeyEnabled` makes Cmd+K or Ctrl+K focus the input.
`CloseRequested` is raised by Escape in an empty search.

### Narrow containers

<Demo name="Narrow" />

The palette fills its container up to 680px and holds up at 360px. Long labels and meta text truncate with an ellipsis,
meta first; below 540px the navigate hint leaves the footer, and the source chips wrap. `ResultsHeight` sets the height
of the results area (392 by default), which never changes while typing, switching modes or streaming.

### Your own words

`Labels` holds every word the palette shows or says, including the placeholders, the "Searching your workspace" status
and the footer hints. `ModelName` and `ModelMark` name the model in the footer, in the input and beside each answer.

### Simulated workspace

For a demo or a prototype without a backend, give the palette an `AiCommandPaletteSimulation`: its `Items`, `Questions`
and canned `Answers` fill in what the palette lacks. The simulated model picks the answer whose `Keywords` the question
mentions most (an answer without keywords takes the rest), "searches" for 700ms, lands the sources and streams the text;
its actions pretend to run and say "Simulated, nothing was sent". With no items and no responder of its own the palette
also plays a walkthrough while it is on screen: it searches for `SearchQuery`, opens Ask AI, asks the first question and
runs the first action; the first real click or key press inside stops it for good.

```csharp
palette.ModelName = "Your model";
palette.Simulation = new AiCommandPaletteSimulation
{
    Items = [new AiCommandPaletteItem("inbox", "Inbox", "Pages", ArcIcons.Inbox) { IsSuggested = true }],
    Questions = ["What changed this week?"],
    SearchQuery = "in",
    Answers =
    [
        new AiCommandPaletteSimulationAnswer
        {
            Sources = [new AiCommandPaletteSource("notes", "Release notes")],
            Text = "Saved views shipped on Tuesday [1].",
            Actions = [new AiCommandPaletteSimulationAction("open", "Open release notes", "Docs", ArcIcons.FileText, "Opened")],
        },
    ],
};
```

```csharp
palette.Labels = new AiCommandPaletteLabels
{
    SearchPlaceholder = "Rechercher des pages et des actions",
    AskMode = "Demander à l’IA",
    Thinking = "Recherche dans votre espace",
};
```

### Best practices

- Keep item labels short and put context in `Meta`; use `Group` to section the results.
- Return sources before text so the chips land first, and cite them in the text.
- Give each action a `Run` that honors the token, and return the created object's name as the done label.

## API reference

<!-- api: AiCommandPalette -->

<!-- api: AiCommandPaletteDialog -->

<!-- api: AiCommandPaletteItem -->

<!-- api: AiCommandPaletteSource -->

<!-- api: AiCommandPaletteAction -->

<!-- api: AiCommandPaletteAnswer -->

<!-- api: AiCommandPaletteAskContext -->

<!-- api: AiCommandPaletteLabels -->

<!-- api: AiCommandPaletteSimulation -->

<!-- api: AiCommandPaletteSimulationAnswer -->

<!-- api: AiCommandPaletteSimulationAction -->

## Accessibility

- The input keeps the focus the whole time and reads as a combo box; the rows are list items, and the highlighted one
  reports itself selected, so arrows never move focus out of the input.
- A polite live region announces the question, the finished answer, action results and simulated runs ("Simulated,
  nothing was sent").
- Status is never color alone: a running, done or failed action changes both its icon and its label.
- Source chips and citations are buttons named with the source's full description; hovering or focusing one lights its
  partner.
- The mode toggle is a pair of pressed-state buttons that do not take focus from the input.

### Keyboard

| Keys | Action |
|---|---|
| Up, Down | Move through results, questions or suggested actions (wrapping at the ends) |
| Enter | Open the page, run the action, ask the question, or retry a failed answer or action; in Ask AI with text, ask it |
| Tab | Ask AI about the current query; with an empty query, open Ask AI with suggested questions, where Tab goes back to search |
| Escape | From Ask AI, back to search (cancels a streaming answer); in search, clear the query, then raise `CloseRequested` |
| Cmd+K, Ctrl+K | Focus the input with `IsHotkeyEnabled`; toggle an `AiCommandPaletteDialog` |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Views (results, questions, answer) | Old view fades out over 100ms, new one fades in over 160ms and rises 2px | 120ms fade | At once |
| Highlight between rows | Glides in 130ms; jumps without travel when the list itself changes | At once | At once |
| Answer text | 3 to 7 words every 34ms, each batch fading in over 140ms; the view follows the text down unless you scroll up | Up to 24 words every 240ms, no fade | Same as reduced |
| Source chips, suggested actions | Fade in over 160ms | At once | At once |
| Action labels, status icons, footer hints | One crossfade; the done check draws once | Short fade | At once |
| Walkthrough | Plays while on screen, pauses off screen and starts over clean | Never plays | Never plays |

The palette never changes size: the results area keeps its height in every view.

## Related

- `CommandPalette` (block) for a command menu without AI.
- `ToolCall` for the tool calls an agent makes.
- `AgentStatus` for the live status of a running agent.
