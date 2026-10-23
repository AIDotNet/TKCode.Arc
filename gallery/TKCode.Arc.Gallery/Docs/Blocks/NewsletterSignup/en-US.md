---
title: Newsletter signup
summary: An email signup framed by a stack of past issues; subscribing drops the next issue, addressed to you, onto the front.
---

<Demo name="Preview" />

## When to use

Use `NewsletterSignup` to collect newsletter or waitlist addresses where the newsletter itself is the best argument for
signing up. Beside the form it shows a stack of recent issues, so a visitor sees what arrives before giving an address.
When the address is accepted, the next issue, addressed to that visitor, lands on the front of the stack and the reader
count goes up by one.

- **A landing page section** for a design, engineering or product newsletter, with the copy and form on one side and the stack on the other.
- **A self-contained card** in a footer, a settings page or a modal, with the stack in a tray above the form.
- **A waitlist** where the form alone is enough (turn the stack and the readers off).

## When not to use

- **Account creation or login**: use a form with passwords and validation rules of its own.
- **A newsletter without issues to show**: use the footer signup of the site footer block, which is a single line.
- **Several fields** (name, topics, frequency): this block collects one email address.

## Usage

```xml
<arc:NewsletterSignup x:Name="Signup" />
```

```csharp
Signup.SubscribeAsync = async (email, token) =>
{
    await http.PostAsJsonAsync("/subscribe", new { email }, token);   // throw to show the error and keep the address
};
Signup.Subscribed += (_, e) => analytics.Track("newsletter", e.Email);
```

With no configuration the block shows Arc's copy, the publication "Margins" and 12,480 readers, and simulates the send
(1100 ms, always succeeding). Replace `SubscribeAsync` with a call to your mail provider: return to show the success
state, throw to show the failure state. The token is cancelled when the block is reset or removed.

### The subscription

| Phase | What shows |
|---|---|
| `Idle` | The empty field, "Subscribe →" and the privacy note. After a failed send the button reads "Try again →" and the address is kept. |
| `Sending` | The field is read-only and the button shows a ring and "Subscribing". Further presses are ignored. |
| `Done` | The button turns green with a check mark and "Subscribed", the confirmation replaces the note, and the next issue sits on the front of the stack. |

`Phase` and `SubscribedEmail` are read-only properties. `Reset()` (the "Use a different email" button calls it) clears the
address and returns to `Idle`.

### Validation

An empty field and a malformed address each have their own message (`EmptyMessage`, `FormatMessage`). Validation starts
when the field loses focus with text in it; once a submit was tried it follows every keystroke. Before that, typing clears
the error. A failed validation shakes the pill and keeps the focus in the field.

### Variants

`Variant` is `Inline` (the default) or `Card`.

<Demo name="Card" />

```xml
<arc:NewsletterSignup Variant="Card" />
```

### The publication

`Publication` holds the name, the `Upcoming` issue and the `Recent` issues (newest first). The first three recent issues
make the stack. Each `NewsletterIssue` has a number (its identity), a date line, a subject and up to three
`NewsletterStory` items with a title, a thumbnail `IImage` and the reading time. Reader faces come from `ReaderFaces`.

```csharp
var publication = new NewsletterPublication { Name = "Margins", Upcoming = next };
publication.Recent.Add(issue148);
publication.Recent.Add(issue147);
signup.Publication = publication;
signup.ReaderFaces.Add(faceBitmap);
```

Without a publication the block uses Arc's sample "Margins", whose stories have no thumbnails. Set
`IsPublicationVisible` or `IsReadersVisible` to false to hide the stack or the readers.

### Form only

<Demo name="FormOnly" />

With neither the stack nor the readers the section becomes one centered column of at most 720 px.

### Narrow containers

<Demo name="Narrow" />

The block follows its own width, not the window's. At 860 px or less the layout is one column. At 520 it tightens its
spacing and type, at 400 it drops the reading times and shows only the recipient on an addressed issue, and at 360 the
readers line may wrap. A form narrower than 300 px stacks the field and the button, both full width.

### Text and localization

Every string is a property with Arc's English default: `Placeholder`, `ButtonLabel`, `PrivacyNote`, `PrivacyLinkLabel`,
`EmailLabel`, `RetryLabel`, `SendingLabel`, `DoneLabel`, `EmptyMessage`, `FormatMessage`, `FailedMessage`, `DoneMessage`,
`DifferentEmailLabel`, `RecipientFormat`, `IssueFormat`, `MinutesFormat`, `ReadersWord`, `IncludingYou` and
`SentToFormat`.

### The privacy link

Pressing it raises `Navigate` (and runs `NavigateCommand`). When the event is not handled and `PrivacyLinkHref` is an
absolute `http`, `https` or `mailto` address, it is opened with the platform launcher. Set `IsPrivacyLinkVisible` to false
to hide it.

## API reference

<!-- api: NewsletterSignup -->

<!-- api: NewsletterPublication -->

<!-- api: NewsletterIssue -->

<!-- api: NewsletterStory -->

<!-- api: NewsletterSubscribedEventArgs -->

<!-- api: NewsletterNavigateEventArgs -->

## Accessibility

- The block is a region named by its title. The field has the hidden label `EmailLabel` and the email input hint.
- Errors are announced assertively; the confirmation is announced politely and includes where the link went.
- Only the front issue is exposed to screen readers ("Margins, issue 148"). The issues behind it, the thumbnails and the faces are decorative.
- Keyboard focus rings are 2 px outside the button and the links. Hover styles apply to a mouse or a pen only.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move between the field, the button and the link. A subscribed button leaves the tab order. |
| `Enter` in the field | Check and send the address (ignored while an input method is composing). |
| `Enter` / `Space` on a button | Press it. |

After a successful send the focus moves to "Use a different email"; after `Reset()` it returns to the field.

## Motion

- Subscribing places the next issue on the front: it rises from below onto the stack while the others step back and shrink (a no-overshoot spring), and the oldest issue drops away behind.
- The label of the button and the message line swap in place with a short blur; the check mark draws itself.
- A rejected address or a failed send shakes the pill. The reader count rolls up by one and ", including you" opens its width.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Issues trade places | Spring on position and scale, opacity in 120 ms | Placed at once, opacity in 160 ms | Immediate |
| Label and message swap | Lift and blur, 240 ms in, 160 ms out | Fade, 120 ms | Immediate |
| Shake | 360 ms | None | None |
| Check mark | Draws in 340 ms | Drawn | Drawn |

### Responsive behavior

Breakpoints use the width of the block: 860, 520, 400 and 360 px, plus 300 px for the form itself.

## Related

- The site footer block for a one-line newsletter field inside a footer.
- The CTA section block for a call to action that leads to a signup page.
- `EmptyStates` for an empty view with a retryable action.
