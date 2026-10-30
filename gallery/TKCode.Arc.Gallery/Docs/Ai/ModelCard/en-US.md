---
title: Model card
summary: A model details card where switching models rolls every price, limit and score in place, a volume slider estimates the monthly cost, benchmark bars show each model's rank, and Compare crossfades to a side by side table against any rival.
---

<Demo name="Preview" />

## When to use

`ModelCard` describes one AI model: what it takes in, how much it holds, what it costs and how it scores against close
rivals. Switching models rolls every price, limit and score to its new value in place, a volume slider estimates the
monthly cost live, and a Compare switch crossfades to a side by side table against any rival. The card never changes
size between models, views or states.

- **The detail pane of a model picker**, showing what the highlighted model can do and what it costs before someone
  selects it.
- **Model pages in docs or a marketplace**, with limits, prices and a quick benchmark read against close rivals.
- **Settings where a team picks a default model** and wants a side by side check against an alternative.

## When not to use

- **Choosing between many models**: render a compact picker and show one `ModelCard` for the highlighted model; the card
  is a detail view, not a row.
- **How full a live conversation is**: use `ContextWindow`.
- **Plan pricing**: use a pricing table; this card shows per token API prices.
- **Official leaderboards with many models**: the bar list shows the model and two rivals.

## Usage

Give the card a `Model`, a pool of `Rivals` and the `Benchmarks` to show. Pass one shared pool for every model: the card
filters out the model itself, so the benchmark rows and ranks stay stable while values roll. Without a `Model` the card
keeps its frame, reads "No model selected" and shows every value as not listed.

```csharp
var model = new ModelCardModel
{
    Id = entry.ApiId,                 // from your model catalog
    Name = entry.Name,
    Provider = entry.ProviderName,
    Summary = entry.Summary,
    Modalities = ModelCardModalities.Text | ModelCardModalities.Image | ModelCardModalities.Tools,
    ContextWindow = entry.ContextWindow,
    MaxOutput = entry.MaxOutputTokens,
    KnowledgeCutoff = entry.KnowledgeCutoff,
    Pricing = new ModelCardPricing { Input = entry.Input, Output = entry.Output, CachedInput = entry.CachedInput },
    Scores = entry.Scores,            // benchmark id → score; leave out what the vendor does not publish
};

card.Benchmarks = [new ModelCardBenchmark { Id = "gpqa-diamond", Name = "GPQA Diamond", ShortName = "Science" }];
card.Rivals = catalog;                // every model, the current one included
card.Model = model;
card.Note = "List prices and vendor-published scores, checked 2026-10-29.";
```

`ContextWindow` and `MaxOutput` are token counts (1,000,000 reads as 1M). `KnowledgeCutoff` is an ISO month such as
`"2026-03"` (shown as Mar 2026); any other text is shown as is. Prices are per million tokens in `Currency`. A missing
value reads "Not listed". `Note` is the footnote under the compare table: say where the figures come from and when they
were checked. Turn `IsSample` on while the figures are placeholders: the benchmark caption then ends in "Sample scores"
and the footnote says the prices and scores are samples.

The demos on this page show real models with their vendors' list prices (standard tier, checked 2026-10-29) and only
the benchmark scores each vendor publishes; Gemini 4 Argon's price is introductory, and Terminal-Bench scores are
reported at each vendor's own effort setting. Each Terminal-Bench version is its own benchmark id, so a 4.0 score is
never ranked against a 2.1 score; a model without a published score reads "Not published" (the demos set `Labels.None`
and `Labels.NotListed`), never zero.

### Anatomy

1. **Header**: the model's mark in a tinted square (or the first letter of its name), the name and the provider, and a
   Compare switch.
2. **Summary**: one sentence, two lines reserved (three on narrow cards), clamped.
3. **Modality chips**: all seven (text, image, PDF, audio, video, tools, reasoning); unsupported ones are dashed and muted.
4. **Specs**: context window with a gauge and the page count it holds (1M tokens is about 1,500 pages), max output and
   knowledge cutoff.
5. **Prices** per million tokens: input, output and cached input (the cached cell shows only when some model in the
   pool has a cached price).
6. **Cost estimate**: a monthly volume slider (1M to 1B tokens) and the estimated cost, billed by `InputShare`.
7. **Benchmarks**: a switcher, a caption, bars for the model and its first two rivals, and a readout with the rank.
8. **Footer**: the API model id, Copy model id and, for pickers, Use model.

### Compare

<Demo name="Compare" />

The Compare switch, or `View="Compare"`, crossfades to a table against one rival with a row per spec, price, the cost
at the chosen volume and every benchmark. The better value is set in a heavier weight with a check, the text "better"
for screen readers and its margin (+2.3, −20%, 2x). A tally under the table says who leads. Pick a rival from the menu,
or set `CompareWith` to its id.

### Loading and errors

<Demo name="States" />

`IsLoading` shows a pulsing skeleton, disables Compare and says "Loading model details". `ErrorText` replaces the body
with the message; `CanRetry` adds a Retry button that raises `RetryRequested`. Both keep the header and the card's size.
With no `Model` (Empty) the header reads "No model selected", Compare and the benchmarks hide and Copy model id is
disabled.

### In a model picker

<Demo name="Picker" />

`CanSelect` adds a primary Use model button that raises `SelectRequested` with the model; set `IsSelected` when it is
the current choice and the button reads Selected with a check. `SelectLabel` renames the button.

```csharp
card.SelectRequested += (_, e) => settings.DefaultModel = e.Model.Id;
card.IsSelected = card.Model?.Id == settings.DefaultModel;
```

### Narrow cards

<Demo name="Narrow" />

The card fills its container up to 640px and reads its own width. Below 480px specs and prices become rows of label
and value, the estimate drops under the slider, the benchmark switcher moves under its heading and the footer buttons
move under the model id. Below 420px the summary reserves three lines; below 360px each bar moves under its name. From
520px the compare table has a label column and shows margins and mini bars; narrower, each label sits above its two values.

### Events

```csharp
card.ViewChanged += (_, e) => Save("view", e.View);
card.CompareWithChanged += (_, e) => Save("rival", e.Id);
card.BenchmarkChanged += (_, e) => Save("benchmark", e.Id);
card.MonthlyTokensChanged += (_, e) => Save("volume", e.Tokens);
card.IdCopied += (_, e) => Log("copied", e.Id);
```

`View`, `CompareWith`, `Benchmark` and `MonthlyTokens` are two-way, so you can bind them instead. `MonthlyTokens` snaps
to the nearest stop: 1M, 5M, 10M, 25M, 50M, 100M, 250M, 500M or 1B.

### Your own words

`Labels` holds every word the card shows or says; sentences with values are functions:

```csharp
card.Labels = new ModelCardLabels
{
    Compare = "Vergleichen",
    Rank = (rank, count) => $"Platz {rank} von {count}",
};
```

### Reduced motion

With reduced motion views, bars, numbers and values change in place and the skeleton holds still. With motion off
everything changes at once.

### Content guidelines

- Keep the summary to one sentence of about 90 characters.
- Give scores for the same benchmark ids on the model and its rivals. A row without a score shows None and the compare
  table shows Not listed, so rows never appear or disappear.
- Turn `IsSample` on while the figures are placeholders; with real prices and scores leave it off and say where they come
  from in `Note`.
- Rank only scores of the same benchmark version: give each version its own id.
- Marks are decorative; pass any 24 × 24 stroked glyph as `Mark` and a `MarkColor`, or leave it out for a monogram.

### Common questions

**Why does the card not shrink when I hide the compare view?** Every panel is stacked in one cell, so the card is always
as tall as the tallest panel. Switching never moves what is around it.

**How is the monthly cost estimated?** `InputShare` of the volume is billed at the input price and the rest at the output
price (75% input by default).

## API reference

<!-- api: ModelCard -->

<!-- api: ModelCardModel -->

<!-- api: ModelCardPricing -->

<!-- api: ModelCardBenchmark -->

<!-- api: ModelCardLabels -->

<!-- api: ModelCardViewChangedEventArgs -->

<!-- api: ModelCardIdEventArgs -->

<!-- api: ModelCardMonthlyTokensEventArgs -->

<!-- api: ModelCardModelEventArgs -->

## Accessibility

- The card is named by the model's name. Each spec and price reads with its label.
- The compare table names each row by its label; the better value says "better", so the comparison never relies on color.
- A polite status region says what changed when you switch views, rivals or benchmarks ("Comparing Claude Opus 5.5 with
  Gemini 4 Argon"). Copy model id announces Copied on its own.
- Benchmark bars are hidden from assistive technology; each row is focusable and named by its full readout (score, rank and
  gap to the leader). Marks are decorative.
- Modality chips say supported or not supported; unsupported chips are dashed as well as muted.
- While loading Compare is disabled and the status says "Loading model details". An error is announced right away.
- Hidden panels take no pointer input and Tab never lands in them.
- Pointer focus draws no ring; keyboard focus shows Arc's focus ring.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the Compare switch, the volume slider, the benchmark switcher and each benchmark row (focus shows its readout), or the rival menu in the compare view, then Copy model id and Use model |
| Arrow keys, Page Up, Page Down, Home, End | On the volume slider, step through the monthly volumes; the estimate rolls to the new value |
| Space, Enter | Toggles Compare, copies the model id or selects the model |
| Arrow Left, Arrow Right | Move between benchmarks in the switcher |
| Enter, Arrow Down | Open the rival menu; arrows move between rivals, Enter picks one and Escape closes it |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Details, compare, loading, error | The old panel fades out in 100ms while the new one fades in over 160ms | At once | At once |
| A model switch: prices, limits, cost, pages, scores | Digits roll to the new value in tabular numerals | At once | At once |
| A model switch: name, summary, cutoff, readout | Crossfade in place (out 100ms, in 160ms) | At once | At once |
| Bars and the context gauge | Slide on a critically damped spring (0.24s, no bounce) | At once | At once |
| Bars on first view | Grow from zero once, 40ms apart, when the card is mostly in view | At once | At once |
| Hovering or focusing a benchmark row | The row tints, the others fade to half, the readout crossfades (160ms) | At once | At once |
| Loading | The skeleton pulses | Still | Still |

Every slot is reserved: two summary lines, seven chips, every spec and price cell, three benchmark rows and a readout
that holds its lines, so the card keeps one size.

## Related

- `ContextWindow` for how full a live conversation's context is.
- `CopyButton`, `Switch`, `Slider` and `SegmentedControl`, which the card uses.
- `DropdownMenu` for the rival menu.
