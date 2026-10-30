---
title: Source cards
summary: A row of cited sources under an AI answer: cards stream into reserved slots while the model searches, the overflow collapses into a stack of site marks, hovering lifts a preview with the passage, and an inline [n] citation highlights its card.
---

<Demo name="Preview" />

## When to use

`SourceCards` shows the pages an AI answer relied on. While the model searches, a spinner and a shimmering "Searching
3 of 7" sit over reserved slots, and each result fades into its slot as it arrives, so the row never shifts. When the
search is done the status reads "7 sources · 2.1s", the answer streams in above the cards, and a `SourceCitation` chip in
the answer highlights the card it cites. Past `Max` the rest collapse into a stack of overlapping marks ("+4 more").
Hovering or focusing a card lifts a preview with the passage the answer used.

- **Citations under an AI search or docs assistant answer.**
- **Live search progress** ("Searching 3 of 8") while an agent browses.
- **Linking inline [n] references** in generated text to the pages they came from.

## When not to use

- **A full search results page** with many results and filters: use a list layout instead.
- **Tool calls with arguments and outputs**: use `ToolCall`.
- **A single link preview inside prose**: use a tooltip or a hover card.

## Usage

Give the cards your sources in citation order, so source 1 is cited as [1]. Append to an observable list as results
arrive while `Status` is `Searching` and `Total` says how many will come; switch to `Done` when the model starts
answering. Put the answer in `Content`: it sits between the status line and the cards, and its height springs as it
streams so the cards glide down.

```csharp
var sources = new AvaloniaList<SourceCardsSource>();
cards.Sources = sources;
cards.Query = "webhook signature verification";
cards.Total = 8;
cards.Status = SourceCardsStatus.Searching;

// As each result arrives.
sources.Add(new SourceCardsSource(result.Title, result.Url) { Site = result.Site, Snippet = result.Passage });

// When the model starts answering.
cards.Duration = searchTime;
cards.Status = SourceCardsStatus.Done;
```

With no `Sources` the cards show the state you set; untouched, they read "No sources found". For a demo or a prototype
without a search, give them a `SourceCardsSimulation` with the sources and the answer to play (a word written as `[n]`
becomes a citation). It plays while `Sources` is null and only on screen, its `Scenario` picks Live, Error or Empty, and
it starts over when the scenario changes or the person presses Retry. Its cards open nothing; setting `Sources` ends it.

```csharp
cards.Simulation = new SourceCardsSimulation
{
    Sources = recordedResults, // SourceCardsSource[] in citation order
    Query = "webhook signature verification",
    Answer = "Read the raw body before you parse it [1].",
    Duration = TimeSpan.FromSeconds(1.4),
    Scenario = SourceCardsScenario.Live,
};
```

### Streaming an answer with citations

<Demo name="Streaming" />

A `SourceCitation` is an inline chip. Put it in the answer with `InlineUIContainer`; inside `SourceCards` it names its
source for screen readers, turns accent while its card is highlighted, and pressing it highlights the card for 2.6s
(opening the stack when the card is in it).

```xml
<arc:SourceCards Status="Done">
    <TextBlock TextWrapping="Wrap">
        <Run Text="Read the raw body before parsing it " />
        <InlineUIContainer BaselineAlignment="Center">
            <arc:SourceCitation Number="1" />
        </InlineUIContainer>
        <Run Text="." />
    </TextBlock>
</arc:SourceCards>
```

Set `Highlighted` yourself to hold a highlight (for example while the reader hovers a sentence), or call
`HighlightSource(n)` for a timed one. `HighlightChanged` reports citation presses.

### The stack and narrow containers

<Demo name="Narrow" />

Wide containers show `Max` + 1 equal columns; below 540px the grid has two columns and the stack is the last tile.
The stack, the Show all toggle and a citation that points into the stack open it (`IsExpanded`, two-way) and raise
`ExpandedChanged`.

### Your own reader and failures

<Demo name="Reader" />

Pressing a card raises `SourceInvoked` and then opens the address in the browser. Mark the event handled to open the
source in your own reader instead, or set `OpensLinks="False"` so cards never open links. A failed search keeps what
arrived, drops its reserved slots and shows "Search failed" with `ErrorText`; `AllowsRetry` adds Retry (and Search
again when nothing was found), both raising `RetryRequested`.

### Status line

| Status | Icon | Reads |
|---|---|---|
| `Searching` | Spinner | "Searching 3 of 7" and the query |
| `Done` | Globe | "7 sources · 2.1s" |
| `Done`, no sources | Crossed search | "No sources found" and the query |
| `Error` | Alert | "Search failed" and `ErrorText` |

Set `IsStatusVisible="False"` when your UI already shows the search progress; Show all and Retry stay.

### Site marks

The card draws a lettered disc from the domain (W for webref.example). Pass the site's favicon shape as a 24 × 24 stroked
geometry in `SourceCardsSource.Icon` rather than inventing a logo.

### Your own words

`Labels` holds every word the cards and their citations show or say. Replace the object to localize or reword:

```csharp
cards.Labels = new SourceCardsLabels
{
    Searching = "Recherche",
    Count = n => n == 1 ? "1 source" : $"{n} sources",
    Progress = (n, total) => total is { } t ? $"{n} sur {t}" : $"{n}",
};
```

### Best practices

- Keep sources in citation order so [n] maps to card n; append, do not rebuild the list.
- Pass `Total` while searching so the slots are reserved and nothing shifts.
- Do not wrap cards in your own links: handle `SourceInvoked` for in-app reading.

## API reference

<!-- api: SourceCards -->

<!-- api: SourceCitation -->

<!-- api: SourceCardsSource -->

<!-- api: SourceCardsSimulation -->

<!-- api: SourceCardsLabels -->

## Accessibility

- The cards are a list named "Sources" that reports itself busy while searching; reserved slots are hidden from
  assistive technology.
- A polite live region says each status once: searching for the query, the number found, no sources, or the failure.
  Counts do not announce on every card. The simulation says that it is simulated and that its cards open nothing.
- Each card's name reads its number, title and domain, and says when it opens in the browser. While its preview is open
  the passage is the card's help text.
- `SourceCitation` is a real button named "Source 2: Request: text() method, webref.example"; its highlight is never color
  alone, because the card also gains an outline.
- The status always pairs an icon with words.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through Retry or Show all, the citations, each card and the stack. A focused card shows its preview |
| Enter, Space | Opens a card (or raises `SourceInvoked`), opens the stack, or highlights a citation's card |
| Escape | Closes the open preview |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A card arrives | Fades in rising 4px over 180ms; its slot fades out over 120ms | Fades, no rise | At once |
| Reserved slots | Bars pulse | Still | Still |
| The stack closes, or a failed search drops its slots | Leaving cells fade in place over 120ms while the stack fades into its cell; the grid's height glides once | Fades, height at once | At once |
| A count changes | Rolls 5px in a reserved width (tabular digits) | At once | At once |
| Status icon and words | Crossfade, 100ms out and 160ms in | 120ms crossfade | At once |
| The answer streams | Its height springs without bounce, so the cards glide down; a width change cuts straight to the new height | At once | At once |
| A preview opens | After a 320ms hover, fades and rises 4px from 0.98 scale over 160ms; it then switches between cards at once | Fades | At once |

Nothing bounces. The simulation pauses when it is scrolled off screen.

## Related

- `ToolCall` for the tool calls an agent makes.
- `AgentPlan` for the plan an agent follows while it searches.
- `AgentStatus` for the live status of a whole run.
