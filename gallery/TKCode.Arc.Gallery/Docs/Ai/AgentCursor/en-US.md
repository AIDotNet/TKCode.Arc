---
title: Agent cursor
summary: A live document where an AI agent edits like a collaborator: its labeled cursor selects, rewrites and fixes text while a teammate reads along, and the changes stay tinted until someone accepts or undoes them.
---

<Demo name="Preview" />

## When to use

`AgentCursor` shows an AI agent editing a document the way a colleague would. Its labeled cursor comes out of its avatar
in the header, drag-selects a paragraph and streams in a rewrite, adds a heading and fixes a typo, while a teammate's
cursor reads along. Every change stays tinted in the agent's color until someone accepts or undoes it from a floating
review bar. Without data of its own it plays a simulation you give it (in the preview, an invented launch brief, an
agent named after a real model and a teammate), looping.

- **An AI writing assistant** that edits a document next to people, with Accept and Undo.
- **Landing and onboarding sections** that show a collaborative AI editor in motion.
- **A product surface that streams structured edits** from an agent and needs a human review step.

## When not to use

- **Free form chat or questions and answers**: use a chat transcript.
- **A real rich text editor**: use your editor's own collaboration cursors; this card renders read-only blocks.
- **Long documents with many sections**: the card is built for a short excerpt of five to ten blocks.
- **A single text diff**: use a diff view.

## Usage

Give the card the document as `Blocks` (title, heading and paragraph blocks with stable ids) and the edits as `Edits`.
Edits address blocks by id: a rewrite replaces a block's text, an insert adds a block after one, and a fix replaces an
exact word inside a block. The card plays the edits in order and stops in review.

```csharp
card.Blocks =
[
    new AgentCursorBlock("title", AgentCursorBlockKind.Title, "Weekly update"),
    new AgentCursorBlock("shipped", AgentCursorBlockKind.Paragraph, "This week the team shipped the new onboarding flow."),
];
card.Edits =
[
    AgentCursorEdit.Rewrite("shipped", "We shipped the new onboarding flow.", "Making it shorter"),
    AgentCursorEdit.Insert("shipped", AgentCursorBlockKind.Heading, "Next week"),
];
```

With no `Blocks`, `Edits`, `Run` and `Simulation` the card shows an empty document; an agent without a name is called
"Agent" (`AgentCursorLabels.Agent`) and nobody reads along without a `Collaborator`.

### Simulation

To show the card without an agent (a demo, a design review, an onboarding tour), give it an `AgentCursorSimulation`: the
document, the edits, the agent, a teammate and the meta line. While the card has no `Blocks`, `Edits` or `Run` it plays
the simulation, says that nothing was saved and, with full motion, loops. Your own data wins over the simulation, and
setting `Simulation` to null stops it and shows the empty document.

```csharp
card.Simulation = new AgentCursorSimulation
{
    Blocks = [new AgentCursorBlock("summary", AgentCursorBlockKind.Paragraph, "Shared inbox is a new place where teams answer email.")],
    Edits = [AgentCursorEdit.Rewrite("summary", "Shared inbox lets a team answer email together.", "Tightening the summary")],
    Agent = new AgentModel("Claude Opus 5.5", ArcIcons.Sparkles),
    Collaborator = new AgentCursorCollaborator("Maya Ortiz"),
};
```

The preview above plays a simulation whose brief, edits, agent and teammate are the gallery's own demo data.

### Anatomy

1. **Header**: the agent's avatar (its mark on a tint of its color) with the teammate's avatar beside it, a status line
   that says what the agent is doing, and Ask, Stop, Run again or Retry.
2. **Document**: the title, an optional muted `Meta` line, headings and paragraphs.
3. **Agent cursor**: an arrow in the agent's color with its name. It selects, streams and flags typos.
4. **Teammate cursor**: a second arrow with the person's initials and first name, reading along.
5. **Review bar**: floats at the bottom with the count of changes, Undo and Accept changes.

### Your document and edits

<Demo name="YourDocument" />

`AutoPlay="False"` keeps the card still until someone presses Ask (or you call `Ask()`). `Agent` names the agent and
gives it a mark and, through a solid `MarkBrush`, the color of its cursor, selection and change tint; without one they
follow the accent. `Collaborator` is the person reading along; set `IsCollaboratorVisible="False"` to hide them.

Store the document from `Accepted` and pass it back as `Blocks`. The card compares blocks by content, so passing back the
document it just showed changes nothing; a different document replaces the one on screen once no run is using it.

### Fetching edits from your agent

<Demo name="FromYourAgent" />

`Run` takes a function that fetches the edits. The cursor reads the document while it waits. Pressing Stop cancels the
token; a failed task ends in the error state with a Retry button, and its message is announced.

```csharp
card.Run = async cancellation =>
{
    var response = await http.PostAsJsonAsync("/api/agent/edits", document, cancellation);
    return await response.Content.ReadFromJsonAsync<List<AgentCursorEdit>>(cancellation) ?? [];
};
```

### Review, accept and undo

Finished edits stay tinted and the review bar counts them. Accept changes clears the tints and raises `Accepted` with the
edited document; Undo puts the text back, closes inserted blocks and raises `Undone`. Stop keeps finished edits for review
and puts back an edit that was half done. `Phase` and `PhaseChanged` follow the run: Idle, Thinking, Editing, Review,
Accepted, Reverted, Stopped, Empty and Error. `Accept()`, `Undo()` and `Stop()` do the same from code.

A simulation loops: the teammate accepts after a short pause, then the document resets and the run starts over. The pause
never runs out while the pointer is over the card or focus is inside it. Set `Loop` to replay your own run; it never
loops below full motion.

### Narrow containers

<Demo name="Narrow" />

The card is fluid up to 680px. Under 480px the document padding tightens and the review bar drops "by" and the agent's name so it stays
on one line. Text wraps, selections and cursor targets are measured from the live lines, and a cursor's tag moves to the
left of its arrow near the right edge.

### Your own words

`Labels` holds every word the card shows or says. Texts with a name or a count are functions:

```csharp
card.Labels = new AgentCursorLabels
{
    Reading = agent => agent + " lit le document",
    Ready = count => count + " modifications prêtes",
    Accept = "Accepter",
};
```

### Content guidelines

- Keep the excerpt short: five to ten blocks.
- Give each edit a `Description` in a few words ("Tightening the summary"); the status line and screen readers use it.
- Fix edits need the exact word to replace inside the block text.

### Best practices

- Persist the text from `Accepted` and pass it back as `Blocks`.
- Keep block ids stable between runs so edits find their blocks.
- Use `Speed` for a faster demo rather than shortening edits.

### Common questions

**Can people type in the document?** No. The blocks are read-only; wire your editor's own collaboration cursors for
real editing.

**What happens to an edit whose block or word is missing?** It is skipped.

## API reference

<!-- api: AgentCursor -->

<!-- api: AgentCursorBlock -->

<!-- api: AgentCursorEdit -->

<!-- api: AgentCursorCollaborator -->

<!-- api: AgentCursorLabels -->

<!-- api: AgentCursorSimulation -->

<!-- api: AgentCursorDocumentEventArgs -->

<!-- api: AgentCursorPhaseChangedEventArgs -->

## Accessibility

- A polite live region announces each step: reading, the current edit, the edits ready, accepted, undone and errors. A
  simulated run adds "Simulated, nothing was saved".
- Cursors, selections and tints are decorative and hidden from assistive technology; the document stays real text blocks.
- Status never relies on color: every end state pairs an icon with a label, and pending changes are counted in the review
  bar.
- Focus inside the card or a pointer over it holds the looping simulation in review, so nobody loses the bar mid reach.
- Every word is in `AgentCursorLabels`.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves between Ask, Stop, Run again or Retry in the header, then Undo and Accept changes in the review bar |
| Enter, Space | Activates the focused button |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Cursor travel | Glides on a gentle arc with an ease-in-out curve, 280 to 600ms by distance | Jumps | Jumps |
| Cursor appears or leaves | Grows out of the avatar and fades over 200ms | Fades | At once |
| Selection | Fills line by line in step with the drag | Fills line by line | Fills line by line |
| Rewrite | Old text fades out in 110ms, then 3 to 6 words every 34ms, each chunk fading in over 140ms | New text appears whole | New text appears whole |
| Block height | Follows the content on a bounce-free 0.24s spring; inserted blocks grow from zero, then their text fades in | At once | At once |
| Status line | Crossfades, 100ms out and 160ms in, shimmering while the agent works | Short crossfade | At once |
| Review bar | Rises 8px and fades in over 200ms | Fades | At once |
| Loop and teammate drift | Loop after review; the teammate drifts a little while reading | Off | Off |

Playback pauses while the card is off screen. Cursors move by their render transform, so travel never lays anything out.

## Related

- `AgentStatus` for the live status of a whole run.
- `ToolCall` for the tool calls an agent makes.
- `AgentPlan` for the plan an agent writes before it acts.
