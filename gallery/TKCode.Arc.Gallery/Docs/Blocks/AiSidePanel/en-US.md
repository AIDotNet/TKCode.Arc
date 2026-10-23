---
title: AI side panel
summary: An assistant beside a page the person reads: it reads the whole page, answers about it or about a selection, and proposes changes as a word diff that the person applies, discards or undoes, with a sweep on the page and an Undo pill.
---

<Demo name="Preview" />

## When to use

The AI side panel is for a page that people read and sometimes change: a specification, a record, a knowledge base article. The assistant sits next to the page, reads all of it, and
when the page should change it does not rewrite it behind the person's back: it shows the change as a card, and the person decides.

- **A design note or specification**: "Shorten the summary" returns a card with the new text as a word diff; Apply writes it into the page.
- **A record or settings page with tables**: ask for a new column and see it as a small table with the new cells tinted before anything changes.
- **A help page where people ask about a passage**: select text, press "Ask about selection", and the question carries that passage as context.
- **A page that must stay in the person's hands**: every change can be undone, and nothing is applied without Apply.

## When not to use

- **A conversation with no page**: use `AiChat`.
- **Rewriting one piece of text in place**: an inline edit control suits it better; the panel works on a page of blocks.
- **A page that is not made of headings, paragraphs and tables**: the panel renders those three kinds of block. Show richer pages yourself and use the panel's thread beside them.
- **Running the model**: the panel asks your `IAiPanelResponder`; it does not call anything by itself.

## Usage

`AiSidePanel` derives from Avalonia's `TemplatedControl` and fills its container, so give it a height. Give it a `Page`, a `Responder` and the `Suggestions` for the empty thread.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiSidePanel Height="640" AssistantName="Helper" ModelName="Model X1" PanelWidth="360" />
</UserControl>
```

```csharp
var page = new AiPanelPage { Title = "Offline sync", Meta = "Design note, updated Mar 3" };
page.Blocks.Add(new AiPanelParagraph { Id = "summary", Label = "Summary", Text = "Offline sync is a planned change..." });
panel.Page = page;
panel.Responder = new MyResponder();
panel.ChangeApplied += (_, e) => Save(e.Page);
```

### The page

An `AiPanelPage` has a title, a line of meta text and `Blocks`: an `AiPanelHeading`, an `AiPanelParagraph` or an `AiPanelTable` (columns and rows). Every block has an `Id` and a `Label`
("Summary", "Storage table"); the label names the block on the selection pill, the change card and the placeholder, and to screen readers. The panel works on the page object you give it: when a change is applied the block in the
page is replaced, so the page you hold is always current. Replacing `Page` resets the thread and the undo.

### Answering

A `IAiPanelResponder` receives the question, the page as it is now (with every applied change) and the selection, and returns an `AiPanelReply`: text, and optionally a change.

```csharp
public async ValueTask<AiPanelReply> RespondAsync(AiPanelRequest request, CancellationToken cancellationToken)
{
    var summary = (AiPanelParagraph)request.Page.Find("summary")!;
    return AiPanelReply.FromText(
        "Here is a shorter summary.",
        new AiPanelChangeProposal("summary", new AiPanelParagraph { Id = "summary", Label = "Summary", Text = shorter }));
}
```

While your method runs, the panel shows "Reading the page"; the text then arrives in whole words about every 50 ms (four to seven words a step, faster to catch up on a long backlog). `AiPanelReply.FromText` takes a whole
text and the panel cuts it into words itself; pass an `IAsyncEnumerable<string>` for a real stream. Stopping cancels the token and keeps what was shown, with the note "Stopped" and no card. An exception shows an error card with
Retry, which asks the same question again.

### Changes

A reply may propose a change to **one block**: the id and the whole new block. The panel shows a card with a header (the block's label and a summary such as "21 words, was 45" or "Adds Sync column"), the change as a word diff
(deleted words struck through, inserted words tinted; a table shows the new table with its new cells tinted) and the buttons Discard and Apply.

- **Apply** writes the change into the page. The block on the page sweeps with a tint from left to right while the old text fades out and the new text fades in and the block's height springs to its new size; an Undo pill ("Summary updated, Undo")
  appears above the box and focus moves to it. `ChangeApplied` is raised with the updated page; save it there.
- **Undo** restores the block with the same sweep and the card reads "Undone"; it can be applied again. Only the **last** applied change can be undone, and the pill stays until it is.
- **Discard** dims the card and ends it; it can no longer be applied.

Several cards can wait at once and be applied in any order.

<Demo name="Programmatic" />

### Selection

Drag across text on the page and a button "Ask about selection" appears under the selection. Pressing it clears the selection, adds a pill "Selection: Summary" to the header and marks the block with a line on its left; the box takes
focus with a placeholder about that block. The pill stays for the next questions until its × removes it. A selection belongs to the block where it starts; a table selection is labeled with the table. `SelectionAsked` reports it, and
`ClearSelection()` removes it from code.

### Open, close and narrow containers

`IsOpen` (two-way), `Open()` and `Close()` show and hide the assistant. Docked, its width springs between `PanelWidth` (360) and zero and the page column re-centers; closed, a pill with the assistant's name sits at the top right of the page to reopen it.
Escape inside the panel closes it and returns focus to that pill; opening puts focus in the box. In a container narrower than `SheetBreakpoint` (640) the assistant is a bottom sheet over the page (`SheetHeightRatio`, 74%): the page dims and is inert
while it is open, and pressing the dimmed page or the close button closes it.

<Demo name="Narrow" />

### Wording

Every word comes from `Labels` (`AiSidePanelLabels`): replace the instance to translate or reword; formatted texts are functions. `AssistantName` and `ModelName` fill the header; `AssistantMark` replaces the spark icon (a geometry or an image).

### Content guidelines

- **Block labels**: short nouns the person would say ("Summary", "Pricing table").
- **Suggestions**: three at most, each something the page lets the assistant do ("Shorten the summary").
- **Change summaries**: let the panel generate them; they are counts, not prose.
- **Answers**: say what changed in a sentence; the card shows how.

### Best practices

- Save the page in `ChangeApplied` and `ChangeUndone`, not on every answer.
- Give blocks stable ids; a change refers to its block by id.
- Propose one block per answer; the person can ask again for the next.
- Keep the responder fast to start: the first feedback is "Reading the page".

### Common questions

**Why did Apply not change my data?** It changes the `Page` object you gave the panel; save it in `ChangeApplied`.

**Why can I only undo the last change?** The Undo pill belongs to the latest applied change; applying another replaces it, and earlier cards still say "Applied".

**Can the assistant edit several blocks at once?** One block per card; ask again for the next.

## API reference

<!-- api: AiSidePanel -->

<!-- api: AiPanelPage -->

<!-- api: AiPanelHeading -->

<!-- api: AiPanelParagraph -->

<!-- api: AiPanelTable -->

<!-- api: AiPanelTurn -->

<!-- api: AiPanelChange -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `ChangeApplied` | A change was applied and the page data updated | `Change`, `Page` |
| `ChangeUndone` | A change was undone | `Change`, `Page` |
| `ChangeDiscarded` | A change was discarded | `Change`, `Page` |
| `SelectionAsked` | A selection became the context of the next question | `Selection` |

### The responder

| Type | Description |
|---|---|
| `IAiPanelResponder` | `RespondAsync(request, cancellationToken)` returns an `AiPanelReply` |
| `AiPanelRequest` | The prompt, the page as it is now and the selection, if any |
| `AiPanelReply` | The answer text (whole through `FromText`, or a stream) and an optional change |
| `AiPanelChangeProposal` | The id of the block and the whole new block |

## Accessibility

- The panel is a region named after the assistant; the page and the thread are separate regions. Each page block is named by its label.
- The header's context pill says what the assistant reads; the selection pill is a button named "Remove selection" with the selected text as its description.
- A polite live region announces that the assistant is reading the page, that an answer is ready, stopped or failed, that a change was suggested, applied, undone or discarded, that a selection was added, and that the panel opened or closed.
  Streamed words are not announced one by one.
- The change card's buttons are named and report their state; an applied card is not a button.
- Focus goes where the person would look next: to the Undo pill after Apply, back to that card's Apply after Undo, into the box after Discard or opening the panel, to the reopen pill after closing it.
- In sheet mode the page below is inert while the sheet is open.
- Reduced motion keeps every state and announcement.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Page controls, the header, the thread (cards, Retry), the Undo pill, the box |
| `Enter` or `Space` | Presses the focused button |
| `Enter` in the box | Sends; `Escape` while an answer is written stops it |
| `Escape` elsewhere in the panel | Closes the assistant and focuses the reopen pill |

## Motion

- The docked panel's width springs between open and closed; the panel body keeps its width and slides with the edge while the page re-centers.
- Streamed words appear in steps of four to seven every 50 ms and each step fades in over 150 ms.
- Applying a change sweeps a tint across the block (about 420 ms), holds, and fades it (about 630 ms); the old text fades out in 100 ms and the new text fades in after 80 ms over 180 ms, and the height springs.
- The ask button, the Undo pill and the reopen pill fade in and out.
- The bottom sheet slides and fades in.
- Reduced motion drops springs and travel: the sweep becomes a tint fade over the whole block, the sheet fades in place and words keep their cadence without fading. Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Open and close | Width spring | 120 ms fade | Immediate |
| Apply or Undo | Sweep, crossfade, height spring | Whole-block tint fade, no travel | Immediate |
| New words | Steps of whole words, 150 ms fade | Same steps, no fade | Same steps, no fade |
| Sheet | Slide and fade | Fade in place | Immediate |

## Related

- `AiChat` for a conversation without a page.
- `AiComposer` for the prompt box on its own.
- `PromptStudio` for editing prompts rather than pages.

| Component | Choose it when |
|---|---|
| `AiChat` | There is no page to read or change |
| `AiComposer` | You only need the box |
| `PromptStudio` | The thing being edited is a prompt template |
