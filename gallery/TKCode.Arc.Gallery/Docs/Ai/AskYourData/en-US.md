---
title: Ask your data
summary: A natural language to chart card: the question goes in, the SQL streams into a collapsible, syntax tinted panel, the query runs with a rolling row count, and the answer builds as stacked bars, lines or a table with a one line insight underneath.
---

<Demo name="Preview" />

## When to use

`AskYourData` turns a question in plain words into a chart in one card. The model's SQL streams into a Generated SQL
panel with syntax tints, the query runs while a count of the rows scanned rolls, and the answer builds as stacked bars,
lines or a table, with a legend, a readout for every category and a one line insight streamed underneath. The SQL stays
one click away, so people can check or copy what ran.

- **Self serve analytics in a product**: a question box on a dashboard, a metrics page or an admin console.
- **An AI assistant's result view** when the answer is a number that is best read as a chart.
- **Teaching or auditing text to SQL**, where showing the generated query builds trust.

## When not to use

- **The query is fixed** and there is no question to ask: use `BarChart` or `LineChart`.
- **A database query as one step of a longer agent transcript**: use `ToolCall`.
- **People write and tune queries by hand**: use a SQL editor.

## Usage

Wire the card to your model and a read only database role with three delegates. With none of them it plays its
`Simulation`, answers you supply; with neither it waits in its empty state ("Ask a question about your data") and Ask
stays unavailable.

```xml
<arc:AskYourData Placeholder="Ask about your orders" />
```

Your model and database:

```csharp
card.Source = new AskYourDataSource { Name = "analytics", Dialect = "PostgreSQL" };

// The whole SQL at once: the card reveals it four tokens a tick at its final size.
card.GenerateSql = request => model.WriteSqlAsync(request.Question, request.CancellationToken);

// Run it read only and shape the rows for the chart. Throw to show the database's message.
card.RunQuery = async request =>
{
    var rows = await db.QueryAsync(request.Sql, request.CancellationToken);
    request.ReportProgress(rows.Count, rows.Count);
    return new AskYourDataResult
    {
        Title = "Weekly signups by plan",
        Categories = WeeksOf(rows),
        Series = SeriesByPlan(rows),
        RowCount = rows.Count,
        DurationMs = rows.Milliseconds,
    };
};

card.ResultReady += (_, e) => history.Save(e.Result.Title, e.Sql);
```

`AutoRun` is on by default only while a simulation writes the SQL; set it to `True` to ask the starting `Question` as
soon as the card is seen. `Ask()` asks the question in the field, `Ask(question)` puts a new one there first, and
`Stop()` cancels a run.

### Simulation

`AskYourDataSimulation` plays a model and a database you describe: the `Question` it asks on its own once the card is
35% on screen, the `Suggestions`, the `Source`, and its `Answers`. A question that contains one of an answer's
`Keywords` gets that answer's `Sql`, `RowsScanned` and `Result`; any other gets the `Fallback` (where `{keyword}` in the
SQL becomes the question's longest word), or no rows. The SQL is revealed after a short pause, the count rolls up in ten
steps of 110ms, then the chart builds and the insight streams; the live region adds that nothing was queried. It stands
in only for the delegates you have not wired, and your own `Question`, `Suggestions` and `Source` win over its own.
Setting `Simulation` to null stops its run and returns to the empty state.

```csharp
card.Simulation = new AskYourDataSimulation
{
    Question = "Weekly signups by plan since August",
    Suggestions = ["Weekly signups by plan since August", "Churn rate by plan per month"],
    Source = new AskYourDataSource { Name = "analytics", Dialect = "PostgreSQL" },
    Answers =
    [
        new AskYourDataSimulationAnswer
        {
            Keywords = ["signup", "sign up"],
            Sql = "SELECT week, plan, count(*) AS signups FROM signups GROUP BY week, plan;",
            RowsScanned = 18_342,
            Result = weeklySignups, // an AskYourDataResult you build
        },
    ],
};
```

The demos on this page take their answers from `AskYourDataDemoData`: an invented product's signups, MRR and churn.

### Streaming the SQL

<Demo name="Streaming" />

Give `StreamSql` an async stream of chunks from your model instead of `GenerateSql` and the panel grows as they arrive,
each chunk fading in. Call `ReportProgress(rowsScanned, totalRows)` from `RunQuery` to roll the count in the running
state; passing the total reserves its width so the line never shifts. Both delegates get a cancellation token: honour it,
so Stop really cancels.

```csharp
card.StreamSql = request => model.StreamSqlAsync(request.Question, request.CancellationToken);
```

### Results

`AskYourDataResult` is shaped for the chart: `Categories` are the X axis labels in order, and each `AskYourDataSeries`
has one value per category. Series add up by default (`Stacked`): bars stack, and the legend, the readout and the table
show totals. Set `Stacked = false` for rates and averages; bars then group side by side and nothing is totalled.

| Property | What it does |
|---|---|
| `Title`, `Subtitle` | The header once the chart is built ("Aug 3 to Oct 25 · 1,430 signups") |
| `LegendValue` | `Sum` for flows such as signups, `Last` for levels such as MRR, `None` for nothing |
| `Chart` | The view this result opens on; otherwise `DefaultChart` |
| `FormatValue` | Formats ticks, the legend, the readout and table cells ($258k, 3.9%) |
| `Insight` | One or two sentences that read the chart, streamed under it with its two lines reserved |
| `RowCount`, `DurationMs` | The SQL meta: "PostgreSQL · 48 rows · 214ms" |

A result with no categories or series shows "No rows matched" with the suggestions.

### Errors and stopping

<Demo name="Errors" />

Throw from `GenerateSql`, `StreamSql` or `RunQuery` and the card shows the message verbatim with Try again, and raises
`Failed` with the stage that failed (`Sql` or `Query`). While the card works the Ask button stops the run, and so does
Escape in the field; a stopped run reads "Stopped" and raises nothing.

### The SQL panel

The panel opens while the SQL is written and folds once the query starts, before the chart builds, so only one region
moves at a time. `KeepSqlOpen` leaves it open. Once the person opens or closes it during a run, their choice wins. The
SQL is tinted by the library's code highlighter and scrolls sideways in its own well; Copy SQL takes the whole query.

### Chart views

The switch changes between bars, lines and a table. Leave `Chart` null and each result opens on its own `Chart` or on
`DefaultChart`; bind `Chart` to control the view yourself. `ChartChanged` is raised when the person switches.

```xml
<arc:AskYourData DefaultChart="Table" KeepSqlOpen="True" />
```

### Narrow cards

<Demo name="Narrow" />

Below 520px the source keeps only its icon, the chart switch moves into the result above the legend at full width, and
the stage grows to fit the wrapped legend and insight. Category labels thin out to stay 46px apart, and wide SQL lines
and tables scroll inside their own wells.

### Your own words

`Labels` holds every word the card shows or says; `Placeholder` the field's hint and `Suggestions` the questions offered
in the empty state (an empty list hides them).

```csharp
card.Labels = new AskYourDataLabels
{
    WritingSql = "SQL wird geschrieben",
    RunningQuery = "Abfrage läuft",
    Rows = rows => $"{rows} Zeilen",
};
```

### Best practices

- Put the schema in your model's prompt and run the SQL with a read only role and a statement timeout.
- Keep series to four or fewer for the default palette.
- Let the model write the insight from the returned rows, with concrete numbers that are in the result.
- Batch very fast progress reports to a few a second.

## API reference

<!-- api: AskYourData -->

<!-- api: AskYourDataResult -->

<!-- api: AskYourDataSeries -->

<!-- api: AskYourDataSource -->

<!-- api: AskYourDataSimulation -->

<!-- api: AskYourDataSimulationAnswer -->

<!-- api: AskYourDataLabels -->

## Accessibility

- A polite live region narrates the run: writing SQL, running the query, then the chart title, the row count and the
  insight. Without `GenerateSql`, `StreamSql` and `RunQuery` it adds "Simulated with sample data, nothing was queried."
- Status is never color alone: a spinner, a drawn check, an alert or a stop glyph sits beside a text label in the SQL row
  and the stage header.
- The SQL row is a button that reports expanded or collapsed, named with its title and meta, such as "Generated SQL,
  PostgreSQL · 48 rows · 214ms".
- The chart is focusable and named after the result; the arrow keys move a readout that is announced, such as
  "Aug 10: Free 61, Pro 26, Team 10, Enterprise 2, total 99".
- The field shifts its border to the accent on focus; every other control shows Arc's keyboard ring.

### Keyboard

| Keys | Action |
|---|---|
| Enter | In the question field, asks the question |
| Escape | In the question field, stops a run in progress |
| Tab | Moves through the field, Ask, the Generated SQL row, Copy SQL, the SQL well, the chart switch, the chart, and the suggestions or Try again |
| Enter, Space | On the Generated SQL row, opens or closes the SQL |
| Left, Right, Home, End | On the chart, moves the readout between categories; on the chart switch, changes the view |
| Escape (chart) | Clears the readout |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| SQL streams | Four tokens every 34ms, each chunk fading in over 140ms; laid out first so the panel holds its final size | Whole at once | Whole at once |
| SQL panel opens or folds | 300ms bounce free height spring, content fades | At once | At once |
| Bars | Rise from the baseline in 320ms with at most 180ms of stagger | Drawn whole | Drawn whole |
| Lines | Draw from the left in 460ms | Drawn whole | Drawn whole |
| Switching views | Crossfade, 100ms out and 160ms in, in a fixed area | At once | At once |
| Header and SQL meta | Crossfade as single lines with a 2px settle | Short fade | At once |
| Rows scanned | Roll digit by digit in tabular numerals in reserved width | Change in place | Change in place |
| Insight | Four words a tick with its two lines reserved | Whole at once | Whole at once |
| Spinner and skeleton | Turn and breathe; pause off screen | Still | Still |

## Related

- `BarChart` and `LineChart` for a fixed query with no question to ask.
- `ToolCall` for a database query as one step of an agent transcript.
- `ReasoningBlock` for the model's thinking above an answer.
