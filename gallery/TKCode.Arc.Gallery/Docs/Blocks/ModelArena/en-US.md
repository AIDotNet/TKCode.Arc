---
title: Model arena
summary: A blind test of two models: one prompt goes to two anonymous models, both answers stream in at the same pace, and the models are revealed after the vote, with time to first token, speed and cost per answer and a standings list.
---

<Demo name="Preview" />

## When to use

The model arena answers "which of these two is better for us?" with the person's own judgment instead of a benchmark. Both answers appear side by side at the same pace, so speed cannot sway the vote, and the models stay hidden until the vote is in.

- **Choosing a provider or a model version**: run the questions your product really asks and let the team vote.
- **Reviewing a prompt or a model change**: the same prompt, two candidates, a vote and the cost of each answer next to it.
- **Showing latency and price side by side**: each answer reports its time to first token, speed and cost, measured on the real stream.
- **A shared standings list**: wins, ties and losses build up as people vote; save and restore them through `Standings`.

## When not to use

- **A conversation with one assistant**: use `AiChat`.
- **Scoring hundreds of prompts automatically**: the arena is one round at a time, judged by a person.
- **Picking a default model**: use `ModelPicker`.
- **Comparing two versions of a prompt**: use `PromptStudio`, which compares them as a word diff.

## Usage

`ModelArena` derives from Avalonia's `TemplatedControl` and is driven by data: give it the `Contestants`, the sample `Prompts` and a `Runner` that streams one model's answer.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ModelArena x:Name="Arena" MaxWidth="800" />
</UserControl>
```

```csharp
arena.Contestants.AddRange(models);
arena.Prompts.AddRange(samplePrompts);
arena.Runner = new MyArenaRunner();

public sealed class MyArenaRunner : IArenaRunner
{
    public async IAsyncEnumerable<ArenaStreamEvent> RunAsync(
        AiModel model, string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var piece in provider.StreamAsync(model.Id, prompt, cancellationToken))
        {
            yield return new ArenaStreamEvent(piece.Text, piece.InputTokens, piece.OutputTokens);
        }
    }
}
```

### A round

Run (Enter in the box, or Ctrl+Enter, Cmd+Enter on a Mac) sends the prompt to a pair of models picked from `Contestants`. Each answer shows "Thinking" until its first text arrives, then "Writing"; the one that completes first says "Finished first". When both are in, the vote row opens: "A is better", "Tie", "B is better" or "Both bad". The vote reveals the models, adds to the standings and offers "Next round", which picks a new pair, moves to the next sample prompt and runs it.

- **The same pace for both**: the real streams may run at very different speeds, but the text is shown on one shared clock in whole words, so what you see does not depend on which model is faster. The speed and cost figures are measured on the raw stream.
- **Blind**: until the vote, no model name or mark is in the card, its tooltips or its accessible names. The Blind switch can be turned off to see the models at once; votes still count.
- **Stop**: Run turns into Stop while a round runs, and Escape in the prompt row stops it. A pane that had not finished says "Stopped"; one that had finished keeps its answer.
- **Failure**: when a stream fails, that pane keeps what it received and shows an error card with "Retry model A". Retrying runs only that model. Throw `ArenaRunException` with `isRetryable: false` for an error that retrying cannot fix; the card then offers a sample prompt instead.

<Demo name="Programmatic" />

### Metrics

Each pane shows the time to first token (counting up while it waits), the speed in tokens per second and the cost. If the runner reports token counts in `ArenaStreamEvent` they are used; otherwise tokens are estimated at four characters each. The cost multiplies the tokens by the model's `InputPrice` and `OutputPrice` per million tokens, and is zero for a model with no price.

### Standings

The trophy button opens the standings: the models with a record, ranked by win rate (a tie counts as half a win), the top `LeaderboardSize` of them. After a vote the button shows a dot, and the first open afterwards shows the rows moving from their old places to the new ones. `Standings` is a dictionary of `ArenaRecord` by model id: fill it before the first vote to start from saved results, and save it on `VoteCast`.

### Narrow containers

Below 560 wide the two answers become two tabs (each shows the name and the status); below 480 the trophy button keeps only its icon, the keyboard hint hides and the vote buttons form a two-by-two grid. The widths are the card's own, not the window's.

<Demo name="Narrow" />

### Wording

Every word comes from `Labels` (`ModelArenaLabels`): replace the instance to translate or reword; formatted texts are functions.

### Content guidelines

- **Sample prompts**: a short chip label and a full prompt that makes the models differ.
- **Model names**: the name and a short name for the tabs.
- **Prices**: say when they are examples.

### Best practices

- Pass the cancellation token on in your runner, so Stop and a new round really stop the stream.
- Report token counts when your provider gives them; estimates are only a fallback.
- Keep the pair random but not repeated: the default picker avoids the last pair, or set `PairPicker`.
- Save `Standings` when `VoteCast` fires.

### Common questions

**Why do both answers appear at the same speed?** The text is released by one shared clock; the speed shown under each answer is the real one.

**Why does the card say nothing about the models before I vote?** That is the blind test; turn off Blind to see them.

**Can I run it without a runner?** No: with no `Runner` Run is disabled.

## API reference

<!-- api: ModelArena -->

<!-- api: ArenaPrompt -->

<!-- api: ArenaPaneState -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `VoteCast` | The person votes | `Vote` |
| `RoundStarted` | A round starts | `Round`, `Prompt`, `ModelA`, `ModelB` |
| `RunStopped` | A run is stopped before both answers finish | `Round`, `Prompt`, `ModelA`, `ModelB` |
| `PaneFailed` | One answer fails | `Slot`, `Model`, `Error` |
| `LeaderboardReset` | The standings are reset | |

### Streaming

| Type | Description |
|---|---|
| `IArenaRunner` | `RunAsync(model, prompt, cancellationToken)` returns one model's answer as `IAsyncEnumerable<ArenaStreamEvent>` |
| `ArenaStreamEvent` | Some text, the input tokens, the output tokens, or all three; a string converts to one |
| `ArenaRunException` | A failure the runner can describe: the message, an optional title and whether it can be retried |
| `IArenaPairPicker` | Chooses the two models of a round |
| `ArenaVote` | The round, the prompt, both models, the choice, the winner and both answers' metrics |
| `ArenaMetrics` | Time to first token, tokens per second, total time, tokens, cost and whether the tokens were estimated |
| `ArenaRecord` | Wins, ties and losses; `WinRate` counts a tie as half |

## Accessibility

- Each answer is a region named "Model A" or "Model B" (with the model's name after the vote), with its answer area as a focusable, named, scrollable area and its metrics as a named group.
- Before the vote no model name exists anywhere in the accessible tree.
- Status is a word and an icon, never color alone. The tabs are a tab list with selection following focus.
- A polite live region announces that a round started, which answer finished first, that voting is open, the result with both models' names and that the standings changed, a stop, a failure and a reset. The streamed text is not announced word by word.
- The trophy button says "Standings, updated" when there is a result not yet seen.
- Reduced motion keeps every state and announcement.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` in the prompt box | Runs the box's text |
| `Ctrl+Enter` / `Cmd+Enter` | Runs the box's text, also on Run or a chip |
| `Escape` in the prompt row | Stops the run |
| `Tab` | The trophy, Blind, the box, Run, the chips, each answer, the vote buttons |
| `Enter` / `Space` on a vote button | Votes; focus moves to Next round |
| `ArrowLeft` / `ArrowRight` on the tabs | Moves and selects, wrapping around; `Home` and `End` go to the ends |
| `ArrowUp`, `PageUp`, `Home` in an answer | Scrolls up and stops following the stream |
| `Escape` in the standings | Closes them; focus returns to the trophy |

## Motion

- Status words and names swap with a short crossfade and the slot's width follows on a quick spring; the spinner turns once a second and the status text shimmers while a model works.
- New text fades in a few words at a time and the answer follows the end of the text until the person scrolls up.
- The vote row crossfades between its states and its height follows on a spring; the models are revealed with the same crossfade.
- The speed and cost numbers roll their digits. Opening the standings after a vote replays the move: rows slide to their new places and the bars grow or shrink.
- Reduced motion drops springs and travel and keeps the cadence of the words without fading; Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Status, names, vote row | Crossfade, width and height springs | 120 ms crossfade | Immediate |
| Answer text | Whole words, 160 ms fade | Same steps, no fade | Same steps, no fade |
| Standings replay | Rows slide, bars scale | Rows and bars change in place | Immediate |
| Following the stream | Eased each frame | Jumps to the end | Jumps to the end |

## Related

- `AiChat` for a conversation with one assistant.
- `PromptStudio` for editing a prompt and comparing its versions.
- `ModelPicker` for choosing a model on its own.

| Component | Choose it when |
|---|---|
| `AiChat` | One assistant answers |
| `PromptStudio` | The thing being compared is a prompt |
| `ModelPicker` | Only the model choice is needed |
