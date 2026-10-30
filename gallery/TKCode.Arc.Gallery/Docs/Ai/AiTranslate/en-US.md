---
title: AI translate
summary: A message card where picking a language streams the AI translation in place, sentence by sentence over the dimmed original, with a detected-language chip, a Show original toggle and a status line.
---

<Demo name="Preview" />

## When to use

`AiTranslate` translates one message or paragraph on demand, inside your product. A byline with the author and a chip
naming the text's language sits above the text; below it are a Translate picker, a Show original toggle and a status line
led by the model's mark. Picking a language dims the text on screen and streams the translation in place: each new
sentence that starts retires one sentence of the old text, while the card's height springs to fit. Finished translations
are kept, so Show original and switching back crossfade at once.

- **Messages, comments, reviews or support tickets** written in another language.
- **Release notes or announcements** that readers in several regions open in their own language.
- **Any place an AI translation should feel inline and reversible**, with the original one click away.

## When not to use

- **Translating a whole page or app interface**: use your localization framework.
- **Long documents with headings and lists**: the card renders one plain paragraph.
- **Live captions or speech**: use a realtime stream component.

## Usage

Give the card its `Text` and wire `Translator` to your model. The translator receives an `AiTranslateRequest` (the text,
the source and target languages and a cancellation token) and returns the translation as an async stream of text deltas.
Yield deltas as your model streams them, or yield the whole translation once: the card paces whatever arrives into chunks
of 3 to 6 words every 34ms either way.

```csharp
card.Text = message.Body;
card.Translator = TranslateAsync;

async IAsyncEnumerable<string> TranslateAsync(AiTranslateRequest request)
{
    // Pass the token on: it is cancelled when the reader picks another language or shows the original.
    await foreach (var delta in model.StreamAsync(request.Text, request.From, request.To, request.CancellationToken))
    {
        yield return delta;
    }
}
```

Without a translator the card simulates the stream from `Translations` when you pass ready translations. With no `Text`
and no `Simulation` the card is empty: no byline or chip, an empty paragraph, and the Translate picker off until a text
arrives.

### Simulation

To show the card without a translator (a demo, a design review, an onboarding tour), give it an `AiTranslateSimulation`:
the message, its byline, the model to credit and translations made ahead of time. Picking a language streams the ready
translation in place after a short wait (a language without one shows the failure), the status says it is simulated and
nothing was sent, and the card tours `Tour` by itself until someone presses a pointer or a key inside it. Your own `Text`
wins over the simulation, and setting `Simulation` to null stops it and shows the empty card.

```csharp
card.Simulation = new AiTranslateSimulation
{
    Text = "La version 4.2 est disponible.",
    Author = new AiTranslateAuthor { Name = "Camille Laurent", Role = "Product lead" },
    ModelName = "Gemini 3.8 Flash",
    Translations = new Dictionary<AiTranslateLanguage, string>
    {
        [AiTranslateLanguage.English] = "Version 4.2 is out.",
        [AiTranslateLanguage.German] = "Version 4.2 ist da.",
    },
    Tour = [AiTranslateLanguage.English, AiTranslateLanguage.German],
};
```

The preview above plays a simulation whose release note and translations are the gallery's own demo data.

### Streaming from your model

<Demo name="Streaming" />

The source language is detected from the text with a light check of common words when `SourceLanguage` is not set, and
the chip reads "Detected · German". A failure (an exception from your translator) puts the previous text back, shows
"Couldn't translate" with Retry, keeps the exception's message as the status tooltip and raises `TranslationFailed`.
Cancelled requests never raise it.

### Ready translations

<Demo name="Ready" />

Pass translations you made ahead of time in `Translations`; the card streams them with its simulation. A language that
is missing shows the failure. Set `SourceLanguage` when you know it: the chip then reads "Written in Spanish". Setting
`Value` to a language other than the source translates as soon as the card is shown. Without an `Author` the card shows
no byline; the chip stays.

### Following a preferred language

<Demo name="Thread" />

`Value` is the language on screen (two-way); null or the source language means the original. Render one card per message
and set every card's `Value` from a shared preferred language. `ValueChanged` is raised only when the reader picks a
language or toggles Show original, so you can remember the choice. Show original remembers the last translation for
Show translation.

### Narrow

<Demo name="Narrow" />

Below 470px the card pads 16px and the status line drops under the controls, in line with the text; below 380px the chip
shows just the language name. Names and meta trim with an ellipsis.

### Anatomy

1. **Byline**: the avatar with initials, the name over the role and timestamp, and the language chip at the far end.
2. **Paragraph**: the text in the language on screen. While a translation streams, the new text comes first and what is
   left of the old text follows, dimmed.
3. **Controls**: the Translate picker (the current language's own name once translated), the Show original toggle and,
   after a failure, Retry. Each is 32px high and reserves the width of its longest label.
4. **Status line**: the model's mark and a label: the model's name at rest, "Translating to German", "Translated from
   French", "Showing original", or a warning icon and "Couldn't translate".

### States

| `State` | Paragraph | Status |
|---|---|---|
| `Original` | The text as written | The model's name, then "Showing original" |
| `Translating` | The translation streaming in over the dimmed text | "Translating to German" |
| `Translated` | The translation | "Translated from French" |
| `Failed` | The previous text, back | A warning icon and "Couldn't translate", with Retry |

### Persisting translations

`Translated` is raised once a translation has fully arrived, before the card caches it. The cache lives per card and per
text; setting a new `Text` clears it and shows the new original. To keep translations across sessions, store them from
`Translated` and pass them back in `Translations`.

### Your own words and mark

`Labels` holds every word the card shows or says, including the language names. `ModelName` is the idle status and
`ModelMark` the 24 × 24 stroked glyph that leads every status; pass your provider's mark.

```csharp
card.ModelName = "Gemini 3.8 Flash";
card.Labels = new AiTranslateLabels
{
    Translate = "Traduire",
    ShowOriginal = "Voir l'original",
    TranslatingTo = language => "Traduction en " + language,
};
```

### Autoplay

A card playing a simulation tours the simulation's `Tour` (in the preview English, Deutsch, 日本語 and Português), then
the original, and loops; without a `Tour` it visits every language that has a translation. It pauses off screen and stops
for good on the first pointer or key press inside the card. `AutoPlay` turns it on or off for any card (a card of your own
tours every language in its picker); with reduced motion it translates once and stops. `IsSimulated` adds "Simulated,
nothing was sent" to the announcements (on by default without a translator or while a simulation plays).

### Content guidelines

- Keep one paragraph or message per card. For a thread, render one card per message.
- The chip names the source language; the picker lists each language by its own name, with the source marked Original.
- Credit the model you actually call in `ModelName`.

### Best practices

- Always pass the request's cancellation token to your network call, so switching languages stops the old request.
- Yield deltas as they arrive rather than waiting for the whole translation.
- Give `SourceLanguage` when you know it; detection is a light check meant for short messages.

### Common questions

**How are sentences split?** On `.`, `!` and `?` followed by a space, and on `。`, `！` and `？`. Decimals such as 4.2 stay
whole.

**Does switching languages mid-stream waste the request?** It cancels it: the token is cancelled and the previous text
comes back before the new language streams.

## API reference

<!-- api: AiTranslate -->

<!-- api: AiTranslateAuthor -->

<!-- api: AiTranslateRequest -->

<!-- api: AiTranslateSimulation -->

<!-- api: AiTranslateLabels -->

## Accessibility

- The card's accessible name is "Message from" and the author; the paragraph's name is the text on screen, and it reports
  busy while a translation streams.
- Starting, finishing, showing the original and failures are announced in a polite status region; while simulated, the
  announcement says that nothing was sent.
- The language menu is a group of radio rows with the current language checked. The Show original toggle reports pressed
  and its name switches to Show translation.
- Status is never color alone: the model's mark with a label, or a warning icon with "Couldn't translate".
- Every control is at least 32px tall and labels reserve the width of their longest version, so nothing moves under the
  pointer.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the Translate picker, the Show original toggle and, after a failure, Retry |
| Enter, Space, Arrow down | On the picker, opens the language menu on the current language |
| Arrow keys, Home, End | Move through the languages; typing a letter jumps to a language. The list loops |
| Enter, Space | Picks the highlighted language and closes the menu. Picking the language that just failed retries it |
| Escape | Closes the menu with focus back on the picker |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A chunk arrives | 3 to 6 words (about 10 characters in Japanese) every 34ms, each fading in over 140ms; no blur, slide or spring | At once | At once |
| Translating starts | The text on screen dims to 45% over 200ms | At once | At once |
| A new sentence starts | One dimmed old sentence leaves | Same | Same |
| A cached translation or the original | One crossfade: the old text leaves in 100ms, the new one fades in over 160ms with a 2px rise | At once | At once |
| The paragraph's height | Follows the text on a critically damped spring (0.24s, no bounce); a window resize cuts straight to the new height | At once | At once |
| Status, picker and toggle labels | Crossfade in their reserved width (100ms out, 160ms in) | Short fade | At once |
| Autoplay | Loops | Translates once and stops | Translates once and stops |

## Related

- `ToolCall` for the tool calls an agent makes in the same transcript.
- `DropdownMenu` for a focused list of actions on a trigger.
- `Badge` for small labels like the language chip.
