---
title: Link unfurl
summary: A message composer that turns a pasted address into a link, shows it loading, then unrolls it into a preview card you can fold back or remove.
---

<Demo name="Preview" />

## When to use

`LinkUnfurl` is a message field for places where people share links: a chat channel, a comment box, a notes inbox.
An address that is pasted, or typed and followed by a space, becomes one inline link. While its preview is fetched the
link shows the address under a soft sweep; when the preview arrives the favicon and the page title roll into the link
and the link itself unrolls into a card under the message, with the site, the title, a short description and the share
image. The card can be folded back into the link at any time, or removed together with the link.

- **Chat and comment composers**: people see what a link points to before they send it, and can drop a preview that
  only adds noise.
- **Notes and inbox capture**: a pasted address turns into something readable at once.
- **Product pages that show how your app treats links**: with the built-in samples and autoplay, the composer plays the
  whole story on its own.

## When not to use

- **Showing a link someone else wrote**: use `HoverCard` to preview it on hover or focus; this control is for writing.
- **Tokens for people, channels or records**: use `MentionInput`, which suggests them as you type.
- **Long formatted documents**: use `RichTextEditor`.
- **A single address field**: a plain `TextField` with validation is clearer than a composer.

## Usage

`LinkUnfurl` derives from Avalonia's `TemplatedControl` and lives in the Special package: register it with
`UseArcSpecial()` next to `UseArc()`. With nothing set it is an empty composer that unfurls any address into a plain
card built from the address itself. The preview above adds three invented samples, a starting message and autoplay,
which pastes the first sample 1.1 seconds after the composer first comes into view, unless someone has already used it.

```xml
<arc:LinkUnfurl Label="Message #trail-club"
                Placeholder="Message #trail-club"
                Samples="{x:Static arc:LinkUnfurlSamples.Previews}" />
```

### Your own resolver and send handler

In an app, unfurl on your server (Open Graph, oEmbed) and return a `LinkUnfurlPreview` from `Resolver`. Return null
when there is nothing to show: the link then stays in the text without a card and fetches again when clicked. The
cancellation token fires when the link is removed or the message is sent, so honor it.

```csharp
composer.Resolver = FetchPreview;
composer.SendHandler = message => http.PostAsync("/api/messages", new StringContent(message.Text));

async Task<LinkUnfurlPreview?> FetchPreview(string url, CancellationToken cancellationToken)
{
    var response = await http.GetAsync($"/api/unfurl?url={Uri.EscapeDataString(url)}", cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return null;
    }
    // Read site, title, description and image from your own response format here.
    return new LinkUnfurlPreview(url, "Site name", "Page title") { Description = "One or two sentences." };
}
```

Enter or the send button raises `SendRequested` and then calls `SendHandler`. The message carries the text, with every
link written out as the address that was pasted, and the previews that resolved. While the returned task runs, the
composer holds: the message is read-only and the send button shows a spinner. When it completes the content lifts away,
the composer shrinks back and the button shows a check for 1.6 seconds; when it fails, its message appears under the
composer as an alert and the text stays. The demo below logs every event; addresses containing "missing" get no
preview, and a message containing "fail" is refused.

<Demo name="Resolver" />

### Opening cards on demand

With `AutoExpand` off a resolved link gains its favicon and title but its card stays folded until someone clicks the
link, presses its sample button or presses Alt+Enter after it. `LinkAccent` changes the tint that ties an unfurled link
to its card; this demo uses the second series color.

<Demo name="ManualOpen" />

### Anatomy

1. **Composer**: at most 576px wide, a 1px border that darkens while focus is inside, radius 26.
2. **Message**: Inter 16/26, at least two lines high. Links sit in the text as single units.
3. **Link**: 26px high and at most 224px wide. Loading: a globe and the address without its scheme, under a sweep.
   Resolved: the favicon and the title, truncated with an ellipsis. Unfurled: an accent tint and ring.
4. **Lane**: the cards, one per link in the order of the links, 10px apart.
5. **Card**: at most 440px wide, with a header (favicon, site, a collapse button and a remove button), the title as a
   real link, up to two lines of description and the image at the Open Graph ratio of 1.91 : 1. A preview without an
   image gets a text-only card; one without a favicon shows the site's initial on a small square.
6. **Toolbar**: "Try" and the sample buttons on the left, the round send button on the right.

### States

| Part | State | Look |
|---|---|---|
| Link | Loading | Muted fill, a thin ring, secondary text, a breathing globe and a sweep every 1.15 s |
| Link | Resolved, folded | Muted fill, the favicon and the title in the text color |
| Link | Unfurled | Accent at 11% (20% in dark) and an accent ring at 34% (48%) |
| Link | No preview | The address with a still globe; clicking fetches again |
| Card | Open / folded | A folded card is hidden and out of the tab order |
| Sample button | Not in the message / in it | A thin ring / a muted fill |
| Send button | Idle / pending / sent | Text color / a spinner / the success color with a check |

### Gestures

- **Click a link** to fold or open its card; a link without a preview fetches again.
- **Drag a card up** to fold it. The card follows the pointer (a drag of 80% of its height folds it completely);
  letting go past 40%, or with an upward flick, folds it, otherwise it springs back open at the speed it was let go.
  With a mouse or pen the whole card drags; on touch only the header does, so the page still scrolls over the body.
- **Sample buttons** paste their link, then fold or open its card.

### Narrow containers

The composer sizes itself from its own width: under 560px its paddings step down, and under 360px "Try" hides so the
sample buttons and the send button keep one row (the samples scroll sideways when they still do not fit). Cards fill
narrower lanes and their images keep the 1.91 : 1 ratio.

<Demo name="Narrow" />

### Reduced motion

Under reduced motion, and while `IsPaused` is set, cards open and fold at once and a loading link shows a still tint
instead of the sweep. Every state, name and announcement stays the same.

<Demo name="ReducedMotion" />

### Content guidelines

- Keep favicons square; they sit in a 16px slot that lines up between the link and its card.
- Return titles as the page states them; the link truncates long titles at 224px and the card shows the whole title.
- Keep descriptions to a sentence or two; the card shows two lines.
- Give `ImageAlt` when the image carries meaning; without it the image is decorative to assistive technology.
- Name the field after where the message goes (`Label`, "Message #trail-club") and reuse it as the placeholder.

### Best practices

- Fetch previews on your server, with a short timeout, and return null rather than an empty preview.
- Decode the image before you return the preview, so the card never opens on an empty frame.
- Let people remove a preview they do not want; the remove button takes the link out of the message too.
- Keep `AutoPlay` for marketing pages and demos; in a real composer turn it off (it only acts when samples exist).

### Common questions

**Does the text keep the address?** Yes. `Text` and the sent message hold the address exactly as it was pasted or
typed; the link is only how it is drawn.

**What counts as an address?** Text starting with `http://`, `https://` or `www.` whose host has a dot and a top-level
domain of at least two letters. `https://x` and `localhost:3000` stay text.

**Can I open a card from code?** Use `ExpandPreview`, `CollapsePreview`, `TogglePreview`, `RemoveLink` and `Retry` with
a link's `Id` from `Links`; `Paste` adds a link at the caret and `Send` sends.

## API reference

<!-- api: LinkUnfurl -->

<!-- api: LinkUnfurlPreview -->

<!-- api: LinkUnfurlLink -->

<!-- api: LinkUnfurlMessage -->

<!-- api: LinkUnfurlLabels -->

<!-- api: LinkUnfurlLinkEventArgs -->

<!-- api: LinkUnfurlSendRequestedEventArgs -->

<!-- api: LinkUnfurlSamples -->

## Accessibility

- The message is a multi-line text field named by `Label` and described by `LinkUnfurlLabels.KeyHint`, which lists its
  keys. Its status lists the links by name: the address and "preview on its way" while loading, "Link to site: title"
  once resolved.
- A link is one unit: the caret steps over it, and one Backspace or Delete next to it removes all of it.
- Every card has a named collapse button and a named remove button, and its title is a real link that opens the
  address. A folded card is hidden and out of the tab order; keyboard focus inside a card that folds goes back to the
  message.
- A polite live region announces a link being added, a card opening, folding and being removed, a missing preview and
  the sent message. A send error appears under the composer as an assertive alert.
- Loading is shown by the sweep, by muted text and by a globe in place of the favicon, and it is announced: never by
  color alone.
- Keyboard focus shows the 2px focus ring and a muted fill on the buttons; pointer focus shows none.

### Keyboard

| Key | Action |
|---|---|
| Enter | Sends the message |
| Shift+Enter | Starts a new line |
| Alt+Enter | Opens or folds the card of the nearest link before the caret |
| Backspace / Delete | Next to a link, removes the whole link |
| Space after an address | Turns the address into a link |
| Tab | Moves from the message to each open card's collapse button, remove button and title, then to the samples and the send button |

## Motion

- One spring progress drives a card: the card is clipped to the exact rectangle of its link, with the two favicons
  aligned, and moved onto it; as the spring runs the clip widens first and deepens after (width follows
  1 − (1 − p)^1.6, height p^1.35), the corner radius grows from 13 to 18, and a plate under the card lifts on a shadow
  only mid-flight. The body fades in after the header and the image settles from a slight zoom. Opening uses a slightly
  overdamped spring that settles in about a second; folding runs the same path backwards on a quicker one.
- The lane grows with the card on the same progress, so the toolbar glides instead of jumping.
- When a preview arrives the link's width eases to fit the title over 340 ms while the title rolls up and the favicon
  pops in.
- A removed link fades and shrinks out after its card has folded into it.
- Sending lifts the content away (180 ms), eases the composer to its new height (480 ms) and fades the empty field back.
- `Speed` scales the morph and the simulated fetch. Reduced motion and `IsPaused` open and fold at once and keep the
  loading tint still. Cards land at once when the composer leaves the visual tree.

## Responsive behavior

- `LinkUnfurl` follows its own width, up to 576 px: below 560 px (`:compact`) the editor, the preview lane and the
  toolbar use smaller padding; below 360 px (`:tight`) the hint in front of the sample links is hidden.
- Preview cards are at most 440 px wide and shrink with the composer; long titles and descriptions end with an ellipsis.
- The card buttons are 30 px round but have a 44 px target, so they are easy to hit on touch. Fold a card back by
  dragging it, or use its fold and remove buttons; nothing depends on hover.

## Related

- `MentionInput`: a text field with @people and #channel tokens and suggestions at the caret.
- `RichTextEditor`: a lightweight editor with formatting, for long text.
- `HoverCard`: previews a person or link on hover or focus without leaving the page.
- `TokenTextEditor`: the editor this composer builds on, for your own kinds of inline tokens.
