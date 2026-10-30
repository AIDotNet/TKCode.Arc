---
title: LLM cost calculator
summary: A pricing calculator for LLM workloads: set tokens per request, requests per day and prompt caching, and the models re-rank by monthly cost as bars glide and prices roll.
---

<Demo name="Preview" />

## When to use

`LlmCostCalculator` puts an honest monthly estimate for an LLM workload in one panel. The header shows the selected
model's monthly total in a large rolling figure. Presets, three sliders and a Cached input switch set the workload, and
every model sits in a list ranked by monthly cost, lowest first, with a bar relative to the most expensive model. Rows
glide to their new rank as values change; picking one selects that model for the total, the breakdown and the price line.

- **A pricing or settings page** that should show what a workload costs across several models.
- **Choosing a model for a feature**, when people should see how cost moves with prompt size, output length, traffic
  and caching.
- **An internal tool** that compares provider prices before a migration or a budget review.

## When not to use

- **Choosing a model in a chat or composer** without a cost comparison: use a model menu.
- **The live token usage of one conversation**: use `ContextWindow`.
- **Actual metered spend against a plan**: this estimates, it does not report.

## Usage

Pass the models to compare with their list prices in US dollars per 1M tokens, and say where the prices come from and
when they were checked in `Description`; without `Models` the panel shows its empty state ("No models to compare"):

```csharp
calculator.Models = priceList
    .Select(p => new LlmModelPrice(p.ApiId, p.Name, p.Provider, input: p.Input, output: p.Output, cachedInput: p.CachedInput))
    .ToList();
calculator.Description = $"List prices per 1M tokens, checked {priceList.CheckedOn:yyyy-MM-dd}";
calculator.Note = "List prices, not a quote. Calculated on this device, nothing was sent.";
```

The demos on this page compare real models at their vendors' list prices (standard tier, checked 2026-10-29). These are
the prices of short prompts: Claude Haiku 5.5 charges more for prompts over 100K tokens, the GPT-6 models over 272K and
Grok 4.7 from 200K, for the whole request; DeepSeek's prices are its peak rates (off-peak is half), and Gemini 3.8 Flash
is at its introductory price until the end of 2026. The workload presets are invented.

`CachedInput` is the cache read price; without it cached tokens bill at the input price. `Mark` takes the model's mark
as a 24 × 24 stroked glyph drawn at 16px; without one the row shows the provider's first letter in a tinted square.

### Your prices and workload

<Demo name="YourPrices" />

`Usage` is the workload and is two-way: input and output tokens per request, requests a day and whether caching is on.
Every slider step, preset and cache toggle writes it and raises `UsageChanged`. Values between slider stops still
compute exactly; the slider shows the nearest stop. `SelectedId` picks the model (null picks the second one; an unknown
id falls back to the cheapest), and `SelectedModelChanged` reports a row picked by pointer or arrow keys with its cost.
`SelectedModel` and `SelectedCost` always hold the current pick.

`CacheHitRate` is the share of input billed at the cached rate while Cached input is on (0.6 by default, shown beside
the switch). `DaysPerMonth` sets the billing month (30).

### The same numbers on the server

`LlmCostCalculator.ComputeCost` is the arithmetic the panel uses, so you can store the same estimate with a plan or put
it on an invoice:

```csharp
var cost = LlmCostCalculator.ComputeCost(model, new LlmUsage(16_000, 500, 5_000, Cached: true), cacheHitRate: 0.6);
// cost.Input, cost.CachedInput, cost.Output, cost.Total (dollars a month) and cost.PerRequest
```

### Presets

<Demo name="NoPresets" />

`Presets` are the workload shortcuts in the segmented row (the demos pass Chat, RAG, Agent and Batch). A Custom segment
lights up on its own when the sliders match no preset. Without presets, or with an empty list, the row hides.

### Loading, errors and no models

`IsLoading` shows placeholder rows with the list's exact geometry and disables the controls. `ErrorText` shows "Prices
did not load" with your reason in place of the list; set `AllowsRetry` for a Try again button that raises
`RetryRequested`. No `Models`, or an empty list, shows the empty state. The list area keeps one height in every state, and the
figures read as dashes while there is no list.

```csharp
calculator.IsLoading = true;
try
{
    calculator.Models = await prices.FetchAsync();
    calculator.ErrorText = null;
}
catch (HttpRequestException ex)
{
    calculator.ErrorText = ex.Message;
}
finally
{
    calculator.IsLoading = false;
}
```

### Narrow layouts

<Demo name="Narrow" />

The panel reads its own width, not the window's. Below 600px the sliders stack. Below 520px the total moves under the
heading, each bar takes its own line under the model name (rows grow from 44 to 58px) and the breakdown wraps into two
columns. Monthly costs switch to compact figures ($420K, $4.2M) from $100,000, so the cost column never overflows; the
total keeps every digit. The panel fills its container and caps at 720px.

### Your own words

`Labels` holds every word the panel shows or says; `Title`, `Description` and `Note` override the three texts most apps
change:

```csharp
calculator.Labels = new LlmCostCalculatorLabels
{
    Title = "Coût mensuel estimé",
    PerMonth = "/mois",
    Status = (name, cost, rank, count) => $"{name} coûte {cost} par mois, rang {rank} sur {count}.",
};
```

### Content guidelines

- Prices are inputs. Pass current list prices and say in `Description` where they came from and when.
- Name the tier the prices are for in `Note` when a vendor charges more for long prompts or less off-peak.
- Keep preset labels to one short word.

## API reference

<!-- api: LlmCostCalculator -->

<!-- api: LlmModelPrice -->

<!-- api: LlmUsagePreset -->

<!-- api: LlmCostCalculatorLabels -->

## Accessibility

- The panel is a group named by its heading. The model list is a list named "Models ranked by monthly cost, lowest
  first", whose rows are radio buttons with a single tab stop on the selected model.
- The accessibility tree lists the rows by rank, so a screen reader reads them in the order shown. Each row's name says
  the model, the provider, its rank and its monthly cost.
- The sliders are Arc sliders whose values are spoken as token and request counts, not stop numbers.
- One polite status region announces the selected model's monthly cost and rank once the numbers have been still for
  450ms (not on every drag step).
- Selection is shown by a tinted row and an accent bar, and reported as selected, never by color alone. The error state
  pairs an icon with the words "Prices did not load".
- The large total and the rolling figures are hidden from assistive technology where the status region and the row
  names already carry the value.
- Pointer focus draws no ring; keyboard focus uses Arc's shared focus ring.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the presets, the three sliders, the Cached input switch, the selected model row, then Try again in the error state |
| Arrow left, Arrow right | On the presets, moves between workloads; on a slider, moves one stop |
| Page up, Page down, Shift + Arrow | On a slider, moves two stops |
| Home, End | On a slider, the smallest or largest stop; in the model list, selects the cheapest or most expensive model |
| Arrow up, Arrow down | In the model list, selects the previous or next model by rank and moves focus with it |
| Space | Toggles Cached input |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A row's rank changes | Glides to its new place by transform on a critically damped spring (about 240ms, no bounce); no reflow | Jumps | Jumps |
| A bar's share changes | Follows the same spring | Jumps | Jumps |
| A price changes | Its digits roll to the new value over 240ms on an ease-out, in tabular figures | Jumps | Jumps |
| The selection changes | The model under the total and the price line crossfade (100ms out, 160ms in); the row tint and accent bar fade over 160ms | Short fade | At once |
| Loading, list, error and empty | The list area crossfades over 160ms at a fixed height | Short fade | At once |
| Loading | The placeholder rows pulse | Still | Still |

Rows, bars and numbers are drawn, not laid out, while they move, so a drag that re-ranks eight rows runs no layout pass.
Nothing runs while the panel is idle.

## Related

- `ContextWindow` for the live token usage of one conversation.
- `AgentTimeline` for the time, tokens and cost of one agent run.
- `Slider` and `SegmentedControl`, which the panel uses for its inputs.
