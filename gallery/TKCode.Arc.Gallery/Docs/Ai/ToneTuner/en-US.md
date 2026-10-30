---
title: Tone tuner
summary: A brand voice card where two sliders (formality and detail) and voice chips rewrite a customer reply: each changed sentence crossfades in place, new words can be highlighted against the original, and Copy takes the current version.
---

<Demo name="Preview" />

## When to use

`ToneTuner` puts a few dials on an AI rewrite. A customer reply sits in a card with Friendly, Confident and Empathetic
chips and two sliders, formality and detail. Each change asks your model for a new version after a short pause; only the
sentences that changed fade out and back in, the words that are new against the draft can be highlighted, and Copy takes
the version on screen.

- **A composer or help desk reply** that offers AI tone controls before sending.
- **Brand voice tooling** where marketers compare versions of the same copy.
- **Any rewrite** where people would rather steer with a few dials than write a prompt.

## When not to use

- **Free form AI editing with a prompt box**: use a chat or a composer instead.
- **Long documents**: the card is sized for a message of a few paragraphs.
- **Exact line by line code or contract diffs**: use a dedicated diff view.

## Usage

Pass the draft in `Text` and your model call in `Rewrite`. The function gets a `ToneRewriteRequest` with the draft, the
tone (`Formality`, `Detail`, `Voice`) and a ready made `Instructions` string that already asks the model to keep facts,
numbers and names, and a `CancellationToken` that is cancelled when a newer change supersedes the request. Return plain
text with the same paragraph breaks so the sentences line up and only the changed ones animate.

```csharp
tuner.Text = draft;
tuner.RecipientName = "Sofia Ramirez";
tuner.RecipientSubject = "Re: Duplicate charge on INV-20481";
tuner.Rewrite = async (request, token) =>
{
    var reply = await model.CompleteAsync(request.Instructions, request.Text, token);
    return reply.Text;
};
tuner.Rewritten += (_, e) => Save(e.Text, e.Value);
```

With no `Text` and no `Simulation` the card is empty: no header, an empty message, and the voice chips, sliders, Changes
and Copy are turned off until a draft arrives. Without `Rewrite` a draft of your own is rewritten by the same plain rules
a simulation uses (see below), and nothing is sent.

### Simulation

To show the card without a model (a demo, a design review, an onboarding tour), give it a `ToneTunerSimulation`: the
draft, who it is for, the model to name and the versions you wrote for each tone (`ToneTunerVersion`). The card answers
each tone with its version after `Latency` (650ms), says the rewrite is simulated and nothing was sent, and tours the
voices by itself until someone presses or focuses anything inside it (`AutoPlay="False"` keeps it still). A tone without
a version is written by a few plain rules (contractions, greetings, fewer sentences, an opening line). Your own `Text`
wins over the simulation, and setting `Simulation` to null stops it and shows the empty card.

```csharp
tuner.Simulation = new ToneTunerSimulation
{
    Text = draft,
    RecipientName = "Sofia Ramirez",
    RecipientSubject = "Re: Duplicate charge on INV-20481",
    Model = "Claude Sonnet 5.5",
    Versions =
    [
        new ToneTunerVersion(new ToneSettings(2, 1, ToneVoice.Neutral), formalReply),
        new ToneTunerVersion(new ToneSettings(0, 1, ToneVoice.Friendly), friendlyReply),
    ],
};
```

The preview above plays a simulation whose draft and versions are the gallery's own demo data. Keep the sentences a dial
does not touch identical between versions, so only the changed ones crossfade.

### Your model

<Demo name="YourModel" />

The original shows at the starting tone (`ToneSettings.Default`: balanced, standard, neutral). Any other tone waits
`Debounce` (220ms) after the last change and then calls `Rewrite`, so dragging a slider sends at most one request after
it settles; a request still running when the tone changes again is cancelled and its answer ignored. `Rewritten` reports
each landed version with its tone and whether it was simulated.

### Tone

`Value` is the tone, two-way: `new ToneSettings(formality, detail, voice)` with levels 0 to 2. `ValueChanged` runs on every
slider step and chip press, before the debounce. A chip sets its voice and moves both sliders to its starting point;
pressing it again returns to the neutral voice. Replace the chips with `Presets`:

```csharp
tuner.Presets =
[
    new TonePreset { Voice = ToneVoice.Friendly, Label = "Warm", Icon = ArcIcons.Smile, Formality = 0, Detail = 1 },
    new TonePreset { Voice = ToneVoice.Confident, Label = "Direct", Icon = ArcIcons.Zap, Formality = 1, Detail = 0 },
];
```

### Changes

`ShowChanges` (two-way, the Changes switch) highlights the words that differ from the original draft with a rounded
accent background, and the header counts them. `ChangedWords` and `DisplayedText` read the same numbers and text from
code. Keep the original stable while the person tunes: the comparison is always against `Text`.

### Failures

<Demo name="Failure" />

When `Rewrite` throws, the text on screen stays, the status says "Couldn't rewrite" with a Retry button, and the live
region reads the error's message. `Retry()` runs the latest tone again from code.

### Narrow

<Demo name="Narrow" />

The card is fluid up to 640px. Under 520px the sliders stack, the chips share one row in equal columns and the status
hides its settings. Under 400px the chip icons, the changed words count and Copy's label drop so nothing clips; it holds at
360px.

### Your own words

`Labels` holds every word the card shows or says, including the level names the sliders speak:

```csharp
tuner.Labels = new ToneTunerLabels
{
    Formality = "Ton",
    FormalityLevels = ["Familier", "Neutre", "Soutenu"],
    Rewriting = model => $"Réécriture avec {model}",
};
```

### Best practices

- Send `Instructions` to the model as the system or user instruction; it already asks to keep facts, numbers and names.
- Return the same paragraph breaks as the draft, so unchanged sentences stay still.
- Name the model in `Model` so the status says who is rewriting.

## API reference

<!-- api: ToneTuner -->

<!-- api: ToneSettings -->

<!-- api: TonePreset -->

<!-- api: ToneRewriteRequest -->

<!-- api: ToneTunerSimulation -->

<!-- api: ToneTunerVersion -->

<!-- api: ToneTunerLabels -->

## Accessibility

- The voice chips are toggle buttons that report pressed, inside a group named Voice.
- Each slider is named (Formality, Detail) and speaks its level name ("Formal"), not a number.
- A polite live region announces each landed rewrite with its settings, says when the rewrite is simulated (nothing was
  sent), and reports failures.
- Highlighted words are counted in the header ("12 words changed"), so changes are not shown by color alone.
- The status pairs an icon with a short label: Original draft, Rewriting, Rewritten, or Couldn't rewrite with Retry.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the voice chips, both sliders, Retry when shown, the Changes switch and Copy |
| Enter, Space | Presses a voice chip, flips Changes, retries or copies |
| Left, Right arrow | Steps a focused slider between its three levels |
| Home, End | Jumps a focused slider to its first or last level |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A rewrite lands | Each changed sentence fades out over 100ms, all of them swap on the same frame (one reflow, out of sight), the new ones fade in over 160ms; unchanged sentences never animate | Swaps at once | Swaps at once |
| Formality or detail changes | The text area eases to the new reserved height on a bounce free spring | At once | At once |
| Voice changes | Nothing moves: the text area already reserves the tallest voice at this formality and detail | Nothing moves | Nothing moves |
| Status | Flips to Rewriting on the same frame as the input with a calm shimmer, then crossfades to Rewritten | No shimmer, short fade | At once |
| Changes switch | Highlights fade over 160ms | At once | At once |
| Self playing tour | A new voice every 3.2s while on screen, until the first press or focus inside | Off | Off |

## Related

- `Slider` for the formality and detail dials on their own.
- `Switch` for settings that take effect immediately.
- `CopyButton` for copying a value with confirmation.
- `AgentStatus` and `ToolCall` for the rest of an AI conversation.
