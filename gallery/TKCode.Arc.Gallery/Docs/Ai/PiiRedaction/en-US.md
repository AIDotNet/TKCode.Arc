---
title: PII redaction
summary: A guardrail card where personal data is redacted before the prompt reaches the model, and the reply comes back with the original values put back.
---

<Demo name="Preview" />

## When to use

`PiiRedaction` shows what a privacy guardrail does to a prompt. Scan marks each name, email, phone number and card
number in the message with its type, then swaps each one for a numbered placeholder such as `[EMAIL_1]` in a single
crossfade. Send carries the safe prompt along a small flow (you, the PII guard, the model), the reply streams back with
its placeholders, and the guard puts the original values back for the person. Show original flips the message between
the safe prompt and what was typed.

- **Explaining or proving a guardrail** in an AI product: support assistants, health or finance copilots, enterprise
  chat.
- **A settings or onboarding screen** where admins see what the PII filter does before they turn it on.
- **A playground** where people paste text and see exactly what would reach the model.

## When not to use

- **The conversation itself**: use a chat or composer and redact behind the scenes; show this card only where the
  guardrail needs explaining.
- **Compliance**: do not rely on the built-in detector. Pass a real DLP or NER service through `Detector`.
- **An arbitrary tool step in a transcript**: use `ToolCall`.

## Usage

```xml
<arc:PiiRedaction AutoPlay="False" Message="" />
```

```csharp
card.Names = [user.Name];
card.Detector = async (text, token) => await dlp.ScanAsync(text, token);   // spans with an exclusive end
card.Sender = async (prompt, token) => await chat.CompleteAsync(prompt, token); // may contain [NAME_1], [EMAIL_1]
card.Redacted += (_, e) => audit.Log("pii.redacted", e.Entities.Select(x => x.Type));
```

`PiiRedaction` is a `TemplatedControl` in the Special package (`UseArcSpecial()`). It fills its container up to 640px.
With no `Message` and no `Simulation` the card shows an empty message field ("Paste a message with names, emails, phone
numbers or cards") and Scan is off until something is typed; the flow names the model "Model" until `ModelName` is set.

### Simulation

To show the card without a model (a demo, a design review, an onboarding tour), give it a `PiiRedactionSimulation`: the
message, the names to flag, the model the safe prompt goes to and the model's reply with its placeholders. While the card
has no `Message` it shows the simulation's and plays scan, send and restore on a loop until someone takes over; the reply
is simulated and the card says so. An edited message gets a short stand-in reply. Your own values win over the
simulation's, and setting `Simulation` to null stops it and shows the empty field.

```csharp
card.Simulation = new PiiRedactionSimulation
{
    Message = "Hi, I'm Juniper Vale. Send the receipt to juniper.vale@example.com.",
    Names = ["Juniper Vale"],
    ModelName = "Grok 4.7",
    Reply = "Done, [NAME_1]. The receipt is on its way to [EMAIL_1].",
};
```

The preview above plays a simulation of an invented support conversation, the gallery's own demo data: a double charge
request from Juniper Vale with a test card number (4242 4242 4242 4242), an email on example.com and a phone number in
the fictional 555 range.

### Anatomy

1. **Head**: a shield, "PII guard" and, while the reply is simulated, a Simulated tag.
2. **Message**: the editable field before a scan; the marked original while it scans; then the safe prompt, with each
   placeholder in its type's color over a rounded mark. Above it, the label ("Message", "Safe prompt" or "Original
   message") and the Show original switch.
3. **Type cells**: Name, Email, Phone and Card, each reading "Not checked", a shimmering "Checking", its token
   ("EMAIL_1", or "EMAIL_1 +1" for more) or "None found". A cell that found something takes a light tint.
4. **Flow strip**: you, the guard and the model, with a dot travelling each hop. The working node's label shimmers.
5. **Reply**: the model's mark and name over the reply, which streams in with its placeholders and then shows the
   restored text with a mark under each value put back.
6. **Footer**: the status with an icon, Reset once there is something to reset, and the one primary action.

### Phases

The primary action is one button whose label follows the phase, so keyboard focus stays put through the whole flow.

| Phase | Status | Primary action |
|---|---|---|
| `Idle` | Ready to scan | Scan |
| `Scanning` | Scanning for personal data | Stop |
| `Redacted` | 4 items redacted, or No personal data found | Send |
| `Sending` | Sending the safe prompt | Stop |
| `Thinking` | Waiting for Grok 4.7 | Stop |
| `Streaming` | Grok 4.7 is replying | Stop |
| `Restoring` | Restoring original values | Stop |
| `Done` | Reply restored | Run again |
| `Error` | The exception's message | Retry |

Stop during a scan returns to the editable message; during a request it cancels the token passed to `Sender` and
returns to the safe prompt. Retry runs the failed step again. Reset cancels anything in flight and returns to the field.
`Scan()`, `Send()`, `Stop()` and `Reset()` do the same from code.

### Playground

<Demo name="Playground" />

`AutoPlay="False"` with `Message=""` gives a blank field. People paste their text and press Scan, or Cmd or Ctrl plus
Enter in the field. `Message` is two way, so the app can read what was typed. `IsEditable="False"` keeps the field read
only.

### Your detector and your model

<Demo name="OwnModel" />

`Detector` replaces the built-in detector with your DLP or NER service. It gets the text and a cancellation token and
returns `PiiMatch` spans (a type, a start and an exclusive end). `Sender` sends the safe prompt to your model and returns
its reply with the placeholders; the card restores them. Without a `Sender` the reply is simulated: the head shows the
Simulated tag and the status adds "Simulated, nothing was sent". A thrown exception in either shows its message with
Retry.

`Redacted` reports the safe prompt and every `PiiEntity` (type, position, value, token and placeholder). `ReplyRestored`
reports the raw reply, the restored text and whether it was simulated. `PhaseChanged` follows every phase. `Phase`,
`SafePrompt` and `Reply` are read only and bindable; `ShowOriginal` is two way. `Entities` holds the last redaction's
entities and `IsSimulated` says whether the reply is simulated.

`ModelName` and `ModelMark` (a 24 × 24 stroked glyph, the sparkle by default) name the model on the flow and above the
reply; `ModelMarkBrush` colors the mark. `NameBrush`, `EmailBrush`, `PhoneBrush` and `CardBrush` set the type colors
(Series1 to Series4 by default).

### The helpers without the card

```csharp
// Server side, before the prompt leaves your backend.
var result = PiiRedactor.Redact(message, PiiRedactor.Detect(message, [user.Name]));
var reply = PiiRedactor.Restore(await model.CompleteAsync(result.Prompt), result.Entities);
```

| Member | What it does |
|---|---|
| `PiiRedactor.Detect(text, names)` | Finds emails, card numbers (13 to 19 digits that pass Luhn), phone numbers (7 to 15 digits) and names (the list, plus "I'm", "I am", "this is" and "my name is" followed by capitalized words), sorted and without overlaps |
| `PiiRedactor.Redact(text, matches)` | Returns a `PiiRedactionResult` with the safe prompt and the entities; overlapping spans after the first are skipped and a repeated value keeps its token |
| `PiiRedactor.Restore(reply, entities)` | Puts the values back and leaves unknown tokens as they are |
| `PiiRedactor.PassesLuhn(value)` | The Luhn check on the digits of a value |

| Type | Members |
|---|---|
| `PiiType` | `Name`, `Email`, `Phone`, `Card` |
| `PiiRedactionPhase` | `Idle`, `Scanning`, `Redacted`, `Sending`, `Thinking`, `Streaming`, `Restoring`, `Done`, `Error` |
| `PiiMatch` | `Type`, `Start`, `End` (exclusive), `Length` |
| `PiiEntity` | `Type`, `Start`, `End`, `Value`, `Token` (`EMAIL_1`), `Placeholder` (`[EMAIL_1]`), `PromptStart`, `PromptEnd` |
| `PiiRedactionResult` | `Prompt`, `Entities` |

### Narrow containers

<Demo name="Narrow" />

The layout follows the card's own width. Below 520px the four type cells fall into two rows of two. Below 400px the card
tightens its padding and gaps and hides the Simulated tag (the status still announces it), and a long model name
truncates with an ellipsis in the flow. The text layers stack in one cell and the reply reserves the height of the
simulation's restored reply, so nothing jumps at 360px, 390px or desktop widths.

### Content guidelines

- Keep placeholders verbatim in your system prompt ("Refer to [EMAIL_1] exactly as written") so the guard can restore
  them.
- Keep one token per value across a conversation if the model refers back to it: `Redact` numbers per call, so persist
  the entity map between turns.
- Sample data in demos must be obviously fake: invented names, example.com emails, 555 numbers and test card numbers.

### Best practices

**Do**

- Redact on the server in production and keep the entities in the request scope.
- Set `AutoPlay="False"` in product UI.
- Log the types of what was redacted, never the values next to the prompt.

**Avoid**

- Treating the built-in detector as complete: names beyond the list and simple introductions need an NER model.
- Sending the original message anywhere the card shows as safe.

### Common questions

**When does the autoplay stop?** For good as soon as someone edits the message, toggles Show original or presses a
button. It only runs while at least a quarter of the card is on screen, and never under reduced motion.

**What happens to a placeholder the card does not know?** It stays as written, so a model that invents `[CARD_7]` shows
exactly that.

**Can I keep the safe prompt but hide the reply?** Leave the card at `Redacted` and read `SafePrompt`; Send is the
person's choice.

## API reference

<!-- api: PiiRedaction -->

<!-- api: PiiRedactionLabels -->

<!-- api: PiiRedactionSimulation -->

<!-- api: PiiRedactedEventArgs -->

<!-- api: PiiReplyRestoredEventArgs -->

<!-- api: PiiRedactionPhaseChangedEventArgs -->

## Accessibility

- A polite status region announces each phase ("Scanning for personal data", "4 items redacted", "Reply restored") and,
  when the reply is simulated, adds "Simulated, nothing was sent".
- Status never relies on color: every phase pairs an icon with a short label, each type cell reads its name with its
  token or "None found", and the original message lists its marks ("juniper.vale@example.com: Email, redacted as
  EMAIL_1").
- Scan, Stop, Send, Run again and Retry are one button whose label changes, so focus stays put. Reset returns focus to the
  message field.
- Only the visible text layer is exposed to assistive tech; the hidden layers that keep the height steady are not.
- The flow strip is decorative and hidden from screen readers, since the status region narrates each hop.
- Show original is the switch's own label, and the switch is disabled and dimmed until there is a safe prompt.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the message field (before a scan), Show original once there is a safe prompt, Reset when it shows, and the primary action |
| Cmd + Enter, Ctrl + Enter | Scans from the field; focus moves to the primary action, which now reads Stop |
| Space, Enter | Toggles Show original, or presses Scan, Stop, Send, Run again, Retry or Reset |

## Motion

- Scan marks each entity in turn, 120ms apart, with a 160ms tint in its type color, while the matching type cell
  crossfades from "Checking" to its token.
- The swap to placeholders is one crossfade between two stacked text layers (the old one out in 100ms, the new one in
  over 160ms), so no word slides or rewraps and the height never changes. Show original uses the same crossfade.
- Each hop of the flow is one dot travelling the connector in 460ms on an ease in out curve; the working node's label
  shimmers.
- The reply streams three to six words every 34ms, each chunk only fading in over 160ms, then crossfades once to the
  restored text.
- Labels, statuses and the Reset button crossfade in place; nothing bounces and nothing changes size between states.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Marks | One after another, 120ms apart, 160ms tint | All at once | All at once |
| Layer swap | 100ms out, 160ms in | 100ms out, 160ms in | At once |
| Flow hops | A dot travels 460ms | None | None |
| Reply | Streams in chunks, then crossfades | Shows whole, then crossfades | At once |
| Autoplay | Loops while on screen | Off | Off |

## Related

- `ToolCall` for an arbitrary tool step in a transcript.
- `AgentStatus` for the live status of a whole agent run.
- `Switch` for the toggle on its own.
