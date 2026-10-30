---
title: Enrich column
summary: An AI enrichment column for a data table, where one prompt runs down every row and each cell shimmers while it works, then settles into a value with its source or a failure you can retry.
---

<Demo name="Preview" />

## When to use

`EnrichColumn` is a table card with one AI column. One prompt runs down every row: each cell queues, shimmers while the
agent works on it, then settles into a short value with the source it came from, or a failure with a retry control. The
person can stop the run, re-run the column or a single row, and edit the column's name and prompt in place.

- **Enriching companies or leads** with facts an agent can look up: headcount, funding, tech stack.
- **Classifying or summarizing rows** with one prompt, with a visible source per answer.
- **Showing per-row AI progress** with stop, re-run and retry in a CRM, a sales tool or an internal admin.

## When not to use

- **Large spreadsheets** with many columns and editing: use a data grid and add an AI column there.
- **One question over a whole dataset**: ask it once and show one answer, not a column.
- **A log of one agent's tool calls**: use `ToolCall`.

## Usage

Give the column your rows and an `Enrich` delegate that fills one cell. It receives the row and an
`EnrichColumnContext`, and returns the value with its source. Throw to mark the cell failed: the exception's message
becomes the reason next to Retry.

```csharp
table.Rows = accounts.Select(a => new EnrichColumnRow(a.Id, a.Name, a.Domain)).ToList();
table.Column = new EnrichColumnDefinition("Headcount and funding", "Find the headcount and latest funding round");
table.Enrich = async (row, context) =>
{
    context.SetActivity($"Searching for {row.Name}");
    var answer = await service.EnrichAsync(row.Domain, context.Prompt, context.CancellationToken);
    if (answer is null)
    {
        throw new InvalidOperationException("No source found");
    }
    return new EnrichColumnResult(answer.Value, new EnrichColumnSource(answer.Host, answer.Url));
};
table.CellChanged += (_, e) =>
{
    if (e.Cell.Status == EnrichColumnCellStatus.Done)
    {
        store.Save(e.Row.Id, e.Cell);
    }
};
```

With no rows of its own the column plays its `Simulation`, a table you supply; with neither it shows the empty frame:
the top bar with "0 rows", an unnamed AI column, no rows and Run column unavailable. Rows of your own with no `Enrich`
show as they are, and nothing runs.

### Simulation

`EnrichColumnSimulation` is a table the column plays while `Rows` is null: a `Title`, the `Column`, the `Model` in the
footer, the `Rows`, and `Answers`, one `EnrichColumnSimulationAnswer` per row id with the value and source the cell
settles into. Its `Steps` are the activities the cell shows before that, each for its `Duration`; an answer with an
`Error` fails on its first attempt, so the failed state and Retry show, and the retry settles into the value. The column
runs it on its own when it first comes into view (`AutoRun`), says so in the top bar ("sample data") and to screen
readers, and sends nothing. Your own `Title`, `Column` and `Model` win over the simulation's; setting `Simulation` to
null stops the run and shows the empty frame.

```csharp
table.Simulation = new EnrichColumnSimulation
{
    Title = "Target accounts",
    Column = new EnrichColumnDefinition("Headcount and funding", "Find the headcount and latest funding round"),
    Model = new EnrichColumnModel("Your model", "web search"),
    Rows = [new EnrichColumnRow("acme", "Acme Robotics", "acme.example")],
    Answers =
    [
        new EnrichColumnSimulationAnswer("acme", "410 people · Series C", new EnrichColumnSource("acme.example/press", "https://acme.example/press"))
        {
            Steps =
            [
                new EnrichColumnSimulationStep("Searching for Acme Robotics", TimeSpan.FromMilliseconds(700)),
                new EnrichColumnSimulationStep("Reading acme.example", TimeSpan.FromMilliseconds(900)),
            ],
            Error = "No source found", // the first attempt fails, so Retry shows
        },
    ],
};
```

The demos on this page take their table from `EnrichColumnDemoData`: the companies and numbers are invented, the model
is a real one (checked on 2026-10-29).

### Your rows and service

<Demo name="YourData" />

The context carries the prompt and the column as they were when the cell started, a `CancellationToken` that fires when
the person stops the run or re-runs the column, the attempt number (1 on the first run, 2 on the first retry) and
`SetActivity`, which sets the shimmering label of the running cell and is safe from any thread. Pass the token to your
request: a cancelled cell goes back to what it showed before, and an answer that arrives after a stop is ignored.

### Anatomy

1. **Top bar**: the table's name (`Title`), the row count with `Note`, and the column button: Run column, Stop while it
   runs, Re-run once cells have values.
2. **Header**: the row column's header, then the AI column with a sparkle, its name over its prompt, and an edit control.
3. **Editor**: opens under the header with the column's name and prompt. Save and run applies them and re-runs every
   row; Cancel or Escape closes it.
4. **Rows**: the logo (16px; a letter monogram when the row has no `Logo`), the name and the domain, then the cell: a
   status icon, its content and the row's run or retry control, which shows under the pointer, on focus and on touch.
5. **Footer**: the model (`Model`, or the simulation's; null hides it) and the progress, with the failure count.

### States

| Status | Icon | Content |
|---|---|---|
| `Idle` | Faint dashed ring | A dash |
| `Queued` | Clock | "Queued" |
| `Running` | Spinner | A shimmering activity label |
| `Done` | Check that draws itself | The value (one line, full text on hover) and the source link |
| `Failed` | Alert in red | "Failed" and the reason |

### Running

`Concurrency` (2 by default) caps how many cells run at once. Workers start 140ms apart so the column fills top to bottom,
and each takes the next row from a queue as it finishes. Workers read the latest rows and column when they take a row, so
editing the rows mid-run never acts on stale data: a row removed while queued is skipped.

`Run()`, `Stop()` and `RunRow(id)` do what the buttons do. `IsRunning` is true while any cell is queued or running.
`RunCompleted` fires when a run settles, finished or stopped, with every cell.

### Saved values

<Demo name="SavedValues" />

Feed values you already have back through `InitialCells`, keyed by row id. `AutoRun` runs the column once when it first
scrolls into view, but only if no cell has a value yet. It is on while a simulation plays and off with your own
delegate unless you set it. `IsEditable="False"` hides the edit control.

### Narrow and long tables

<Demo name="Narrow" />

The card fills its container and is designed for widths up to 760px. Below 560px the domain, the source text and the
model detail fold away; the source keeps its arrow link. Below 440px the gutters tighten to 16px, values wrap to two lines
in 60px rows and a failure's reason hides behind its label (hover or focus it to read it). On touch screens the row
controls stay visible.

The rows are a virtualizing list. Give the card a height (or a `MaxHeight`) and only the rows on screen are built; without
one every row shows.

### Your own words

`Labels` holds every word the column shows or says. Replace the object to localize or reword:

```csharp
table.Labels = new EnrichColumnLabels
{
    RowHeader = "Account",
    RunColumn = "Enrich",
    Rows = n => $"{n} accounts",
};
```

### Content guidelines

- Keep values to one short line ("240 people · Series B"); the cell truncates with an ellipsis and shows the full value on
  hover.
- Give every value a source the person can open; a link they can check is what makes an AI value usable.
- Make failure reasons short and specific ("No source found", "Rate limited").

### Best practices

- Honor the cancellation token and keep `Concurrency` low for rate-limited services.
- Persist values from `CellChanged` and pass them back through `InitialCells` next time.
- For hundreds of rows, give the card a height so the list virtualizes, and keep concurrency low.

### Common questions

**What happens to a cell when the person stops?** Its request is cancelled and it shows what it showed before the run:
its previous value, or a dash.

**Does saving the column keep old values?** Every row re-runs with the new prompt; until a row's turn comes it shows
"Queued".

**How do I open sources myself?** Handle `SourceRequested` and set `Handled`; otherwise the system opens the address.

## API reference

<!-- api: EnrichColumn -->

<!-- api: EnrichColumnRow -->

<!-- api: EnrichColumnDefinition -->

<!-- api: EnrichColumnCell -->

<!-- api: EnrichColumnContext -->

<!-- api: EnrichColumnResult -->

<!-- api: EnrichColumnSource -->

<!-- api: EnrichColumnModel -->

<!-- api: EnrichColumnSimulation -->

<!-- api: EnrichColumnSimulationAnswer -->

<!-- api: EnrichColumnSimulationStep -->

<!-- api: EnrichColumnLabels -->

## Accessibility

- The rows form a table: the company is each row's header and the AI column's name and prompt are its column header.
- Every cell state pairs an icon with text (Queued, a shimmering activity, the value, Failed with a reason), never color
  alone.
- A polite live region announces runs, stops and the final count, and says when values are simulated and nothing was sent.
- Icon buttons carry labels that name the row ("Run Lumen Freight", "Retry Quarry Labs"); source links say they open a
  new tab.
- Every word is in `EnrichColumnLabels`.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the column button, the edit control, each row's run or retry control and source links. Row controls appear on focus |
| Enter, Space | Runs, stops or re-runs the column, runs one row, retries a failed cell or opens a source |
| Ctrl + Enter (⌘ + Enter) | Saves the edited column and re-runs it |
| Escape | Closes the column editor without saving and returns focus to the edit control |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A cell changes state | One crossfade in a shared grid cell: the old content out in 100ms, the new in 160ms rising 2px | 120ms crossfade, no rise | At once |
| Status icon | Crossfades over 120ms with a bounce-free scale (0.6 to 1) | Crossfade only | At once |
| Done tick | Draws in 240ms | Shown whole | Shown whole |
| Running label | A calm shimmer, one pass each 1.8s; a new activity trades places inside it | Still | Still |
| Spinner | One turn each 800ms | One turn each 1.6s | Still |
| Editor opens or closes | A critically damped height spring (0.25s, no bounce) | At once | At once |
| Row control appears | Fades over 160ms | Fades over 120ms | At once |

Nothing changes size while cells work: rows have a fixed height and a cell's content trades places in the same slot.

## Related

- `ToolCall` for the tool calls an agent makes.
- `AgentStatus` for the live status of a whole run.
- `Table` for a sortable, selectable data table without AI.
