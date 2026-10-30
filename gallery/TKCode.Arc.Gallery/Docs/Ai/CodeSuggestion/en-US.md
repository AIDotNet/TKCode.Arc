---
title: Code suggestion
summary: An AI code edit suggestion where the proposed changes stream into a small editor panel as inline diff hunks, and the person accepts or rejects each one, or all at once with Cmd or Ctrl + Enter.
---

<Demo name="Preview" />

## When to use

`CodeSuggestion` shows an AI's proposed edit to one file as an inline diff in a small editor panel. The header names the
model and what the person asked for; the panel shows the file with each change as a hunk: removed lines tinted red with
a minus, new lines tinted green with a plus. New lines stream in, then every hunk gets Reject and Accept under it, and
the footer offers Reject all and Accept all. Accepted hunks fold into the file with a smooth height change.

- **Inline edits in an editor**, where the model rewrites part of a file the person has open.
- **Approving an agent's file change** before it is written, with a clear record of what was kept.
- **Config or SQL change previews**, where every line that changes should be read before it lands.
- **Landing pages and docs** that show an AI coding feature with a realistic, self playing edit.

## When not to use

- **Large multi file diffs or side by side comparisons**: use a dedicated diff tool; this card is for one file and a
  handful of hunks.
- **Code without changes**: use `CodeBlock`.
- **An agent step that is not a file edit**: use `ToolCall`.
- **Approving actions in general** (emails, payments, deploys): use an approval card, not a code review.

## Usage

Give the card the file in `Code`, the changes in `Hunks` and set `Status`. Each `CodeSuggestionHunk` replaces `Remove`
lines starting at the zero based line `Start` with its `Insert` lines (remove 0 inserts before `Start`, an empty insert
deletes). Hunks that overlap one before them are ignored. With no code and no hunks the card shows an empty file.

```xml
<arc:CodeSuggestion FileName="load.ts"
                    FilePath="src/api"
                    Instruction="Time out slow requests"
                    Code="export async function load(url: string) {&#10;  const res = await fetch(url);&#10;  return res.json();&#10;}">
    <arc:CodeSuggestionHunk Id="timeout" Start="1" Remove="1" Summary="Add a 10s timeout"
                            InsertText="  const res = await fetch(url, { signal: AbortSignal.timeout(10_000) });" />
</arc:CodeSuggestion>
```

```csharp
// Write the file once every hunk has a decision; the card never writes anything itself.
private async void OnResolved(object? sender, CodeSuggestionResolvedEventArgs e)
{
    await File.WriteAllTextAsync(path, e.Code);
}
```

`DecisionMade` fires for every hunk accepted or rejected, with the file as it now stands, if you prefer to write as the
person goes. `GetCode()` returns the same text at any time, and `Accept`, `Reject`, `AcceptAll` and `RejectAll` decide
from code.

### Anatomy

1. **Header**: the model's mark in a tinted square, the instruction as the title and the model's name under it.
2. **File tab**: a two letter language mark, the file name, its folder (from a 520px card) and the running +/− count.
3. **Code**: fixed 20px rows with line numbers; a pending hunk's removed lines are red with a minus, its new lines green
   with a plus. Pending new lines have no number yet.
4. **Review bar**: under each pending hunk, its summary with Reject and Accept.
5. **Footer**: the status (an icon and a label) and the actions: Stop, Reject all and Accept all, Regenerate or Retry.

### Streaming

<Demo name="Streaming" />

Set `Status` to `Thinking` while the model reads the file, then to `Streaming` and grow `Hunks` as tokens arrive: add a
hunk with no lines, then append to its lines with `AppendLine` (or replace `Insert`). Each new line opens to its row and
fades in. Set `Status` to `Ready` when the answer is complete; every hunk then gets its review bar. Going back to
`Thinking` clears earlier decisions.

`AllowsStop` shows Stop while the model reads or writes and raises `StopRequested`: abort your request and set the
status. `AllowsRegenerate` shows Regenerate once the review is done and Retry after an error, and raises
`RegenerateRequested`.

### States

| Status | Code | Footer |
|---|---|---|
| `Thinking` | The plain file | A spinner and a shimmering "Reading fetch-invoice.ts", Stop |
| `Streaming` | Hunks as they grow | A spinner and "Writing changes", Stop |
| `Ready` | Hunks with review bars | A dot and "3 changes to review", Reject all and Accept all |
| `Ready`, all decided | The file as it now stands | A check and "Applied 2 of 3 changes" or a cross and "Rejected all 3 changes", Regenerate |
| `Error` | The plain file | An alert and `ErrorText`, Retry |

### Narrow cards

<Demo name="Narrow" />

The card fills its container up to 720px. Long lines scroll sideways inside the code area while the line numbers stay
pinned on the left and each review bar stays pinned to the visible width, so Accept is always in reach. The folder hides
below a 520px card. Below 440px the gutter narrows, code drops to 12px, hunk summaries hide (the buttons keep their
names for screen readers) and the footer stacks the status above full width actions. It holds down to 320px.

### Long files

<Demo name="LongFile" />

`MaxCodeHeight` caps the code area; longer files scroll inside it. `AreShortcutsEnabled="False"` turns the keyboard
shortcuts off (Accept all then shows no hint), and `Model="{x:Null}"` hides the model line.

### The model and the file

`Model` takes an `AgentModel`: its `Name` goes under the title and its `Mark` (a 24 × 24 stroked glyph) into the header
square, in `MarkBrush` or the text color. `FileName` picks the highlighting language from its extension; any language
the library's code highlighter knows is colored, others show in the plain text color. `FileMark` replaces the two
letter language mark with your own content, and `ShowsFileMark="False"` hides it.

### Simulation

For a demo or a prototype without a model, give the card a `CodeSuggestionSimulation` with the file and the hunks to
play, and set the file name, the instruction and the model on the card as usual. While the card has no code and no
hunks of its own it plays the simulation: it reads the file for 0.8s, streams the hunks (three take about 1.6s), then
accepts and rejects them on its own and replays about 2.6s after it finishes. Any pointer, key or focus inside the card
hands control to the person for that run; `Restart()` starts a new run. `AutoPlay="False"` keeps the streaming but
leaves the review to the person. Setting `Code` or `Hunks` ends the simulation. The preview's model is a real one,
credited with a neutral mark; no brand mark is drawn.

```xml
<arc:CodeSuggestion FileName="greet.ts" Instruction="Greet by name">
    <arc:CodeSuggestion.Simulation>
        <arc:CodeSuggestionSimulation Code="export const greet = () =&gt; &quot;Hello&quot;;" />
    </arc:CodeSuggestion.Simulation>
</arc:CodeSuggestion>
```

```csharp
card.Simulation = new CodeSuggestionSimulation
{
    Code = recordedFile,
    Hunks = [new CodeSuggestionHunk("greet", 0, 1, "export const greet = (name: string) => `Hello, ${name}`;")],
};
```

### Your own words

`Labels` holds every word the card shows or says. Replace the object to localize or reword:

```csharp
card.Labels = new CodeSuggestionLabels
{
    Accept = "Übernehmen",
    Reject = "Verwerfen",
    ToReview = count => $"{count} Änderungen offen",
};
```

### Reduced motion

With reduced motion the card opens on the finished suggestion with no streaming and no autoplay, and folds happen at
once. Decisions still apply and the status still updates.

### Content guidelines

- Give each hunk a summary of two to six words ("Add retry limits"); it names the buttons for screen readers.
- Keep hunks sorted and non overlapping. Map a unified diff with `Start` as the zero based line of the first removed
  line (or the insert position), `Remove` as the removed line count and `Insert` as the added lines.
- Say in surrounding copy when content is a sample.

### Best practices

- Write the file in `Resolved`, or progressively in `DecisionMade`; never assume a suggestion was applied.
- Stream by appending to `Insert`; do not rebuild the hunk list on every token.
- Wire `StopRequested` to abort the request.

### Common questions

**Can the person decide while the model is still writing?** No: review bars appear once the status is `Ready`.

**What happens to overlapping hunks?** A hunk that touches lines an earlier hunk already changes is ignored.

## API reference

<!-- api: CodeSuggestion -->

<!-- api: CodeSuggestionHunk -->

<!-- api: CodeSuggestionSimulation -->

<!-- api: CodeSuggestionLabels -->

<!-- api: CodeSuggestionDecisionEventArgs -->

<!-- api: CodeSuggestionResolvedEventArgs -->

## Accessibility

- The card is a labelled group ("Code suggestion: Add retry with backoff") and the code is a focusable region named
  after the file.
- In the code's description, each pending removed or added line starts with "Removed:" or "Added:", so the diff never
  relies on the red and green tints; the visible minus and plus signs carry it for sighted readers too.
- Accept and Reject name their hunk ("Accept Retry 429 and 5xx with backoff"). Accept all exposes its shortcut.
- A polite live region speaks the state ("3 changes to review"), each decision ("Accepted: Add retry limits. 2 changes
  to review") and the result. A simulation adds "Simulated, nothing was written to disk." Streaming is announced once,
  not per line.
- The status always pairs an icon with a label: a spinner for reading and writing, a dot for review, a check for
  applied, a cross for rejected and an alert for errors.
- Focus never falls back to the page when the pressed control leaves with its hunk or state. Pointer focus draws no
  ring; keyboard focus uses Arc's shared focus ring.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves to the code area, then each hunk's Reject and Accept, then the footer actions |
| Enter, Space | Presses the focused Accept, Reject, Accept all, Reject all, Stop or Regenerate; after a hunk is decided, focus moves to the next pending hunk's Accept, or to the code area when none are left |
| Cmd or Ctrl + Enter | Accepts every pending hunk while focus is inside the card |
| Cmd or Ctrl + Backspace | Rejects every pending hunk while focus is inside the card |
| Arrow left, Arrow right | Scrolls long lines when the code area has focus |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Thinking | About 0.8s with a calm shimmer on the label (simulation) | No shimmer, no simulated streaming | At once |
| A hunk opens | Its removed lines tint red over 240ms | At once | At once |
| A new line streams in | Opens to its 20px row in 120ms while it fades in, one every 38ms (simulation) | At once | At once |
| Review bars appear | All at once, on a critically damped spring (0.24s, no bounce) | At once | At once |
| Accept | Removed lines and the bar fold to zero on the settle spring (0.26s) so the code below glides up; the green tint and plus signs fade over 240ms | At once | At once |
| Reject | New lines and the bar fold the same way; the red tint fades | At once | At once |
| Footer status | Icon, label and actions each crossfade in place (out 100ms, in 160ms, the label rises 2px); the icon only swaps when its kind changes | Short crossfade | At once |

Rows are a fixed 20px and folds move between known heights, so nothing is measured mid animation. Each line is laid out
once per text, so streaming a new line never re-highlights the others. The simulation runs on single timers, starts only
when the card is in view and waits while it is scrolled away.

## Related

- `CodeBlock` for code without changes.
- `ToolCall` for agent steps that are not file edits.
- `AgentPlan` for the plan an agent writes before it edits anything.
