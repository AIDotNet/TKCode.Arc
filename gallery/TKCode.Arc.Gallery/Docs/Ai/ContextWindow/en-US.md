---
title: Context window
summary: A model's context window as a grid of 200 cells that fills part by part, with a compaction threshold, a legend and actions that show what still fits.
---

<Demo name="Preview" />

## When to use

`ContextWindow` shows what fills a language model's context window and how much room is left. The window is a fixed
budget of 200 cells, so every cell is a two-hundredth of it. The cells fill in reading order: the system prompt, the
tool definitions, files and memory, the summary of earlier messages and the conversation, then free space, with the
part held back for the reply drawn as outlined cells at the end. A dashed line marks where compaction starts. Above the
grid a large number reads the tokens used; beside it a legend lists every part with its tokens and its share. Send
message, Attach file and Compact now change the window, and a status line under them says what happened.

- **Developer tools and agent consoles**: show a session's budget next to the transcript, so people see why a long run
  starts to forget things and what a big attachment costs.
- **Coding agents that compact long sessions**: make compaction visible. The older messages fold into a short summary,
  the cells come back, and the status line says how much was folded.
- **Model choice in context**: switching to a model with a smaller window rearranges the grid at once and shows whether
  the session still fits; when it does not, the card folds the conversation and says so.
- **Documentation and onboarding**: explain context windows, output reserves and compaction with a session that people
  can poke at, like the preview on this page.

## When not to use

- **A simple quota or plan allowance**: use `Progress` or a usage meter. The grid is about what is inside the window,
  not about how much of a plan is spent.
- **Choosing a model**: use a model picker. The pill here only switches between the windows you pass in.
- **Billing or spend over time**: use a chart. The card shows one moment of one session.
- **Many sessions at once**: a table with a small bar per row reads better. One card per screen is the intended use.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ContextWindow>
        <arc:ContextWindow.Models>
            <arc:ContextWindowModel Id="your-model" Name="Your model" Window="200000" Reserve="16000" />
        </arc:ContextWindow.Models>
        <arc:ContextWindowItem Category="System" Label="System prompt" Tokens="3800" />
        <arc:ContextWindowItem Category="Messages" Label="Plan the importer rewrite" Tokens="9400" />
    </arc:ContextWindow>
</UserControl>
```

`ContextWindow` is a `TemplatedControl` in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. The card is at most 744px wide and follows its own width, not the window's.

Without data (no models, items, turns or files) the card is empty: no model pill, every cell free, and no Send message
or Attach file. Give it your models and items, and `Turns` and `Files` for the two buttons. The demos on this page use
real models with the context windows their vendors publish (checked 2026-10-29: Claude Sonnet 5.5 1M, Grok 4.7 500K,
Mistral Medium 3.5 256K) and an invented coding session of 436K tokens; the output reserve is what the demo app asks for
in one reply (32K), never more than the model's published output limit.

### Anatomy

1. **Readout**: the tokens used as a large number, "of 1M tokens" on its baseline, and under them the percentage used
   (a whole number that passes 100 when the window overflows), "used", a dot and the free tokens.
2. **Model pill**: the model's mark, its name, its window in muted figures and a chevron. It opens a menu of the
   models; the selected one has a check. The pill hides when there is no model.
3. **Grid**: 20 columns by 10 rows of square cells in the order system prompt, tools, files and memory, summary,
   conversation, free space and the output reserve. Free cells after the threshold line are a shade lighter.
4. **Threshold line**: a dashed line on the row boundary nearest the threshold (after row 8 for 80%).
5. **Caption**: "Each cell is 5K tokens" on the left and a dashed sample with "Compacts at 80%" on the right.
6. **Legend**: seven rows (System prompt, Tools, Files & memory, Summary, Conversation, Free, Output reserve), each with
   a swatch, its tokens and its share of the window. An empty row shows "No summary yet" or "0" in muted text and no
   share. A stronger divider separates what is used from what is left.
7. **Actions**: Send message (primary), Attach file (secondary) and Compact now (ghost, on the right while there is
   room). Send message and Attach file hide when there is nothing to send or attach.
8. **Status line**: an icon and a sentence: an info circle for plain news, a warning triangle near or past the
   threshold, the shrink glyph while messages fold, and a check circle when a compaction landed.

### How the cells are counted

The window is divided into 200 cells, so a cell holds the window over 200 tokens: 5K on a 1M window, 2.6K on 512K
and 2K on 400K. The parts are laid out by cumulative rounding: the card adds the parts up in order and rounds each
running total to whole cells (halves round up), and a part gets the cells between two such boundaries. Rounding the
running totals keeps the grid honest: the used cells always match the total used, even when every part is small.
The reserve is rounded on its own and counted back from the last cell. When the window overflows, the parts stop at
the reserve and the parts that come later get no cells; the reserve is always drawn.

Token counts read "996" below 1,000, "4.1K" with one decimal below 10K (without a trailing ".0", so "2K"), "509K"
below a million and "1M" or "1.2M" above. Shares read "0.4%" with one decimal below 10% (keeping "1.0%") and "29%"
from 10% on. Each row rounds on its own, so the rows can add up to 99.8% or 100.1%.

### Send, attach and compact

Send message adds the next of `Turns` as a message at the end of the window; Attach file adds the next of `Files` at
the end of the files and memory, and the summary and the conversation move along. Both start over after the last one.
The status line names what was added and how many tokens it took.

From 85% of the threshold (68% of the window at the default 80%) a send or an attachment warns: the status line turns
to a warning and adds that compaction is near. When one reaches the threshold, the card says it passed it and, 540ms
later, folds on its own. Switching to a model whose window the session overfills warns at once and folds after 600ms.

Folding takes 700ms. Every action and the model pill are off while the conversation dims from the oldest message to
the newest. Then every message but the newest two becomes part of one summary (about 6% of the folded tokens, at
least 4K and at most 24K), the summary lands right after the files, and the cells it frees turn back into free space.
A later compaction adds to the same summary; the summary itself is never folded. Compact now does the same on
demand while there are more messages than the newest two.

| Moment | Status | Time |
|---|---|---|
| A send past the threshold | Added “Why does the nightly sync time out?” (+35K) · past 80% | 0 |
| Folding starts | Past 80%: folding 35 earlier messages… | 540ms |
| The summary lands | Folded 608K into a 24K summary | 1,240ms |
| A switch to a smaller model | 436K is over 80% of Grok 4.7 | 0 |
| Folding starts | Too full for Grok 4.7: folding 22 earlier messages… | 600ms |

Files are never folded. When the window is past the threshold and only the newest messages are left, the status line
says there is nothing to fold yet; when a compaction leaves the window past the threshold, it says how much was folded
and suggests removing files. The used tokens can pass the window: the readout then shows more than 100% and no free
tokens.

### Your own session

Pass your models, items, turns and files. Each `ContextWindowModel` has a window and a reserve in tokens, a name and
an optional mark (a 24 × 24 stroked glyph such as an `ArcIcons` member) with its color. Each `ContextWindowItem` has a
category, a label and its tokens; the grid groups items by category, so their order only matters within one.

<Demo name="YourSession" />

```xml
<arc:ContextWindow>
    <arc:ContextWindow.Models>
        <arc:ContextWindowModel Id="heron-2" Name="Heron 2" Window="256000" Reserve="16000" />
    </arc:ContextWindow.Models>
    <arc:ContextWindowItem Category="System" Label="Style guide and instructions" Tokens="2400" />
    <arc:ContextWindowItem Category="Files" Label="handbook/billing.md" Tokens="14600" />
    <arc:ContextWindowItem Category="Messages" Label="Outline the refunds page" Tokens="8200" />
</arc:ContextWindow>
```

```csharp
card.ItemsChanged += (_, e) => log.Add($"{e.Change}: {e.Items.Count} items");
card.Compacted += (_, e) => log.Add($"Folded {e.FoldedCount} messages into {e.SummaryTokens} tokens ({e.Reason})");
```

The card rewrites `Items` as it goes: a send or an attachment inserts an item, a compaction removes the folded
messages and adds to the summary. `ItemsChanged` follows each of these with the new items and what changed them,
`Compacted` reports what was folded, the summary it became and the tokens before and after, and `ModelChanged` follows
the pill. `SelectedModel` is two way. `UsedTokens`, `FreeTokens`, `CanCompact` and `Phase` are read only and bindable.

### Driving it from your app

In a real session the items come from your app. Leave `Turns` and `Files` empty to hide Send message and Attach file,
and add real items with `AddItem`: it inserts the item where its category belongs and runs the same wave, status and
threshold check as the built-in actions. Changes you make to `Items` or to an item's `Tokens` show at once, quietly:
no status line and no automatic compaction.

<Demo name="HostDriven" />

```csharp
// The summary your summarizer will write: here 5% of the folded tokens, at least 2,000.
card.Summarizer = folded => Math.Max(2_000, folded.Sum(item => item.Tokens) / 20);
card.AddItem(new ContextWindowItem(ContextWindowCategory.Messages, "Summarize the open tickets", 9_400));
```

`Summarizer` returns the summary's size for the messages being folded; without one the card uses 6% of them between
4K and 24K. Use `Compacted` to run your own summarization. `AutoCompact` turns the automatic folding off: the status
line still says when the threshold was passed, and Compact now stays available. `KeepRecentMessages` sets how many of
the newest messages survive a compaction (two by default).

### A different threshold

`Threshold` is the share of the window at which compaction runs (0.8 by default). The dashed line sits on the row
boundary nearest to it, so 0.6 draws it after row 6 and 0.75 after row 8, while the check itself uses the exact value.
Warnings start at 85% of the threshold.

<Demo name="Threshold" />

### Narrow containers

The layout follows the card's content width, which is its width less 24px of padding on each side. Up to 640px of
content the legend moves under the grid, the cells tighten (2px apart with a 2px radius instead of 3) and the legend
takes the full width. Up to 420px the model pill moves under the readout. The actions wrap; Compact now moves to the
start of a new line when it no longer fits beside the others, and the status line wraps.

<Demo name="Narrow" />

### Reduced motion

Under reduced motion the cells and the numbers change at once, the status line is replaced without a fade, the
conversation does not dim, and a compaction lands right away. Every status is still announced in order.

<Demo name="ReducedMotion" />

### Content guidelines

Name the parts by what they are in your product, and keep the status sentences short: they are read aloud on every
change. Item labels show only in the status line, quoted for messages, so use a few words.

| Instead of | Write |
|---|---|
| User message #42 | Outline the refunds page |
| /Users/me/projects/docs/handbook/billing.md | handbook/billing.md |
| Context compaction has been completed successfully | Folded 279K into a 17K summary |
| Model gpt-like-large-2025 | Heron 2 |

`Labels` takes a `ContextWindowLabels` object with every word the card shows or announces, including the legend names
and the status sentences. Sentences with numbers are functions, so the word order can change in other languages:

```csharp
card.Labels = new ContextWindowLabels
{
    Messages = "Chat",
    Folded = (folded, summary) => $"{folded} folded into a {summary} summary"
};
```

### Best practices

**Do**

- Feed the real usage of a session: the system prompt, every tool definition, attached files and memory, the summary
  and each turn with the tool results it produced.
- Set the reserve to the largest output you request, so the grid shows the room the reply needs.
- Pass a `Summarizer` that reports the real summary size once your summarizer has run.
- Keep two or three models in the menu, with marks that are generic glyphs rather than vendor logos.

**Avoid**

- Rebuilding `Items` on a timer. Each change animates; update the items that changed instead.
- Presenting an invented session as real usage.
- Using the grid for anything but one window. It always has 200 cells; its proportions only make sense for one budget.

### Common questions

**Why does a small part sometimes have no cells?** Cells are counted from running totals, so a part smaller than half a
cell can round to nothing while the total stays exact. The legend always shows its tokens.

**Why does the summary not shrink?** A compaction adds its summary to the existing one, and summaries are never folded.
Remove or replace the summary item in `Items` when your app rewrites it.

**What happens when I switch to a bigger model?** The grid rearranges, the status line names the new model and window,
and nothing folds.

**Why are Send message and Attach file missing?** They hide when `Turns` or `Files` is empty.

**Can the window overflow?** Yes. The readout passes 100%, free space reads 0, and the parts that do not fit before the
reserve get no cells.

## API reference

<!-- api: ContextWindow -->

<!-- api: ContextWindowModel -->

<!-- api: ContextWindowItem -->

<!-- api: ContextWindowTurn -->

<!-- api: ContextWindowLabels -->

<!-- api: ContextWindowCompactedEventArgs -->

<!-- api: ContextWindowModelChangedEventArgs -->

<!-- api: ContextWindowItemsChangedEventArgs -->

The enums:

| Type | Values |
|---|---|
| `ContextWindowCategory` | `System`, `Tools`, `Files`, `Summary`, `Messages` |
| `ContextWindowPhase` | `Idle`, `Summarizing` |
| `ContextWindowCompactReason` | `Manual`, `Threshold`, `ModelSwitch` |
| `ContextWindowChange` | `Sent`, `Attached`, `Added`, `Compacted` |

## Accessibility

- The card is a group named "Context window". The grid is one image whose name describes it in full: the model, the
  tokens used of the window, every part with its tokens and the threshold. It updates with every change, not with
  every frame of a roll.
- The readout is read as one line ("436K of 1M tokens, 44% used, 532K free"). The legend is a list named "Context
  breakdown" whose rows read their name, tokens and share, so nothing depends on color.
- The pill is named after the model and its window and says that it changes the model. The menu's models are radio
  items with the selected one checked.
- Every send, attachment, model switch and compaction is announced through a polite live region, and every status has
  an icon as well as words.
- While messages fold every action is off. Instead of letting keyboard focus fall to the page, the card keeps it on a
  hidden holder named after the status and hands it back when the compaction lands: to the action that had it, or to
  Send message when that one is off.
- Keyboard focus shows a 2px ring; pointer focus shows none.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the model pill, Send message, Attach file and Compact now |
| `Enter` / `Space` on the pill | Open the model menu on the selected model |
| `Up` / `Down` on the pill | Open the model menu |
| `Up` / `Down` in the menu | Move between the models, wrapping around |
| `Enter` / `Space` in the menu | Choose the model, close the menu and return to the pill |
| `Escape` in the menu | Close the menu and return to the pill |
| `Enter` / `Space` on an action | Send, attach or compact |

## Motion

- New tokens recolor their cells in a short wave from the first changed cell: 9ms per cell for a message added at the
  end, 3ms per cell for a file inserted in the middle and for the cells a compaction frees, never more than 360ms. Each
  cell turns in 240ms on the standard curve, mixing its colors in OKLab.
- A model switch recolors every changed cell in one pass from the first cell, 1.4ms per cell.
- While messages fold, the conversation dims from the oldest message to the newest (each cell to 0.35 and back to 0.5
  over 720ms, at most 6ms apart and 420ms in all). When the summary lands the cells are fully opaque again at once and
  recolor in the 3ms wave.
- The large number, the percentage and the legend's tokens and shares roll to their new values over 400ms on an
  ease-out curve, from whatever they show at that moment, so quick repeated sends never jump backwards. The free tokens,
  the window and the captions change at once.
- The status line crossfades in place: the old line fades out in 100ms while the new one fades in over 160ms and rises
  3px.
- Pointing at a legend row lights its cells and fades the other cells to 0.28 and the other rows to 0.45 in 160ms.
- The model menu grows from the pill on the CSS spring and the chevron turns over with it.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Cells | Recolor in a wave | Change at once | Change at once |
| Numbers | Roll over 400ms | Change at once | Change at once |
| Status line | Crossfades | Replaced | Replaced |
| Folding | Dims for 700ms, then lands | Lands at once | Lands at once |
| Automatic compaction | After 540ms or 600ms | At once | At once |
| Legend hover | Fades in 160ms | At once | At once |

## Related

- `Progress` for a single allowance with a label.
- `PromptCache` for how a shared prompt prefix is reused across requests.
- `AgentLoop` for how an agent gathers context step by step.

| Component | Choose it when |
|---|---|
| `Progress` | One number against one limit is enough |
| `PromptCache` | You explain caching rather than budgeting |
| `AgentLoop` | You explain how an agent works, not what fills its window |
