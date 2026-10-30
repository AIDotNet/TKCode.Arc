---
title: Generation stats
summary: A live stats strip for a streaming model response: time to first token, speed with a sparkline, output tokens and cost count up, then settle and open into a latency and token cost breakdown.
---

<Demo name="Preview" />

## When to use

`GenerationStats` describes one model response while it streams and after it ends. The head shows the model's mark and
name and a status with an icon and a word: Waiting, Streaming, "Done in 4.18s", "Stopped at 1.90s" or Failed. Below it
four cells: time to first token, speed in tokens per second with a small sparkline, output tokens and cost. While the
response streams every number counts up in tabular digits; when it ends they settle on exact values. Details opens a
breakdown with a latency bar and a token table with rates and cost.

- **Under assistant messages** in a chat product that shows people what a response cost or how fast it was.
- **Playgrounds, eval runners and internal dev panels** that compare models on latency, throughput and cost.
- **Admin or debugging views** that inspect a single request's token usage and cache hit rate.

## When not to use

- **How much of the context a conversation uses**: use `ContextWindow`.
- **Spend across many requests or days**: use a chart or a table; this strip describes one response.
- **A long running agent with steps**: use `AgentStatus`; this measures one model call.

## Usage

Feed the strip from the streaming handler you already have. Set `StartedAt` when the request leaves and `Status` to
`Waiting`; on the first chunk set `Usage.Ttft` and `Status` to `Streaming`; give it a new `Usage` with the growing
`OutputTokens` on every chunk; on the last one set `Usage.Duration` and `Status` to `Done`. Speed, latency and cost follow.
With no `Status` and no `Simulation` the strip is idle: the head reads Idle and every cell a dash.

```csharp
// When the request leaves. The provider is your own key for it, written as given into the copied JSON.
stats.Model = new GenerationModel(name: "Claude Sonnet 5.5", provider: "anthropic", id: "claude-sonnet-5-5");
stats.Pricing = new GenerationPricing(input: rates.Input, output: rates.Output, cachedInput: rates.CachedInput);
stats.StartedAt = DateTimeOffset.Now;
stats.Usage = new GenerationUsage { InputTokens = input + cacheRead, CachedTokens = cacheRead };
stats.Status = GenerationStatus.Waiting;

// On every chunk (the first one also sets Ttft and Status = Streaming).
stats.Usage = stats.Usage with { OutputTokens = outputSoFar };

// On the last one.
stats.Usage = stats.Usage with { Duration = DateTimeOffset.Now - started };
stats.Status = GenerationStatus.Done;
```

<Demo name="Streaming" />

### Anatomy

1. **Head**: the model's mark (a monogram, or your own glyph in `GenerationModel.Mark`), its name (it truncates rather
   than wraps), the status icon and word, and Details.
2. **Cells**: First token, Speed (with the sparkline), Output and Cost, split by hairlines.
3. **Error row**: when the request fails, the reason in `ErrorText` and Retry.
4. **Breakdown**: the latency bar (first token, generation, total), the token table (input, cached input, output,
   reasoning, total) with rates and cost, a note and a button that copies the stats as JSON.

### States

| Status | Head reads | Cells |
|---|---|---|
| `Idle`, or no `Status` and no `Simulation` | Idle | Dashes |
| `Waiting` | A spinner and "Waiting" | First token counts live from `StartedAt` |
| `Streaming` | An accent spinner and "Streaming" | Output, speed and cost count up; the sparkline draws |
| `Done` | A check that draws itself and "Done in 4.18s" | Exact values |
| `Stopped` | A square and "Stopped at 1.90s" | What arrived |
| `Error` | An alert and "Failed" | What arrived; the error row opens |

### Speed and cost

Speed is measured when a chunk lands (tokens since the first token over the time since it), so it holds still between
chunks; once the response ends it is the output tokens over the generation time. The sparkline adds a smoothed reading
every 160ms while the output grows and halves its points when it reaches 48, so it always shows the whole run. Pass
`Usage.TokensPerSecond` or `SpeedSamples` when your service reports them.

Cost comes from `Pricing` (US dollars per million tokens): uncached input at the input rate, cached input at the cached
rate (the input rate when none is given), output and reasoning at the output rate. When your provider reports the cost,
pass `Usage.CostUsd` and leave `Pricing` out; the Rate column then hides.

Map provider usage like this: Anthropic `input_tokens` plus `cache_read_input_tokens` is `InputTokens` and
`cache_read_input_tokens` is `CachedTokens`; OpenAI `prompt_tokens` and `prompt_tokens_details.cached_tokens` map the same
way, and `completion_tokens_details.reasoning_tokens` is `ReasoningTokens`. `OutputTokens` counts output without
reasoning.

### Breakdown

<Demo name="Breakdown" />

Details opens the breakdown; `IsExpanded="True"` opens it on first render. `DetailsToggled` is raised when the person
presses Details. `Note` puts a footnote under it, such as where the rates come from.

### Inline

<Demo name="Inline" />

`Variant="Inline"` is one quiet line for the space under a chat message ("412ms to first token · 88 tok/s · 284 tokens ·
$0.0049"). It wraps onto a second line on phones instead of scrolling, and the breakdown opens as a panel below it.

### Failures and stops

When the person stops a response, set `Usage.Duration` and `Status` to `Stopped`. When the request fails, set `ErrorText`
to a short, specific reason and `Status` to `Error`: the error row opens with Retry, which raises `RetryRequested`.
`AllowsRetry="False"` hides it.

### Simulated runs

Leave `Status` unset and give the strip a `GenerationStatsSimulation` to play runs of your own, for a landing page, a
prototype or a demo: each `GenerationStatsRun` names the model and its prices, the token counts it ends on, the time to
the first token and the peak speed. The strip waits, streams in chunks with a ramp up and a little waver, settles and moves
on; a run with `FailAtTokens` fails there once with its `ErrorText` and succeeds when retried. The runs loop while the
strip is on screen, hold while the breakdown is open and settle at once under reduced motion. The preview above plays four
runs of real models at their list prices; the tokens and timings are invented.

```csharp
stats.Simulation = new GenerationStatsSimulation { Runs = MyRuns.All };
stats.Note = "Simulated runs at list prices.";

public static class MyRuns
{
    public static IReadOnlyList<GenerationStatsRun> All { get; } =
    [
        new()
        {
            Model = new GenerationModel("Claude Sonnet 5.5", "anthropic", "claude-sonnet-5-5"),
            Pricing = MyPrices.ClaudeSonnet, // your price list, US dollars per million tokens
            InputTokens = 3_682,
            CachedTokens = 2_816,
            OutputTokens = 284,
            TimeToFirstToken = TimeSpan.FromMilliseconds(412),
            PeakTokensPerSecond = 96,
        },
    ];
}
```

In XAML, set `<arc:GenerationStats.Simulation>` to a `GenerationStatsSimulation` whose `Runs` come from `{x:Static}`, as the preview's Code tab shows. Give each strip its own simulation (it keeps the playback state); the runs can be shared. Setting `Status` hands the strip
to your data, and setting it back to null starts the simulation again from its first run.

### Responsive

The four cells sit in one row from 600px of strip width and in a two by two grid below it, so the speed cell keeps room
for its sparkline. Below 440px Details becomes an icon button (its name stays "Show details" / "Hide details") and the
table's Rate column hides. The strip always fills its container; numbers count in reserved tabular cells, so nothing
beside them moves.

### Your own words

`Labels` holds every word the strip shows or says. Replace the object to localize or reword:

```csharp
stats.Labels = new GenerationStatsLabels
{
    DoneIn = time => $"Terminé en {time}",
    FirstToken = "Premier jeton",
    Details = "Détails",
};
```

### Best practices

- Keep the model name as people know it ("Claude Sonnet 5.5") and put the API id in `GenerationModel.Id`.
- Use the inline variant under chat messages and the card in eval views, playgrounds and admin panels.
- Give a new `Usage` per chunk instead of rebuilding the strip; the numbers glide from where they are.

## API reference

<!-- api: GenerationStats -->

<!-- api: GenerationModel -->

<!-- api: GenerationUsage -->

<!-- api: GenerationPricing -->

<!-- api: GenerationStatsSimulation -->

<!-- api: GenerationStatsRun -->

<!-- api: GenerationStatsLabels -->

## Accessibility

- The strip is a group named after the model ("Claude Sonnet 5.5 stats"). Each cell is named by its label and value, so it
  reads "First token, 412ms".
- Details is a button that reports expanded or collapsed; at narrow widths it becomes an icon button that keeps the name
  "Show details" or "Hide details".
- A polite status region announces settled outcomes ("Claude Sonnet 5.5 done in 4.18s, 284 output tokens at 88 tokens per
  second, $0.0049"), failures and stops, never the live counts. A playing simulation says once that its runs are simulated
  and stays quiet until someone interacts with the strip.
- Status never relies on color: a spinner, a check, an alert or a square always sits next to a word. The latency bar is
  decorative; its values are listed beside it in text.
- Each row of the token table is named with its column headers ("Input: Tokens 866, Rate $2.00 per million tokens, Cost
  $0.0017").
- Pointer focus draws no ring; keyboard focus shows Arc's focus ring.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves to Details, then to Retry when a response failed, then to the copy button inside an open breakdown |
| Enter, Space | Opens or closes the breakdown, retries, or copies the stats as JSON |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A number changes | Glides to the new value over about 100ms in tabular digits; a drop cuts straight to it | At once | At once |
| Status changes | Old word and icon fade out in 100ms, the new ones fade in over 160ms rising 2px | Short crossfade | At once |
| Done | The check draws itself once | Drawn whole | Drawn whole |
| Waiting, streaming | The spinner turns once every 800ms | Holds still | Holds still |
| Breakdown or error row | Height on a 0.26s critically damped spring, content fades over 160ms; nothing bounces | At once | At once |
| Details chevron | Turns on the same spring | At once | At once |
| Simulated runs | Play in a loop while on screen; hold while the breakdown is open | Stop on a settled run | Stop on a settled run |

The live clock runs only while waiting or streaming, and a simulation ticks in virtual time only while the strip is on
screen.

## Related

- `ContextWindow` for how much of a model's context a conversation has used.
- `AgentStatus` for the live status of a long running agent.
- `ToolCall` for the tool calls an agent makes.
