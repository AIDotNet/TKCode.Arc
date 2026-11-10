---
title: Eval suite
summary: An evaluation suite for a prompt or a model: test cases run on a baseline and a candidate version, both pass rates with the change in points, every case marked passing, failing, regressed or fixed, filters for the regressions, and an open case with both outputs side by side or as a word diff.
---

<Demo name="Preview" />

## When to use

An eval suite answers one question before a prompt or a model change ships: is the new version better than the one in
use, and what did it break? It is one card. The header picks the two versions to compare, a baseline (what runs today)
and a candidate (the change), and runs the suite. Three tiles give each version's pass rate and the change in points.
Below them, every test case is one row with its status, its input, the kind of check and a verdict for each version.
A case moves in one of four ways: it passes on both, fails on both, **regresses** (passed, now fails) or is **fixed**
(failed, now passes). The filter pills jump to the regressions, and opening a case shows both outputs with the reason
the check gave, side by side or as one word diff. A run queues every case, runs a few at a time, and flips each row to
its result as it lands; it can be stopped at any point.

- **Reviewing a prompt change**: a shorter system prompt, a new tone or a new instruction, checked on the cases that
  matter before it goes live.
- **Comparing models**: the same cases on the current model and a cheaper or newer one.
- **A release gate**: "no regressions" is visible at a glance, and each one can be read and discussed.
- **Keeping history**: results of earlier versions stay in `Results`, so switching the baseline shows an older
  comparison without running again.

## When not to use

- **Head-to-head preference voting** on open-ended answers with no check: use `ModelArena`.
- **Editing and trying a single prompt**: use `PromptStudio`; come here when the change needs to be checked on many cases.
- **Live monitoring of production traffic**: charts over time belong in a dashboard; a suite is a fixed set of cases.
- **Hundreds of metrics per case**: the row shows a status and one verdict per version. Use a `Table` or a `DataGrid` for
  wide numeric results.

## Usage

`EvalSuite` derives from Avalonia's `TemplatedControl`. With no properties it shows an invented sample suite: fourteen
support-reply cases of a made-up bike share, three prompt versions with their results, v12 against v13, and a simulated
run behind **Run suite**.

```xml
<arc:EvalSuite Title="Support replies"
               Cases="{Binding Cases}"
               Versions="{Binding Versions}"
               Results="{Binding Results, Mode=TwoWay}"
               BaselineVersionId="v12"
               CandidateVersionId="v13" />
```

### Anatomy

- **Header**: the title, a line with the case count and where the run stands, the **Run suite** button (it becomes
  **Stop** while running), and the two version choices with an arrow from the baseline to the candidate.
- **Progress**: a thin bar while a run is in progress.
- **Tiles**: the baseline's and the candidate's pass rate with "10 of 14 passed", and the change in points with an arrow
  and "2 regressions · 3 fixed". The change is the difference of the two rounded rates, so the three numbers always agree.
- **Filters**: All, Regressions, Fixed and Failing (the candidate fails: regressions and cases failing on both), each with
  its count.
- **Case list**: a status chip, the case title over its input, the check (hidden when narrow) and a verdict for each
  version: a check mark and "Pass", a cross and "Fail", or a warning and "Error"; rubric cases add their score.
- **Open case**: the title and status, the input with the check, and the two outputs with their verdicts and reasons, or a
  word diff from the baseline output to the candidate output.

### Cases, checks and results

An `EvalCase` has an `Id`, a short `Title`, the `Input`, the `Expected` text and a `Check`:

| Check | Passes when |
|---|---|
| `Exact` | The output, trimmed, equals `Expected` |
| `Contains` | The output contains `Expected`, ignoring case |
| `Regex` | The output matches the pattern in `Expected`, ignoring case |
| `Rubric` | The grader's `Score` (0 to 1) reaches `Threshold` (0.7 by default); `Expected` describes the rubric |

An `EvalResult` belongs to one case and one version (`CaseId`, `VersionId`) and holds the `Output`, `Passed`, the
optional `Score`, a one-line `Detail` (the reason) and the `Duration`. `Results` may hold results for any number of
versions; the suite picks the two it compares. `EvalSuite.Grade(case, versionId, output, score)` applies a case's check
and writes the reason for you.

### Running

**Run suite** (or `RunAsync()`) drops the two versions' old results for these cases, queues every case and runs
`MaxConcurrency` cases at a time (2 by default), each on both versions at once. A row shows Queued, then Running with a
turning glyph, then its status as both results arrive; the counts, the tiles and the filters follow each case.
`Results` is replaced by a new list after every case, so a two-way binding saves the run as it goes. **Stop** (or
`Stop()`) cancels the cases in flight; finished cases keep their results and the rest show "Not run".

Set `RunCaseAsync` to run a case with your own code. Return the graded result; an exception becomes an "Error" verdict
with its message, and the token is cancelled when the run stops or the suite leaves the window.

```csharp
suite.RunCaseAsync = async (evalCase, version, cancellation) =>
{
    var reply = await client.CompleteAsync(version.Id, evalCase.Input, cancellation);
    var score = evalCase.Check == EvalCheckKind.Rubric
        ? await grader.ScoreAsync(evalCase.Expected, reply, cancellation)
        : (double?)null;
    return EvalSuite.Grade(evalCase, version.Id, reply, score);
};
```

<Demo name="CustomRunner" />

`RunStarted`, `CaseCompleted` (with the two results and the status) and `RunCompleted` (with the `Summary`, whether it
was stopped and the time) report a run. `Phase`, `FinishedCount`, `RunTotal` and `Summary` can be read or bound.

### Regressions and the open case

Choose a filter pill, or set `Filter`. Click a row, or press Enter on it, to open the case below the list
(`SelectedCaseId`); click it again, press Escape or the close button to close it. While a case is open, the arrow keys
move to the next case and open it. `DetailMode` switches between **Side by side** and **Word diff**; the diff marks
added words in green and removed words struck through in red, each run with a sign, so it does not rely on color.

<Demo name="Regressions" />

### Wording

All text comes from `Labels` (`EvalSuiteLabels`). Entries ending in `Format` take formatted values in `{0}`, `{1}` …,
so the word order can change. `CultureName` formats the numbers.

```csharp
suite.Labels = new EvalSuiteLabels
{
    Run = "Lancer",
    Stop = "Arrêter",
    PassedOfFormat = "{0} sur {1} réussis",
};
```

### Content guidelines

- **Titles say what the case checks**, not what it asks: "Greets by name", "Quotes the day pass".
- **Prefer the strictest check that is still fair**: `Contains` for a fact, `Regex` for a format, `Rubric` only for qualities
  such as tone or length.
- **Keep reasons short**: the reason line is one sentence ("Missing “30 days”", "Scored 0.62, needs 0.70").
- **Name versions briefly** and put the difference in the note: "v13 · Shorter prompt".

## API reference

<!-- api: EvalSuite -->

<!-- api: EvalCase -->

<!-- api: EvalVersion -->

<!-- api: EvalResult -->

<!-- api: EvalSummary -->

<!-- api: EvalSuiteLabels -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `RunStarted` | A run starts | `Baseline`, `Candidate`, `Cases` |
| `CaseCompleted` | A case has both results | `Case`, `Baseline`, `Candidate`, `Status` |
| `RunCompleted` | A run finishes or is stopped | `Summary`, `WasStopped`, `Finished`, `Elapsed` |

## Accessibility

- The card is a region named by its title. Each tile has one accessible text ("v13: 79% pass rate, 11 of 14 cases";
  "Change: +8 points. 2 regressions · 3 fixed"); the rolling digits themselves are hidden from screen readers.
- The case list is one tab stop. Its name is the row under the keyboard cursor ("2 of 14: Greets by name, Regression.
  Baseline Pass, candidate Fail"), and its help text says how many cases are shown, so moving the cursor reads each row.
- Statuses and verdicts never rely on color alone: each has a glyph (arrow down for a regression, arrow up for a fix,
  check, cross, warning) and a word.
- The subtitle is a polite live region, and a hidden live region announces the start and the end of a run ("Run
  complete. v13 passed 11 of 14, +8 points. 2 regressions.") or where it was stopped.
- The version choices, the filter pills, the detail switch and the close button are Arc controls with names; the outputs
  are selectable text.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Run, the two version choices, the filter pills, the case list, then the open case's switch and close button |
| `ArrowUp` / `ArrowDown` in the list | Previous or next case (opens it while a case is open) |
| `Home` / `End`, `PageUp` / `PageDown` | First or last case, a page up or down |
| `Enter` or `Space` in the list | Opens or closes the case under the cursor |
| `Escape` | Closes the open case and returns to the list |
| `ArrowLeft` / `ArrowRight` on the pills | Previous or next filter |

## Motion

- The pass rates and the change roll to their new values on a spring; the change arrow and its color switch at once.
- A finished case's chip and verdicts fade in over 240 ms, rising 4 px; a running case turns its glyph.
- The open case grows and shrinks on a height spring; switching between side by side and the word diff is immediate.
- The selected filter pill glides between pills.
- Reduced motion keeps the fades without the rise and stops the turning glyph; the height and the numbers jump. Off
  applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Case finishes | Fade and 4 px rise, 240 ms | Fade, 240 ms | Immediate |
| Running case | Glyph turns | Still glyph | Still glyph |
| Pass rates and change | Roll on a spring | Jump | Jump |
| Open or close a case | Height spring | Immediate | Immediate |

## Responsive behavior

- `EvalSuite` changes at its own width, not the window's. The card is at most 960 px wide (`CardMaxWidth`).
- At 620 px or less (`CompactWidth`, `:compact`) the paddings tighten, the three tiles stack, the version choices share the
  width, the check column hides, the verdict columns keep only their marks, and the two outputs of the open case stack.
- The case list scrolls inside the card above 448 px (`ListMaxHeight`) and draws only the rows in view, so long suites
  stay fast. Titles and inputs end with an ellipsis; the open case shows them in full.
- Let the card stretch (`HorizontalAlignment="Stretch"`) and give it 400 px or more.

<Demo name="Narrow" />

## Related

- `PromptStudio` to edit and try one prompt before checking it here.
- `ModelArena` for blind preference voting between two models.
- `ModelPicker` to choose the model a version uses.

| Component | Choose it when |
|---|---|
| `PromptStudio` | One prompt is being written and tried |
| `ModelArena` | People vote on two answers without a fixed check |
| `Table` | Results are many numeric columns per case |
