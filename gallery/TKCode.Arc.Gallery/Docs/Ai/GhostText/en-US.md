---
title: Ghost text
summary: An inline AI autocomplete where muted ghost text continues the sentence at the caret: Tab accepts it in place, Cmd+Right takes one word, Escape dismisses it, and a calm chip shows the model behind it.
---

<Demo name="Preview" />

## When to use

`GhostText` is a text field with AI help that stays out of the way. When the person pauses, muted ghost text continues
their sentence exactly at the caret. Tab turns it solid in place, Cmd+Right takes one word at a time, Escape fades it
away, and the text never changes without an explicit key press. A small chip at the end shows the model behind it.

- **Long-form fields people write in your product**: pull request descriptions, emails, support replies, issue bodies,
  release notes, CRM notes.
- **Short single-line fields** where a likely completion saves typing, such as an email subject or a commit message.
- **Anywhere AI help should wait for a pause** and only act on a key press.

## When not to use

- **A chat prompt box** that sends a message to an assistant: use a composer.
- **Code edits** with accept and reject per hunk: use a code suggestion view.
- **Picking from known options**: use a combobox or a command menu.
- **Sensitive fields** (passwords, card numbers, health data), unless your model provider and data policy allow it.

## Usage

Give the field a `Suggest` function. It receives the whole text (the caret is always at the end when it is called) and a
cancellation token, and returns a `GhostTextResponse`: a string, a `Task<string?>`, or a stream of chunks wrapped with
`GhostTextResponse.FromStream`. Return `null` to skip a text without showing Thinking.

```csharp
field.Suggest = (text, cancellationToken) =>
    GhostTextResponse.FromStream(StreamCompletion(text, cancellationToken));

async IAsyncEnumerable<string> StreamCompletion(string text,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    using var response = await http.PostAsJsonAsync("/api/autocomplete", new { text }, cancellationToken);
    response.EnsureSuccessStatusCode();
    using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));
    var buffer = new char[256];
    int read;
    while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
    {
        yield return new string(buffer, 0, read);
    }
}
```

`Text` is two-way. The field asks after `Delay` (450ms) of quiet, once the trimmed text has `MinLength` (3) characters.
Every keystroke restarts the pause and cancels the request in flight, so a fast typist triggers nothing until they stop.
With no `Suggest` and no text, the field is a plain text field, or plays the simulation you give it.

### Streaming suggestions

<Demo name="Streaming" />

Each streamed chunk appends to the ghost and fades in on its own. `Label` adds a quiet row above the text and `Footer`
content at the start of the footer row. `Rows` (3) is the minimum height in lines and `MaxRows` (10) the height at which
the field stops growing and scrolls. `Model` names the model and gives its 12px mark; pass your own glyph and color with
`AgentModel`.

For a demo or a prototype without a model, give the field a `GhostTextSimulation` with a `Script` of lines: it types
each line, lets its `Reply` stream in 3 to 6 word chunks after a short think and accepts it, alternating Tab with a word
at a time, then starts over. Clicking the field hands it to the person, who gets the scripted replies and, for anything
else, the simulation's own `Suggest` (the preview passes a small rule based stand-in that completes words and fails on
"timeout"). Label, placeholder, footer and model stay on the field. Setting `Suggest` or `Text` ends the simulation.

```csharp
field.Label = "Commit message";
field.Simulation = new GhostTextSimulation
{
    Script = [new GhostTextSimulationLine("Bump the parser", " to fix the date bug.")],
};
```

### Single line

<Demo name="SingleLine" />

`IsMultiline="False"` makes a single-line field that scrolls sideways with the caret and keeps the chip inline at its
end. Line breaks typed or pasted into it become spaces.

### Plain

<Demo name="Plain" />

`Variant="Plain"` drops the border, background and focus ring so the text sits flush inside a card you already have.
Give that card its own focus style.

### Events and methods

<Demo name="Events" />

`Accepted` reports the accepted text and whether it was the whole suggestion or one word; `Dismissed` reports what was
dismissed with Escape. Log both to measure acceptance per model and prompt. `Status`, `Suggestion` and `IsStreaming`
describe the suggestion at any moment, and `StatusChanged` reports each change.

`AcceptAll()`, `AcceptWord()`, `Dismiss()`, `Request()` (ask now, without waiting for a pause) and `FocusField()` drive
the field from your own buttons or tests.

### States

| Status | Chip reads | The text |
|---|---|---|
| `Idle` | The model's mark and name | Your text only |
| `Loading` | A shimmering "Thinking" | Your text only |
| `Ready` | A Tab key cap and "to accept" ("Tap to accept" on touch screens) | Your text, then the ghost |
| `Error` | An alert, "No suggestion" and "Retry" | Your text only |

After Tab the chip reads "Accepted" with a check for a moment, then the model's name again.

### Writing suggestions

- Prompt for a continuation only: no preamble and no repetition of the input.
- Start with a space when the text ends mid-sentence without one; the field strips a leading space when the text already
  ends with whitespace.
- Keep it to a sentence or a short line, and use a fast model: people expect ghost text within about half a second.
- Return `null` for contexts you never want to complete (inside a code block, after a URL), so Thinking never flashes.

### Your own words

`Labels` holds every word the field shows or says. Replace the object to localize or reword:

```csharp
field.Labels = new GhostTextLabels
{
    Thinking = "Réflexion",
    ToAccept = "pour accepter",
    SuggestionAnnouncement = suggestion => $"Suggestion : {suggestion}. Tab pour accepter.",
};
```

### Common questions

**Does typing over the ghost ask again?** No. Characters that match the start of the ghost consume it in place;
anything else clears it and a new suggestion is asked for after the next pause.

**Why does the ghost disappear when I click?** Suggestions only continue from the end of the text. Moving the caret away
clears the suggestion; it comes back after the next pause at the end.

## API reference

<!-- api: GhostText -->

<!-- api: GhostTextResponse -->

<!-- api: GhostTextLabels -->

<!-- api: GhostTextSimulation -->

<!-- api: GhostTextSimulationLine -->

## Accessibility

- The field is a native text box, named by `AutomationProperties.Name` or a string `Label`, and described by a sentence
  that explains Tab, Command or Control plus Right Arrow, and Escape (`IsHintVisible` turns it off with the chip).
- The ghost is part of how the text is drawn, never part of the text, so it is not read as content. A polite live region
  reads the finished suggestion ("Suggestion: date the plan changed. Press Tab to accept."), then Suggestion accepted,
  Suggestion dismissed or Couldn't get a suggestion. `Notice` adds a one-off message to the same region.
- The chip is a button with a clear name in each state (Accept suggestion, Retry suggestion). Tab reaches it only in the
  error state, where Retry is its action; in the ready state Tab itself accepts. Pressing it never takes focus from the
  field.
- State is never shown by color alone: Thinking, the Tab key cap with "to accept", Accepted with a check and No
  suggestion with an alert icon all carry words.
- Keyboard focus draws Arc's focus ring around the whole field; pointer focus does not.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Accepts the whole suggestion and keeps the caret at the end. With no suggestion showing, Tab moves focus as usual |
| Cmd+Right, Ctrl+Right | Accepts the next word; the rest stays. Option+Right and Alt+Right work too. With no suggestion showing, the keys move the caret as usual |
| Escape | Dismisses the suggestion. With no suggestion showing, Escape is left alone, so a surrounding dialog can still close |
| Typing | Characters that match the start of the ghost consume it in place; anything else clears it |
| Arrow keys, click | Moving the caret away from the end clears the suggestion |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A chunk streams in | Fades in over 140ms, opacity only | At once | At once |
| Escape, leaving the field | The ghost fades out over 110ms | At once | At once |
| Typing a different character | The ghost goes at once | At once | At once |
| Tab, Cmd+Right | The accepted text eases from ghost ink to text ink over 200ms in place; the rest of the ghost never fades again | At once | At once |
| The text grows or shrinks | The height follows on a 180ms tween, clipped while it moves, never bouncing | At once | At once |
| Chip state | Old content out in 100ms, new in 160ms, while the width follows on a 200ms tween anchored to its right edge | Short crossfade | At once |
| Key caps | Press for 160ms when their key or method fires | At once | At once |
| Thinking | A calm shimmer | Still | Still |

The simulation loops with full motion; with reduced motion or none it plays once and stops. It pauses while off screen.

Below 520px of width the word and Escape hints step aside and only the chip remains, and a footer note truncates with an
ellipsis.

## Related

- `Textarea` and `Input` for plain text fields.
- `MentionInput` for @ mentions inside a message.
- `TextShimmer` for the calm light of the Thinking state.
- `ToolCall` and `AgentStatus` for the rest of an AI reply.
