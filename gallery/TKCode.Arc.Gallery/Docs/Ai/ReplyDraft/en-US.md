---
title: Reply draft
summary: An AI reply drafter under a received email: quick replies steer a fresh streaming draft, tone and length rewrite it with one crossfade, and the reader edits it before Insert.
---

<Demo name="Preview" />

## When to use

`ReplyDraft` shows a received email with an AI reply under it. The model streams a draft in quick, softly fading chunks;
quick reply chips steer a fresh draft, the tone and length groups rewrite the finished one with a single crossfade, and
the reader can fix a word in place before pressing Insert. Insert hands the text, with the reader's edits, to your
composer: the card never sends anything itself.

- **Drafting a reply** to an email, ticket or message with a model, where a person reviews and edits before sending.
- **Offering intents plus tone and length** for generated text in an inbox, support desk or CRM.
- **Showing an AI writing feature** on a landing page with a realistic example that plays by itself.

## When not to use

- **Free-form chat with a model**: use a chat thread and composer; the draft here answers one message.
- **Inline autocomplete while typing**: use ghost text at the caret instead of a separate draft box.
- **Sending the email**: this card inserts a draft; your composer owns sending.

## Usage

Give the card the message, your quick replies and a generator that streams your model's reply. Every new action (a chip,
a tone or length change, Regenerate) cancels the previous request's token, so pass it on to your client.

```csharp
draft.Message = new ReplyDraftMessage
{
    FromName = email.From.Name,
    FromEmail = email.From.Address,
    Subject = email.Subject,
    Body = email.PlainText,          // blank lines split paragraphs
    ReceivedAt = email.Received,
};
draft.Suggestions = [new ReplyDraftSuggestion("yes", "Sounds good"), new ReplyDraftSuggestion("later", "Next week")];
draft.Model = new AgentModel("Mistral Medium 3.5");
draft.Generator = request => replies.StreamAsync(email.Id, request.Tone, request.Length,
    request.Suggestion?.Id, request.Mode, request.Previous, request.CancellationToken);
draft.InsertHandler = async args => await composer.InsertAsync(args.Draft);
```

A message of yours without a `Generator` cannot be drafted: the draft box shows its line with Draft reply turned off. With
no `Message` and no `Simulation` the card is empty: a blank email, no quick replies, "No reply drafted yet." and Draft
reply turned off.

### Simulation

To show the card without a model (a demo, a design review, an onboarding tour), give it a `ReplyDraftSimulation`: the
email, the quick replies, the model to credit and the replies you wrote for each quick reply, tone and length
(`ReplyDraftVersion`). The card streams the matching reply after a short read, says it is simulated and nothing was
sent, and tours tones, length, a second quick reply and Insert on a loop until someone presses or types inside it. Your
own `Message` wins over the simulation, and setting `Simulation` to null stops it and shows the empty card. Leave
`AutoPlay` off in products.

```csharp
draft.Simulation = new ReplyDraftSimulation
{
    Message = email,
    Suggestions = [new ReplyDraftSuggestion("yes", "Sounds good")],
    Model = new AgentModel("Mistral Medium 3.5"),
    Replies =
    [
        new ReplyDraftVersion("yes", ReplyDraftTone.Friendly, ReplyDraftLength.Medium, "Hi Maya,\n\nSounds good!\n\nSam"),
        new ReplyDraftVersion("yes", ReplyDraftTone.Formal, ReplyDraftLength.Medium, "Dear Maya,\n\nThat suits me.\n\nSam"),
    ],
};
```

`ReplyDraftSimulation.Generate` streams the same replies as a `Generator`, for when you wrap the simulation yourself. The
preview above plays a simulation whose email and replies are the gallery's own demo data.

### Anatomy

1. **Email**: a mail glyph, the subject and the time; the sender's avatar, name, address and recipient; the body,
   indented under the avatar.
2. **Reply heading**: the model's mark and "Reply with ..." with the status at the right: an icon and a word, which
   shimmers while the model works.
3. **Quick replies**: toggle chips; the one steering the draft is filled with the accent.
4. **Draft box**: the draft streams in here and becomes editable text when it is done.
5. **Tone and length**: two small radio groups with a gliding pill, separated by a hairline.
6. **Actions**: the keyboard hint, Regenerate and Insert.

### States

| Status | The draft box shows | Status line |
|---|---|---|
| `Idle` | A shimmering "Reading Maya's email" line, or Draft reply when `AutoStart` is off | "No draft yet" |
| `Drafting` | The draft streaming in | A shimmering "Drafting" |
| `Rewriting` | The old draft, dimmed and read-only | A shimmering "Rewriting" |
| `Ready` | The editable draft | A check and "Ready to edit" |
| `Inserting` | The draft; Insert shows a spinner | A shimmering "Inserting" |
| `Inserted` | The draft | A double check and "Inserted" |
| `Failed` | The error with Try again | An alert and "Couldn't draft" |

### Your own model

<Demo name="OwnData" />

`Generator` returns an `IAsyncEnumerable<string>` of text deltas. When your service returns the whole reply at once,
wrap it with `ReplyDraftStreams.FromText` and the card streams it locally in chunks of 3 to 8 words. A request's `Mode`
is `Draft` for a fresh reply and `Rewrite` when the tone or length changed on a finished draft: then restyle
`Previous`, which holds the text on screen with the reader's edits. Throw to show the error state.

`DraftCompleted` fires each time a draft or rewrite finishes, with the text, tone, length and quick reply.

### Inserting

<Demo name="Insert" />

Insert, or Cmd+Enter (Ctrl+Enter) from anywhere inside the card, raises `InsertRequested` and then awaits
`InsertHandler`, showing a spinner on the button meanwhile. A handler that throws keeps the draft and says "Couldn't
insert". With no handler, and no `InsertRequested` handler that marks the event handled, the insert is simulated and the
announcement says that nothing was sent.

### Starting on demand

`AutoStart` writes the first draft as soon as the card scrolls into view. Set it to false to show a short line and a
Draft reply button instead, so the model only runs when the reader asks. `StartDraft()` and `Regenerate()` do the same
from code.

### Narrow containers

<Demo name="Narrow" />

The card fills its container up to 600px. Below 480px the padding drops to 16px and the body loses its avatar indent;
below 460px the hairline between the groups hides and Length moves to its own row. Chips wrap, long names and addresses
truncate, and nothing scrolls sideways. After a touch press, options grow to 32px and chips to 36px.

### Your own words

`Labels` holds every word the card shows or says; replace the object to localize or reword it:

```csharp
draft.Labels = new ReplyDraftLabels
{
    Ready = "Prêt à modifier",
    Insert = "Insérer",
    ReplyWith = model => "Répondre avec " + model,
};
```

`SourceMark` replaces the mail glyph before the subject with your own 24 × 24 stroked mark, and `AgentModel.Mark` the
model's sparkle.

### Content guidelines

- Quick reply labels are two or three words ("Sounds good", "Next week"); their ids are what your prompt understands.
- Keep drafts plain text: the draft box is a plain-text editor.
- Say what the insert does in your own composer; the card only hands over the text.

### Best practices

- Pass the request's cancellation token to your model client: every new action aborts the previous request.
- Treat `Rewrite` as restyling `Previous`, so the reader's edits survive a tone change.
- Leave `AutoPlay` off in products; it is for marketing and docs pages.

## API reference

<!-- api: ReplyDraft -->

<!-- api: ReplyDraftMessage -->

<!-- api: ReplyDraftSuggestion -->

<!-- api: ReplyDraftRequest -->

<!-- api: ReplyDraftGenerator -->

<!-- api: ReplyDraftInsertHandler -->

<!-- api: ReplyDraftStreams -->

<!-- api: ReplyDraftSimulation -->

<!-- api: ReplyDraftVersion -->

<!-- api: ReplyDraftLabels -->

<!-- api: ReplyDraftEventArgs -->

<!-- api: ReplyDraftSuggestionEventArgs -->

<!-- api: ReplyDraftToneChangedEventArgs -->

<!-- api: ReplyDraftLengthChangedEventArgs -->

<!-- api: ReplyDraftTone -->

<!-- api: ReplyDraftLength -->

<!-- api: ReplyDraftMode -->

<!-- api: ReplyDraftStatus -->

## Accessibility

- The card is named after the sender ("Email from Maya Chen"); the reply area is a named section.
- Quick replies are toggle buttons that report whether they are pressed; tone and length are radio groups where only the
  selected option is a tab stop.
- The finished draft is a plain-text editable text box named "Draft reply", so readers can fix a word before Insert; the
  edits are what Insert hands over.
- A polite live region announces drafting, rewriting, ready, inserted and errors; simulated actions say that nothing was
  sent. It stays quiet while the tour plays on its own.
- Status uses an icon plus a short word, never color alone.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the quick replies, the draft text, tone, length, Regenerate and Insert |
| Arrow keys | Inside Tone or Length, move the selection and rewrite the draft |
| Home, End | Inside Tone or Length, jump to the first or last option |
| Enter, Space | Activate a quick reply, an option or a button |
| Cmd+Enter, Ctrl+Enter | Insert the finished draft from anywhere inside the card |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A draft streams | Chunks of 3 to 8 words every 34ms; each chunk fades in over 140ms | The draft appears whole | The draft appears whole |
| Tone or length rewrite | The old draft dims, fades out in 100ms; the new one fades in over 160ms rising 2px | At once | At once |
| Draft box height | Springs to its height (0.26s, no bounce); held during a regenerate until the new draft finishes | At once | At once |
| Tone and length pill | Glides on the same bounce-free spring | Jumps | Jumps |
| Status | Crossfades (100ms out, 160ms in); working words shimmer | Crossfade without shimmer | At once |
| Chip or option press | Scales to 0.97 | No scale | No scale |

## Related

- `Textarea` for a plain multiline field the person writes in themselves.
- `ToolCall` and `AgentStatus` for what an agent is doing.
- `SegmentedControl` for switching between a small set of views.
