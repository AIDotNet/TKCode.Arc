---
title: Smart form fill
summary: A paste-to-fill form card: AI reads an email signature or messy notes and fills a contact form field by field, with a confidence dot per value, its source lit up in the text, review for guesses and Undo.
---

<Demo name="Preview" />

## When to use

`SmartFormFill` turns pasted text into a record. People paste an email signature, a forwarded message or notes into the
well; AI reads it and fills the form one field after another. Each value fades in with a confidence dot, the words it came
from light up in the pasted text, guesses ask for review before the form can be saved, and Undo puts the old values back.

- **Creating a contact or a lead** from an email signature or call notes.
- **Onboarding and CRM flows** where typing six fields by hand is the main friction.
- **Vendor, company or shipping records** that arrive as unstructured text; pass your own fields.
- **Any form where AI-filled data must be checked** before it is saved, with visible confidence and an easy review path.

## When not to use

- **Plain data entry** with no unstructured source: use `Input` and the other fields directly.
- **Long multi-page applications**: use a step-by-step form and add a paste step to one page.
- **Bulk imports of many records**: use a table import flow instead of one card.

## Usage

Give the card your model through `Extract`. It receives the pasted text and a `CancellationToken`, and returns a
`SmartFormFillResult`: a `SmartFormFillValue` per field id with the value, a confidence from 0 to 1, the exact substring of
the text it came from and, for a guess, a note that says why. Fields the result leaves out keep their value. `Submit`
saves the form; the button shows a loading state while its task runs.

```csharp
card.Extract = async (text, token) =>
{
    // Ask your model for structured output: { field: { value, confidence, source, note } }.
    var contact = await contacts.ExtractAsync(text, token);
    var result = new SmartFormFillResult();
    foreach (var field in contact.Fields)
    {
        result[field.Id] = new SmartFormFillValue(field.Value, field.Confidence, field.Source, field.Note);
    }
    return result;
};
card.Submit = values => crm.SaveContactAsync(values);
```

With no extractor the card reads with a simulated reader: the `Result` of a sample whose text was pasted, and a small
rule-based parser for anything else (nothing is sent). With no `Extract` and no `Simulation` the form starts empty, with no
Try sample button and no model named.

### Simulation

To show the card without a model (a demo, a design review, an onboarding tour), give it a `SmartFormFillSimulation`: the
samples behind Try sample, each with the result a read returns (`SmartFormFillSample.Result`), and the model to name.
With full motion the card then plays itself in a loop (paste, fill, check, undo) until someone clicks or types inside it.
Your own `Extract` wins over the simulation, and setting `Simulation` to null stops it and clears what it filled. Set
`AutoPlay="False"` in product screens.

```csharp
card.Simulation = new SmartFormFillSimulation
{
    ModelName = "DeepSeek-V4.1-Flash-1017",
    Samples =
    [
        new SmartFormFillSample("Email signature", signature)
        {
            Result = new SmartFormFillResult
            {
                ["name"] = new SmartFormFillValue("Maya Okafor", 0.98, "Maya Okafor"),
                ["email"] = new SmartFormFillValue("maya.okafor@example.com", 0.99, "maya.okafor@example.com"),
            },
        },
    ],
};
```

The preview above plays a simulation whose texts and results are the gallery's own demo data.

### Anatomy

1. **Header**: the title and one line under it (`Title`, `Description`; an empty description hides the line).
2. **Paste well**: a heading with the model that reads ("Read by DeepSeek-V4.1-Flash-1017"), the paste area, and Try sample, Edit text
   and Fill. Once a read starts, the paste area becomes the pasted text, where each field's source is highlighted.
3. **Form**: the fields in order, two half fields to a row. Each has its label, a confidence badge (a dot plus a percentage,
   or Review, Checked or Edited), an Arc `Input`, and for a guess a warning ring, a check button and the reason under it.
   A company field shows the company's logo or a monogram at its start.
4. **Footer**: the status with an icon, then Undo and the submit button.

### Reading and filling

Pasting into the area (Cmd/Ctrl+V) starts a read right away; Cmd/Ctrl+Enter or Fill reads typed text; Try sample pastes the
next sample. While it reads, the status shimmers "Reading with DeepSeek-V4.1-Flash-1017" and Fill turns into Stop. Stop or Escape cancels
the extractor's token and nothing changes.

When the result arrives, fields fill in form order 140ms apart. The source of the field being filled takes a stronger tint
in the pasted text; afterwards hovering or focusing a field does the same, so cause and effect read across the card.
`Filled` is raised once every field from a read has landed, and `ValuesChanged` on every change, typed or filled.

```csharp
card.Filled += (_, e) => analytics.Track("contact-filled", e.FilledCount, e.ReviewCount);
card.ValuesChanged += (_, e) => draft.Save(e.Values);
```

`ReadAsync(text)`, `Stop()`, `TrySampleAsync()`, `Check(id)`, `Undo()` and `SubmitAsync()` do the same from code;
`GetFieldValue` and `SetFieldValue` read and write one field.

<Demo name="Extractor" />

### Review and Undo

A value whose confidence is below `ReviewThreshold` (0.8 by default) asks for review: its badge reads Review, a warning ring
surrounds the input, the reason shows under it and a check button sits at its end. Checking it, or editing the value,
clears the review. Save does not go through while a field still asks for review: it says how many are left and focuses the
first. Undo restores the values from before the last fill and raises `Undone` with them.

### Your own fields

<Demo name="CustomFields" />

`Fields` replaces the contact form. A field has an `Id` (the key in values and results), a `Label`, a `Kind` (`Text`,
`Email`, `Phone`, `Url` or `Company`, which adds the logo slot), a `Placeholder` and a `Span` (`Half` or `Full`).
`DefaultValues` fills fields before anything is read. `Samples` sets the texts behind Try sample (give each a `Result`
for the simulated reader to return); none hides the button and the autoplay.

```xml
<arc:SmartFormFill Title="New vendor" SubmitLabel="Add vendor" AutoPlay="False">
    <arc:SmartFormFillField Id="company" Label="Vendor" Kind="Company" Span="Full" />
    <arc:SmartFormFillField Id="email" Label="Billing email" Kind="Email" />
</arc:SmartFormFill>
```

`ResolveLogo` gives the company field your own mark: return a control for a company name, or null for the monogram.
`ModelName` and `ModelMark` name the model beside the paste area.

### Narrow columns

<Demo name="Narrow" />

The card follows its own width, not the window's. From 720px of content the well sits left of the form; below that it
stacks on top at a fixed height. Under 440px every field takes the full row and the footer puts the status above the
actions. It holds at 360px without horizontal scrolling.

### Your own words

`Labels` holds every other word the card shows or says; replace the object to localize or reword it:

```csharp
card.Labels = new SmartFormFillLabels
{
    TrySample = "Beispiel",
    Fill = "Ausfüllen",
    Reading = model => $"{model} liest",
    Filled = (filled, review) => $"{filled} Felder ausgefüllt",
};
```

### Content guidelines

- Write `Description` as one plain sentence about what to paste.
- Keep notes short and concrete: "Two numbers in the text; picked the mobile.", not "Low confidence".
- Return the exact source substring; a paraphrased source cannot be highlighted.

### Best practices

- Keep `ReviewThreshold` around 0.8 so inferred states, paraphrased titles and one of several phone numbers are flagged
  rather than saved silently.
- Pass the token on to your HTTP call so Stop, Escape and a new paste really cancel the request.
- Turn `AutoPlay` off in product screens; the loop is for docs and marketing pages.

### Common questions

**Can people type instead of pasting?** Yes. Every field is an ordinary input, and typed text in the paste area is read
with Fill or Cmd/Ctrl+Enter.

**What happens to values people typed before a read?** A read changes only the fields it returns; Undo brings back what
was there before it.

## API reference

<!-- api: SmartFormFill -->

<!-- api: SmartFormFillField -->

<!-- api: SmartFormFillValue -->

<!-- api: SmartFormFillSample -->

<!-- api: SmartFormFillSimulation -->

<!-- api: SmartFormFillLabels -->

## Accessibility

- The footer status is a polite live region: it announces reading, how many fields were filled and how many need review,
  checks, undo and save. With the built-in reader it adds "Simulated".
- Each filled input describes its confidence ("Filled by AI, 94% confident"); a guess also gives the reason and reports a
  Review status until it is checked or edited.
- Status is never color alone: confidence shows a dot plus a percentage or a word, and the footer pairs an icon with text.
- The check button inside a field that needs review is named after the field ("Mark Phone as checked"). Save focuses the
  first field that still needs review.
- The pasted text view is a focusable, labelled region ("Pasted text"), so keyboard users can scroll it; keyboard focus
  draws a ring around it.

### Keyboard

| Keys | Action |
|---|---|
| Cmd/Ctrl + V | Pastes into the text area; the read starts right away |
| Cmd/Ctrl + Enter | Reads the typed text in the text area |
| Escape | Stops a read in progress; nothing changes |
| Tab | Moves through the text area, Try sample, Fill, every field, the check buttons, Undo and Save |
| Enter, Space | Presses the focused button |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Values arrive | In form order, 140ms apart | All at once | All at once |
| A value lands | Fades in over 200ms; the input breathes an accent tint for 900ms; nothing moves | At once, no tint | At once |
| Active source in the text | Its highlight takes the full tint over 160ms | Same | At once |
| Badges, logo and status | Old out in 100ms, new in over 160ms rising 2px | Short crossfade | At once |
| Reading label | A calm shimmer | Still | Still |
| Review ring | Fades in over 160ms | At once | At once |
| Autoplay loop | Plays while the card is on screen | Never plays | Never plays |

No layout animates: values, badges and highlights change in place, so the card never jumps while it fills. The loop pauses
when the card leaves the screen, and a read in flight is cancelled when the card is removed.

## Related

- `Input` and `Textarea` for the fields the card is built from.
- `ToolCall` for showing what an agent did with a tool.
- `ContextWindow` for how much of a model's context a conversation uses.
