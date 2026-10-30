---
title: Provider keys
summary: A settings card for bring-your-own-key AI products: each provider's masked key, status and last use, with reveal, copy, rotate and remove with undo, and an inline form that tests a new key before saving.
---

<Demo name="Preview" />

## When to use

`ProviderKeys` is the settings card of a product where people bring their own AI provider keys. Each provider gets a
row with its mark, the masked key (`sk-••••4f2a`), a status as icon plus label and when the key was last used. A key
can be revealed for five seconds, copied, rotated or removed with a short undo window. Adding or rotating a key opens an
inline form under the row that checks the format as you type, then tests the connection before saving.

- **Workspace or account settings** where people add their own keys for the providers you support.
- **An onboarding step** that asks for a first key and shows when it connects.
- **A gateway admin page** that shows which providers are connected, which keys were rejected and when each was used.
- **Rotating a leaked key**, or removing one with a moment to undo it.

## When not to use

- **Choosing a model for a request**: use a model picker.
- **Your own product's API keys for customers**, with scopes and expiry: use a table.
- **Spend or rate limits**: use a usage meter.

## Usage

Set `Providers` to the providers you support, bind `Keys` (two-way) to what your server returns, handle `KeySaved` and
`KeyRemoved` to persist, and set `TestKey` to test keys on your server. Providers without a key read Not set. Without
`TestKey` a test fails with "No key test is set up", so no key reads Connected without a check.

```csharp
card.Providers =
[
    new ProviderKeyProvider { Id = "anthropic", Name = "Anthropic", KeyPrefix = "sk-ant-api03-" },
    new ProviderKeyProvider { Id = "google", Name = "Google" },   // no documented prefix: no prefix check
];
card.Keys = await api.GetProviderKeysAsync();      // one ProviderKey per provider
card.TestKey = request => api.TestProviderKeyAsync(request.Provider, request.Key, request.CancellationToken);
card.KeySaved += (_, e) => api.SaveProviderKey(e.Provider, e.Key);
card.KeyRemoved += (_, e) => api.DeleteProviderKey(e.Provider);
```

### Anatomy

1. **Mark**: the provider's glyph (or a monogram) in a tinted rounded square.
2. **Name over key**: the provider's name, then the masked key in the mono face (the full key while revealed).
3. **Status over last use**: Connected, Invalid or Not set with an icon, then "Used 2 min ago", "Never used" or the
   rejection reason. Pressing the status tests the stored key again.
4. **Actions**: reveal with a countdown ring, copy, rotate and remove; Add key on an empty row; Undo after a removal.
5. **Inline form**: the key input with Cancel and Save, and a helper line that says what is wrong, Testing connection
   or Connected.

### Your API

<Demo name="YourApi" />

`TestKey` receives a `ProviderKeyTestRequest`: the provider's id, the pasted key when adding or rotating (no key when
retesting a stored one, so test it on your server by provider) and a token that is cancelled when the form closes.
Return `ProviderKeyTestResult.Ok(detail)` or `ProviderKeyTestResult.Fail("401 Unauthorized")`; an exception counts as a
failure with its message. Rotation keeps the old key on the row until the new one passes, then swaps.

| Event | When |
|---|---|
| `KeysChanged` | Every change: added, rotated, removed, restored with Undo, or retested. `Keys` is already updated. |
| `KeySaved` | A new key passed its test. Persist it; the row already shows it as connected. |
| `KeyRemoved` | The undo window closed, so a removal reaches your server only once it sticks. |
| `KeyCopied` | A key was copied, for an audit log. |

Never send full keys to the client in production: give each `ProviderKey` only `Last4` and leave `Value` null. The row
then shows the masked key and turns reveal and copy off.

### Empty state

<Demo name="Empty" />

An empty `Keys` list shows every row as Not set with an Add key button. `Providers` chooses which providers get a row
and in what order; each `ProviderKeyProvider` gives the name, the key prefix and minimum length the form checks, and a
`Mark` glyph or a `Monogram`. Use the prefix the provider documents, and leave `KeyPrefix` empty when it documents
none.

### Simulated tests

For a demo or a prototype, set `Simulation` instead of `TestKey`: a `ProviderKeysSimulation` waits `Delay` (700ms),
fails a stored invalid key again with its own error (or `RejectedError`) and connects everything else. Nothing is sent,
and every announced result ends with "Simulated, nothing was sent." The demos on this page use real provider names and
the key prefixes their docs show; the keys are placeholders and the rejected key is invented.

```xml
<arc:ProviderKeys>
    <arc:ProviderKeys.Simulation>
        <arc:ProviderKeysSimulation Delay="0:0:0.7" />
    </arc:ProviderKeys.Simulation>
</arc:ProviderKeys>
```

### Loading

<Demo name="Loading" />

`IsLoading` shows placeholder rows with the real row geometry while keys load.

### Narrow cards

<Demo name="Narrow" />

At 560px and wider each row is one line of columns. Below that (`NarrowWidth`) the status moves beside the name, the
key gets its own line and last use shares a line with the actions; the form stacks its input above Cancel and Save.
The card holds at 360px with no horizontal scroll.

### Your own words

`Labels` holds every word the card shows or says, and `Title`, `Description` and `Footer` set the card's own text
(null hides the description or the footer):

```csharp
card.Labels = new ProviderKeysLabels
{
    Connected = "Verbunden",
    NotSet = "Nicht gesetzt",
    AddKey = "Schlüssel hinzufügen",
    CopyName = provider => $"{provider}-Schlüssel kopieren",
};
```

### Best practices

- Test keys from your server (a models list request is enough) and return the provider's own error.
- Pass `Last4` only; reveal and copy are for products that keep keys on the device.
- Keep `RevealDuration` short; five seconds is enough to read or compare a key.

## API reference

<!-- api: ProviderKeys -->

<!-- api: ProviderKey -->

<!-- api: ProviderKeyProvider -->

<!-- api: ProviderKeysSimulation -->

<!-- api: ProviderKeysLabels -->

## Accessibility

- The card is named by its title. Every icon button has a provider-specific name, such as "Copy the Anthropic key".
- Status is never color alone: an icon plus Connected, Invalid, Not set or Testing. The status button's name includes
  the rejection reason ("OpenAI key: Invalid, 401 Unauthorized. Test again").
- Reveal is a toggle that reports pressed; Rotate and Add key report whether the inline form is expanded.
- A polite live region announces reveals, copies, test results, removals and undo. Results of a `Simulation` add
  "Simulated, nothing was sent."
- The form's input is named "Anthropic API key" and its help text is the helper line; format errors and rejections
  mark it invalid.
- Focus is managed: the form focuses its input, Cancel and Escape return focus to the button that opened it, Remove moves
  focus to Undo and Undo returns it to Remove.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through each row's status (retest), reveal, copy, rotate and remove, or Add key on an empty row |
| Enter, Space | Presses the focused button |
| Enter | In the form, checks the format, then tests and saves the key |
| Escape | In the form, cancels and returns focus to the button that opened it |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Status, key, last use and helper text | One crossfade in place: out 100ms, in 160ms rising 2px | Short crossfade, no rise | At once |
| Inline form opens or closes | Height spring (0.24s, no bounce) with a short fade | At once | At once |
| Reveal countdown | A hairline ring around the button empties linearly over `RevealDuration` | No ring (the key still masks itself) | No ring |
| Undo countdown | A small ring in Undo empties over `UndoDuration` | No ring | No ring |
| Eye icon | The new icon scales up from 0.6 and fades in over 160ms | Fades only | At once |
| Pressing an icon button | Scales to 0.94 on a spring | No scale | No scale |
| Testing spinner | One turn every 0.8s | One turn every 1.6s | Still |

Each text sits in a fixed cell and the status column has a fixed width, so nothing resizes when a status or a key
changes. Relative times refresh every 30 seconds.

## Related

- `CopyButton` for copying a single value with confirmation.
- `Input` for a single line field with a label and an error.
- `ContextWindow` and `AgentStatus` for other parts of an AI product's settings and runs.
