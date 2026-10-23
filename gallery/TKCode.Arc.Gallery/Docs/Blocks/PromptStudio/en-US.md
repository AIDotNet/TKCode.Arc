---
title: Prompt studio
summary: A card to edit a prompt template with colored variable chips, try it on saved test cases, and compare and ship versions: version and model menus, variable fields, a Run button and an output panel for the reply.
---

<Demo name="Preview" />

## When to use

The prompt studio is for the people who maintain the prompt behind an AI feature, not for the feature's end users. It puts the whole loop on one card: edit the template, try it on saved test cases, look at what changed
between versions, and promote one to production.

- **An admin or settings page for an AI feature**: the support-reply prompt of a help desk, the summary prompt of a document tool.
- **A review of prompt changes**: compare the open version with another as a word diff before promoting it.
- **A place to try a prompt on real examples**: saved test cases fill the variables, Run shows the reply with its time, tokens and cost.
- **Choosing a model and an effort next to the prompt**: with prices, so the cost of a change is visible.

## When not to use

- **A chat with an assistant**: use `AiChat`.
- **A code or free-text editor**: the template editor knows one kind of token, the variable in double braces.
- **Evaluating a prompt on hundreds of cases**: the card runs one test case at a time.
- **Storing the prompts**: the card edits a draft and tells you; saving, versions and promotion are your code.

## Usage

`PromptStudio` derives from Avalonia's `TemplatedControl`. Give it the `Versions`, the `Variables`, the `TestCases` and the `Models`, and a `RunHandler`; with no handler a built-in simulator answers and the card says it is simulated.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PromptStudio Title="Refund reply" MaxWidth="660" />
</UserControl>
```

```csharp
studio.Versions.AddRange(versions);                 // newest first
studio.Variables.AddRange(["buyer_name", "ticket_id"]);
studio.TestCases.AddRange(testCases);
studio.Models.AddRange(models);
studio.OpenVersion = studio.Versions[0];
studio.ProductionVersion = studio.Versions[0];
studio.RunHandler = async IAsyncEnumerable<string> (request, cancellationToken) => { /* yield text */ };
```

### The template

A template has a system message and a user message (the System and User tabs). A variable is written between double braces, such as `{{buyer_name}}`, and shows in the editor as a colored chip with only its name.
Each variable takes one of four series colors in the order of `Variables`; names found in the text but not listed are added after them.

- Typing two opening braces opens a list of the known variables at the caret: Up and Down move, Enter or Tab insert, Escape closes, and typing filters; the last row adds the name you typed as a new variable. Typing the name and two closing braces also makes a chip, and pasted text with spaces inside the braces is normalized.
- A chip is one unit: the caret skips over it, Backspace or Delete removes it whole, and copying it writes the braces and the name.
- Undo and redo restore chips; typing is grouped into steps by pauses of about a second and by spaces.
- Pressing a chip focuses the variable's field.

`Draft` is the template being edited (two-way) and `TemplateChanged` fires after every edit; save it there. A token estimate for the shown part sits at the top right of the editor; set `TokenEstimator` to use your tokenizer.

### Variables and test cases

Each variable has a field with a dot and a label in its color. Focusing a field highlights its chips in the editor, every keystroke flashes them, and an empty value draws its chips with a dashed outline. Choosing a test case
resets the values to its own; values you typed are discarded. `Values` holds the current values.

<Demo name="Minimal" />

### Running

Run (or Ctrl+Enter, Cmd+Enter on a Mac, anywhere in the card) renders the template with the values and calls `RunHandler`, which returns the reply as a stream of text. The output panel shows "Thinking", then "Writing" with the reply appearing in whole words, then
"Reply" with a footer such as "1.57 s, 245 tokens, $0.0014"; the cost comes from the model's `InputPrice` and `OutputPrice` per million tokens and is left out when it has none. The button turns into Stop while it runs; stopping keeps what was written.
A handler that throws shows an error box with Retry (`RunFailed` lets you word the message). Only one run goes at a time: a new run or Stop cancels the token of the previous one. `RunState`, `LastResult`, `RunStarted` and `RunCompleted` tell the rest.

### Versions

The version pill ("v4, Production") opens the version menu: each version with its author, commit message and age, and two actions on the selected one.

- **Compare** turns the editor into a word diff between the open version and the selected one: inserted words tinted, deleted words struck through, a count such as +17 −1 and a "Back to editing" button; a dot on the tab shows which part changed. `IsCompareMode` and `CompareVersion` are two-way, and Escape leaves.
- **Promote to production** moves the Production marker to the selected version. `ProductionVersion` changes at once and `Promoted` is raised with the version and the previous one; set `Cancel` to refuse, and the studio rolls back. An Undo in the menu swaps them back, raising `Promoted` again with `IsUndo`.

<Demo name="Compare" />

### Model and effort

The model pill opens a menu of `Models` with their prices per million tokens (input and output) and a row of efforts. Choosing a model resets the effort to the model's default and keeps the menu open; prices marked as examples are noted at the bottom.

### Wording

Every word comes from `Labels` (`PromptStudioLabels`): replace the instance to translate or reword; formatted texts are functions. `Now` sets the moment relative times ("last week") are measured from.

### Content guidelines

- **Variable names**: lower case with underscores that say what goes in (`buyer_name`).
- **Test cases**: short labels (three or four words at most); they share one segmented control.
- **Commit messages**: what changed, in a few words.
- **Prices**: say when they are examples.

### Best practices

- Save in `TemplateChanged`, and roll a promotion back with `Cancel` when your server refuses it.
- Pass the cancellation token on in your run handler so Stop really stops.
- Keep test cases that cover the edges of the prompt, such as a missing value.
- Compare before promoting: the diff is the review.

### Common questions

**Why does Run say simulated?** No `RunHandler` is set, so the built-in simulator answered.

**Why is a chip dashed?** Its variable has no value in the current test case.

**Can I use my own variable syntax?** The card uses double braces.

## API reference

<!-- api: PromptStudio -->

<!-- api: PromptVersion -->

<!-- api: PromptAuthor -->

<!-- api: PromptTestCase -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `TemplateChanged` | The template was edited | `Template` |
| `RunStarted` | A run starts | `Request` |
| `RunCompleted` | A run ends: done, stopped or failed | `State`, `Result` |
| `RunFailed` | The handler threw; change `Message` to word the error | `Exception`, `Message` |
| `Promoted` | A version was promoted or a promotion undone; set `Cancel` to roll it back | `Version`, `Previous`, `IsUndo` |
| `VariableAdded` | A variable was added from the editor | |

### Running

| Type | Description |
|---|---|
| `PromptRunHandler` | `(request, cancellationToken)` returns the reply as `IAsyncEnumerable<string>` |
| `PromptRunRequest` | The raw and rendered templates, the values, the model, the effort, the test case id and the version id |
| `PromptRunResult` | The text, the elapsed time, the estimated tokens, the cost and whether it was simulated |
| `PromptTemplate` | The system and the user message |

## Accessibility

- The card is a region named after the prompt. The tabs are a tab list; the variable fields, test cases and output panel are named groups.
- The editor is a text box for assistive technology: a chip reads as its name with the braces, and the list of variables announces how many names match.
- The version and model pills are buttons that report they open a dialog; versions are radio items; the Production version is named as such.
- A polite live region announces that a run started, that the reply is ready, that it was stopped or failed with the reason, that a variable was added, that a version was promoted or a promotion undone, that a comparison opened, and that the reply was copied.
  The streamed words are not announced one by one.
- Status never relies on color alone: dashed chips are also in the missing list, errors have an icon and a sentence, Production has a word.
- Reduced motion keeps every state and announcement.

### Keyboard

| Keys | Action |
|---|---|
| `Ctrl+Enter` / `Cmd+Enter` | Runs the prompt, from anywhere in the card |
| `Tab` | The pills, the tabs, the editor, the variable fields, the test cases, Run, Copy |
| `ArrowLeft` / `ArrowRight` on tabs and test cases | Moves and selects |
| Two opening braces in the editor | Opens the list of variables |
| `ArrowUp` / `ArrowDown` in a list | Moves; wraps around |
| `Enter` or `Tab` in the list | Inserts the variable |
| `Backspace` / `Delete` next to a chip | Removes it whole |
| `Ctrl+Z`, `Ctrl+Shift+Z` | Undo and redo, chips included |
| `Escape` | Closes a list or menu; in the compare view, goes back to editing |

## Motion

- The version and model pills take the width they need on a spring and crossfade their labels; the tabs' highlight and the test cases' highlight glide on a quick spring.
- The Production marker slides between rows of the version menu; menus grow from their pill.
- The output panel follows its content on a spring and keeps its old height while a re-run starts; the reply appears in whole words, each step fading in over 150 ms.
- A chip flashes when its field is edited; the token count rolls its digits.
- Reduced motion drops springs and travel and keeps the cadence of the words without fading; Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Pill labels and widths | Crossfade, width spring | 120 ms crossfade | Immediate |
| Reply words | Whole words, 150 ms fade | Same steps, no fade | Same steps, no fade |
| Output panel height | Spring, held on re-run | Immediate | Immediate |
| Chip flash | Short pulse | None | None |

## Related

- `AiSidePanel` for an assistant beside a page.
- `ModelPicker` for choosing a model on its own.
- `TokenTextEditor` for the editor of tokens the template editor builds on.

| Component | Choose it when |
|---|---|
| `AiSidePanel` | The thing being edited is a page of content |
| `ModelPicker` | Only the model choice is needed |
| `TokenTextEditor` | You need your own tokens in a text editor |
