---
title: Document extraction
summary: Turns a document into typed data and shows where each value came from: a quiet document viewer beside an editable form whose fields fill in as the model reads, with tinted source text, curved links, low-confidence review and a typed JSON result.
---

<Demo name="Preview" />

## When to use

Document extraction is for the moment a person has to trust what a model read from a document. The reviewer sees the document and the extracted data side by side, can see exactly where each value came from, and decides what to correct before the data goes on.

- **Invoices, receipts and orders into a system of record**: the fields fill in as the model reads, and the table of line items grows row by row.
- **Email and message triage**: the same viewer reads an email and pulls out the order, the carrier and the dates.
- **Reviewing the model's doubt**: values below a confidence threshold carry the model's reason and a "Looks right" button, and a Review button jumps to the next one.
- **Handing typed data to your code**: dates arrive as ISO text, amounts as numbers, IBANs without spaces, and a value that does not fit its type is null and blocks "Accept all".

## When not to use

- **A conversation about a document**: use `AiChat` or `AiAnswer`.
- **Reading very long documents**: the viewer shows one page at a time and the form is meant for a few dozen fields.
- **Training or scoring a model on many documents**: this is a person reviewing one document.
- **Showing a file**: the viewer draws the document model you give it, not a PDF file.

## Usage

`DocumentExtraction` derives from Avalonia's `TemplatedControl` and is driven by data. Give it `Samples` (a document with its schema each), a `Model` for the footer and an `Extractor` that calls your model.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DocumentExtraction x:Name="Extraction" MaxWidth="1080" />
</UserControl>
```

```csharp
extraction.Samples.Add(new ExtractionSample { Document = invoice, Schema = invoiceSchema });
extraction.SelectedSample = extraction.Samples[0];
extraction.Model = new ExtractionModelInfo { Name = "My model", Id = "my-model", InputPricePerMillion = 0.12m, OutputPricePerMillion = 0.60m };
extraction.Extractor = new MyExtractor();
extraction.Accepting += (_, e) => e.Defer(SaveAsync(e.Output.ToJson()));

public sealed class MyExtractor : IDocumentExtractor
{
    public async IAsyncEnumerable<ExtractionEvent> ExtractAsync(
        ExtractionDocument document, ExtractionSchema schema, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Send document.ToPlainText() and schema.ToJsonSchema() to your model, then report what comes back.
        await foreach (var found in model.ReadAsync(document.ToPlainText(), schema.ToJsonSchema(), cancellationToken))
        {
            yield return new ExtractionValueEvent(found.Path, found.Value, found.SourceId, found.Confidence, found.Note);
        }
    }
}
```

### The document

An `ExtractionDocument` is a name and a list of blocks drawn on a sheet of white paper (an email is drawn as a mail page): a masthead, a heading, paragraphs, address lines, key and value pairs, a band of labelled cells, columns, a table, totals, a rule and a footer. Text is made of runs and `DocumentSpan`s: a span is a piece of text a value can point back to, with an id that the extractor cites as `SourceId`.

### The schema

An `ExtractionSchema` lists the fields to find: a key, a label and a type (`Text`, `Date`, `Money`, `Currency`, `Iban`, `Integer`, `Number`). A table field has columns, and its values are reported by the path `key.row.column`. Fields belong to groups; each group takes one of the four series colors in turn, and the same color tints the dot, the field rows, the cited text and the links. Without groups each field gets a color of its own.

<Demo name="Custom" />

### A run

Extract reads the document: the status line shimmers "Reading the document", a sweep crosses the page, and then values land one by one. Each landed value fills its field, tints the text it came from, and raises a bar in the footer. The viewer follows the values until you scroll by hand. The button turns into Stop; stopping keeps the values found so far. A failed run keeps them too and offers Retry. Extract again starts over.

- **Events**: the extractor yields `ExtractionValueEvent` for each value (the text as the document writes it, such as "1,400.00"; dates as `YYYY-MM-DD`), `ExtractionUsageEvent` with token totals for the cost, and `ExtractionErrorEvent` or an exception to fail. Cancel through the token.
- **Cost and time**: the footer shows the model, the cost from the token totals and the prices, and the elapsed time.

### Reviewing

- **Linking**: hovering or focusing a field lights its text on the page and draws a curved link; pointing at text on the page lights its field, and clicking it focuses the field. In a narrow container the panes stack and no links are drawn.
- **Low confidence**: a value below `ReviewThreshold` (0.8) shows a warning, the model's reason and "Looks right". Review in the status line jumps to the first one.
- **Editing**: every field is a real input. Editing marks the value "Edited", clears its low-confidence mark and updates the JSON tab, where the edited line flashes. A value that does not fit its type shows a message, is null in the output and blocks Accept all.
- **Tabs**: Fields, JSON (the typed output, linked to the fields) and Schema (the JSON schema that is sent to the model).

<Demo name="Narrow" />

### Accepting

Accept all raises `Accepting` with the typed output. Take a deferral with `e.Defer(task)` to save asynchronously; set `Cancel` or fault the task to undo the accept. After a successful save every field shows as accepted. Editing a value afterwards returns only that value to unaccepted.

### Zoom

The viewer zooms from 40% to 200% in steps, with Ctrl or Cmd and the plus, minus and 0 keys. Fit keeps the page the width of the pane. Zoom, `IsFitWidth` and the tab are two-way properties.

### Wording

Every word comes from `Labels` (`DocumentExtractionLabels`): replace the instance to translate or reword; formatted texts are functions.

### Content guidelines

- **Field labels**: short nouns ("Invoice no.").
- **Descriptions**: say what to look for; they go to the model in the JSON schema.
- **Reasons**: the extractor's note should say why it is unsure, in one sentence.

### Best practices

- Cite a `SourceId` for every value that has a place in the document, so the text is tinted and linkable.
- Report real confidence; the review flow depends on it.
- Pass the cancellation token on so Stop and a new run really stop the model.
- Save in `Accepting` and throw when saving fails.

### Common questions

**Why is there no link line?** Links are drawn only in the side-by-side layout, and only when both ends are visible.

**Why is a value null in the JSON?** Its text does not fit its type, for example a date that is not `YYYY-MM-DD`.

**What does the average confidence count?** Every value the model found, table cells included.

## API reference

<!-- api: DocumentExtraction -->

<!-- api: ExtractionDocument -->

<!-- api: ExtractionSchema -->

<!-- api: ExtractionValue -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `ExtractionStarted` | A run starts | |
| `ExtractionCompleted` | A run ends: done, stopped or failed | `Result`, `Error` |
| `ValueChanged` | A value changes by the model or the person | `Path`, `OldText`, `NewText`, `Origin` |
| `ValueChecked` | The person marks a value "Looks right" | `Path` |
| `Accepting` | Accept all needs the output saved; set `Cancel` or fault a deferral to undo | `Output`, `Values`, `Cancel`, `Defer` |
| `Accepted` | The accept was saved | |
| `SelectedPathChanged` | The linked field changes | `Path` |

### Streaming and output

| Type | Description |
|---|---|
| `IDocumentExtractor` | `ExtractAsync(document, schema, cancellationToken)` returns the events of one run |
| `ExtractionValueEvent` | A value: the path, the text, the source span id, the confidence and a note |
| `ExtractionUsageEvent` | The token totals so far |
| `ExtractionErrorEvent` | The model could not finish |
| `ExtractionOutput` | The typed result in schema order; `ToJson()` writes it |
| `ExtractionModelInfo` | The model's name, id and prices per million tokens |

## Accessibility

- The viewer is a region named after the file and the zoom, the page is an article, and the form's inputs are real, named text boxes; a table's cells are named by column and row.
- Confidence is spoken in words ("Low confidence, 64 percent, check this value"), and status is never color alone: warnings, checks and edits have icons and words.
- Invalid values are marked invalid and described by their message; low-confidence values are described by the model's reason.
- Two polite live regions announce the run (started, how many were found and how many need a check, stopped, failed) and the review (checked, accepted, a value to fix first). A failed run is announced assertively.
- Reduced motion keeps every state and announcement.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | The file button, the zoom buttons, the page, the tabs, the action button, the fields in order (a low-confidence field is followed by its Looks right button), Accept all |
| Focusing a field | Selects it: the page scrolls to its text and the link is drawn |
| `Escape` | Stops a run; otherwise clears the selected field |
| `Ctrl` or `Cmd` with `+`, `-`, `0` on the page | Zoom in, zoom out, fit to width |
| `ArrowUp` / `ArrowDown` on the page | Scrolls it |
| `ArrowLeft` / `ArrowRight` on the tabs | Moves and selects |
| `Enter` / `Space` | Activates a button, or chooses a document in the menu |

## Motion

- Values land with a short fade of the text and the confidence column; the cited text flashes a darker tint and settles; footer bars rise on a spring, and the average and the cost roll.
- Table rows open on a spring; the model's reason opens and closes on a spring; the status line changes with a crossfade and its height follows.
- Links draw themselves in a fifth of a second with a gloss that passes once; switching links crosses over.
- The page zooms on a spring about its center, and the sweep crosses the page once at the start of a run.
- Accept all marks the fields one after the other, 35 ms apart.
- Reduced motion drops the sweep, the link drawing, springs and the staggers, and keeps the crossfades; Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Links | Draw and gloss | Appear and disappear | Appear and disappear |
| Landing values | Fade and flash | Fade | Immediate |
| Zoom, rows, bars | Springs | Immediate | Immediate |
| Accept all | Staggered 35 ms | All at once, with crossfade | Immediate |

## Related

- `ModelArena` for judging two models by their answers.
- `PromptStudio` for editing the prompt behind an extraction.
- `AiAnswer` for an answer that cites its sources.

| Component | Choose it when |
|---|---|
| `ModelArena` | You compare models, not read a document |
| `PromptStudio` | You maintain the prompt |
| `AiAnswer` | The result is prose with citations |
