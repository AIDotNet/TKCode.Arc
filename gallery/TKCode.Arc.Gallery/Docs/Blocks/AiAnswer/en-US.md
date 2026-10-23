---
title: AI answer
summary: A page that answers one question like an article: reasoning, source cards, a streamed answer with headings, lists, a table, code and numbered citations that light the sentence they support, then actions, related questions and a follow-up box.
---

<Demo name="Preview" />

## When to use

The AI answer is for a question that deserves a page, not a chat bubble. It shows how the answer was reached (the reasoning and the sources), writes the answer in full
typography, and lets people check each claim: pointing at a citation shows where the sentence came from.

- **A search or research assistant**: one question in, one cited answer out, with the sources as cards above the text.
- **A help center answer**: the page keeps the question as its title, so it can be shared or copied as a link.
- **An answer that must be verified**: citations, source cards and the sentences they support light each other up, in both directions.
- **An answer that can be improved in place**: try again, shorter, longer or with another model, and page between the versions.

## When not to use

- **A back-and-forth conversation**: use `AiChat`. Asking again here replaces the answer in place.
- **Only a prompt box**: use `AiComposer`.
- **Plain text with no citations**: render it with `Markdown`.
- **Running the model**: the block does not call anything by itself. It asks your `IAiAnswerResponder`, or shows an answer you give it.

## Usage

`AiAnswer` derives from Avalonia's `TemplatedControl` and fills its container, so give it a height. Set a `Responder` and a `Model`, or give it a finished `Answer`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiAnswer Height="720" Question="Which checks should block a merge during the move?">
        <arc:AiAnswer.Models>
            <arc:AiModel Id="lumen-pro" Name="Lumen Pro 4" />
            <arc:AiModel Id="lumen" Name="Lumen 4" />
        </arc:AiAnswer.Models>
    </arc:AiAnswer>
</UserControl>
```

### Answering with a responder

An `IAiAnswerResponder` receives the question, how it is asked for and the model, and returns the answer as a stream of events.

```csharp
public sealed class MyResponder : IAiAnswerResponder
{
    public async IAsyncEnumerable<AiAnswerEvent> RespondAsync(AiAnswerRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AiAnswerSourcesEvent(sources);            // early, so the sources row fills while the model thinks
        yield return new AiAnswerReasoningEvent("The risk is not the new pipeline...");
        yield return new AiAnswerThoughtEvent();                   // the label turns to "Thought for 8s"
        await foreach (var chunk in StreamAnswerAsync(request, cancellationToken))
        {
            yield return new AiAnswerTextEvent(chunk);              // Markdown, of any size
        }
        yield return new AiAnswerDoneEvent(Tokens: 812, Duration: TimeSpan.FromSeconds(18.6), FollowUps: followUps);
    }
}
```

`request.Mode` is `Answer`, `Retry`, `Concise`, `Detailed` or `SwitchModel`, and `PreviousAnswerMarkdown` is the answer being replaced. The token is cancelled when the person stops the
answer, asks something else or leaves. Throwing an exception fails the answer and shows a note with a Try again button.

### A finished answer

Without a responder, set `Question`, `Answer` (Markdown with `[1]` citations), `Sources`, `Reasoning`, `Usage` and `FollowUps`, and the block shows them whole: no reveal, no follow-up box.

<Demo name="Static" />

### Reasoning and sources

While the model thinks, the reasoning row shimmers "Thinking"; when the text starts it reads "Thought for 8s" and opens to the reasoning, which can be opened while it is still being written.
The sources row shows up to four overlapping site marks and "N sources"; opened, the sources are cards in two columns, or one row you scroll sideways when the block is narrow (576 and below).
Each card is a link: it raises `SourceInvoked`, and unless you handle the event its `Url` opens. `IsReasoningExpanded` and `IsSourcesExpanded` are two-way.

<Demo name="Narrow" />

### Citations light the sentence

Point at or focus a citation in the text and the sentence just before it, its source card and the pill itself light up; a preview card with the site, the title, an excerpt and the domain
opens after a moment. Point at a source card instead and every sentence that cites it lights, with its pills. At narrow widths the lit card scrolls into view.

### Versions

Answer again, with Try again, More concise, More detailed or Switch model (the models in `Models` other than the current one, in a submenu), adds a **version** of the answer. A pager ("2 / 2") appears
in the action bar; each version keeps its own model, text, cost, rating and whether it was stopped. `CurrentVersionIndex` is two-way and `Versions` lists them.

### Feedback

Thumbs up records at once. Thumbs down opens a small list of reasons (`FeedbackReasons`, or the defaults from `Labels`); choosing one records the rating and thanks the person, and the list closes by itself.
Pressing a rating again takes it back; the two are exclusive, and each version has its own. `FeedbackChanged` reports the value, the reason, the question and the version.

### Copying and sharing

Copy answer puts the text and its numbered sources on the clipboard. The link button and "Share thread" copy a link that **your code** provides through `ShareLinkProvider`: without one, both buttons are hidden,
because the block never copies a link it made up. `Copied` reports what was copied.

### Asking

The box under the answer asks a follow-up; related questions (`FollowUps`, or the ones the responder sends with `AiAnswerDoneEvent`) ask in place. Asking replaces the page: the question, the title and the answer change, and the previous
versions are dropped. `QuestionAsked` is raised first with its origin, and setting `Cancel` asks nothing. `Stop()` or Escape stops an answer: what was written stays, with a note that it may be incomplete and a Try again button.

<Demo name="Empty" />

### The empty page

With no question and no answer the page shows a heading, a line of explanation and the `Suggestions` as a list; New thread and Share are disabled and the box takes focus.
`NewThread()` and `NewThreadCommand` go back to it.

### Wording

Every word comes from `Labels` (`AiAnswerLabels`): replace the instance to translate or reword; formatted texts are functions. `Title` wins over the question when set.

### Content guidelines

- **Questions**: whole questions, as a person would ask them.
- **Answers**: a lead sentence that answers, then short sections; cite the claim, not the paragraph.
- **Sources**: a title people recognize, the domain, a date and an excerpt of one or two sentences.
- **Reasoning**: a few sentences of what the model is going to do, not a transcript.

### Best practices

- Send the sources early, before the text that cites them, and number them from 1.
- Always pass the cancellation token on, so stopping really stops.
- Provide a real share link or none.
- Keep related questions to three or four that the sources can answer.

### Common questions

**Why is there no conversation?** The block answers one question at a time; use `AiChat` for a dialogue.

**Why does the copy button include sources?** The answer is only worth copying with where it came from.

**Why are the share buttons missing?** `ShareLinkProvider` is not set.

## API reference

<!-- api: AiAnswer -->

<!-- api: AiAnswerVersion -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `QuestionAsked` | A question is about to be answered; set `Cancel` to ask nothing | `Question`, `Origin` |
| `FeedbackChanged` | An answer is rated, a down rating gets its reason, or a rating is taken back | `Value`, `Reason`, `Question`, `Version`, `VersionIndex` |
| `SourceInvoked` | A source card or a citation is pressed; set `Handled` to open it yourself | `Item`, `FromCitation` |
| `Copied` | Something was copied | `Kind` (`Answer`, `ShareLink`, `ThreadLink`, `Code`), `Text` |
| `StatusChanged` | The status changes | `OldStatus`, `NewStatus` |

### The responder

| Type | Description |
|---|---|
| `IAiAnswerResponder` | `RespondAsync(request, cancellationToken)` returns the answer as a stream of events |
| `AiAnswerRequest` | The question, the mode, the model and the answer being replaced |
| `AiAnswerReasoningEvent` | More reasoning text |
| `AiAnswerSourcesEvent` | The sources; send them early |
| `AiAnswerThoughtEvent` | The reasoning is over; the text starts |
| `AiAnswerTextEvent` | More of the Markdown answer, of any size |
| `AiAnswerDoneEvent` | The answer is complete, with its cost and follow-up questions |

## Accessibility

- The block is a region named "AI answer"; the question is a heading that takes focus when a related question or a suggestion asks a new one.
- The reasoning and sources rows are buttons that report whether they are expanded; closed content is removed from the tab order. Source cards are links named "Source 1: title, domain".
- Citations are focusable in reading order, open their card on focus and close it with Escape without moving focus; the lit sentence is a visual aid, so the card carries the same information.
- A polite live region announces "Thinking", "Writing the answer", "Answer ready" or "Answer stopped", failures, copies, ratings, rating changes and version changes. Streamed words are not announced one by one,
  and the answer is marked busy while it is written.
- The action buttons are named and have tooltips; the rating buttons report whether they are pressed; the version pager is a group named "Version 2 of 2".
- Focus goes where the person would look next: back to the answer-again button after its menu, back to the thumbs-down button after the reasons, to the new question after a related question.
- Status never relies on color alone: lit sentences have the card and the number too, failures have an icon and a sentence.
- Reduced motion keeps every state and announcement.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | New thread, Share, the reasoning and sources rows, the citations, the action bar, the related questions, the box |
| `Enter` or `Space` | Presses the focused button or card; on a citation, opens its source |
| `Escape` | Closes a citation card, a menu or the reasons without moving focus; in the box, stops the answer |
| `ArrowDown` / `ArrowUp` in a menu | Moves between entries; letters jump to a matching entry |
| `ArrowRight` / `ArrowLeft` | Opens and closes the "Switch model" list |
| `Enter` in the box | Asks; `Shift+Enter` adds a line |

## Motion

- Streamed text is released in whole words about every 50 ms (about four words a step) and each step fades in over 160 ms; headings, list items, table rows and code lines appear when their whole line has arrived.
- Reasoning and sources open on a spring with their content fading in; the source marks fan out when pointed at.
- Lighting a sentence fades its background in over 160 ms.
- Switching versions crossfades the answer; the height follows on a spring, and the digits of the pager roll.
- Menus and the reasons list grow from their button.
- Reduced motion keeps the cadence of the words but drops the fade, the springs and the lighting transition; Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| New words | Whole words every 50 ms, 160 ms fade | Same batches, no fade | Same batches, no fade |
| Reasoning and sources | Height spring, fades | 120 ms fades | Immediate |
| Lit sentence and card | 160 ms | Immediate | Immediate |
| Version switch | Crossfade; the height springs | 120 ms crossfade | Immediate |
| Menus | Grow from the anchor | 120 ms fade | Immediate |

## Related

- `AiChat` for a conversation.
- `AiComposer` for the prompt box.
- `Markdown` for the text of an answer on its own.
- `ModelPicker` for choosing the model elsewhere.

| Component | Choose it when |
|---|---|
| `AiChat` | People go back and forth |
| `AiComposer` | You only need the box |
| `Markdown` | You only render text |
| `ModelPicker` | The model choice lives on a settings page |
