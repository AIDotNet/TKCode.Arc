---
title: Hero signup
summary: A landing hero with a large headline and an email field that validates, sends and confirms in place, over a quiet dot field with a row of faces as proof.
---

<Demo name="Preview" />

## When to use

`HeroSignup` is the first section of a landing page whose only job is to collect an email address: a waitlist, a
private beta, an early-access list. A pill-shaped eyebrow names the moment ("Private beta"), a large headline makes the
promise, one sentence explains it, and a single field with a button does the rest. The address is checked as the person
types, sent through your handler, and the form is replaced in place by a short confirmation, so the visitor never leaves
the top of the page. A row of generated faces and a line of proof ("Joined by 2,400 product teams") sit under the form,
and a faint dot field with a soft accent glow gives the section depth without a picture.

- **A waitlist or beta page** where the address is the conversion.
- **A launch page** that collects interest before the product ships.
- **A campaign hero** next to a picture, with the content lined up on the start edge.

## When not to use

- **Sign-in or account creation**: use the Auth blocks, which handle passwords and providers.
- **A newsletter with past issues to show**: use `NewsletterSignup`, which shows the newsletter itself.
- **A hero with several calls to action and no form**: use `CtaSection`.

## Usage

```xml
<arc:HeroSignup x:Name="Hero" Title="Release notes your whole team reads" ButtonText="Join the waitlist" />
```

```csharp
Hero.SubmitAsync = async (email, token) =>
{
    await http.PostAsJsonAsync("/waitlist", new { email }, token);   // throw to show FailedMessage and keep the address
};
Hero.Submitted += (_, e) => analytics.Track("waitlist", e.Email);
```

With no handler the send is simulated (900 ms, always accepted). `Phase` reports `Idle`, `Sending` or `Done`;
`SubmittedEmail` holds the accepted address and `Reset()` (the "Use a different email" button) brings the empty form
back. `Submit()` checks and sends the address from code.

### Validation

An empty field shows `EmptyMessage` and a malformed address `FormatMessage`, under the field. Before the first submit,
typing clears an error; after it, every keystroke checks again. A failed send keeps the address and shows
`FailedMessage`.

### Alignment

<Demo name="StartAligned" />

`Alignment="Start"` lines the eyebrow, headline, copy, form and proof row up on the start edge, for a hero with a picture
beside it. The default is `Center`.

### Proof row

`ProofText` is the line beside the faces; `ProofNames` lists the people drawn as generated portraits (at most five).
Set `ProofText` to null to hide the row, and `IsBackdropVisible` to false to drop the dot field.

### Text and localization

Every string is a property with an English default: `Eyebrow`, `Title`, `Description`, `Placeholder`, `EmailLabel`,
`ButtonText`, `SendingText`, `Note`, `EmptyMessage`, `FormatMessage`, `FailedMessage`, `DoneTitle`, `DoneFormat`,
`ResetText` and `ProofText`.

## API reference

<!-- api: HeroSignup -->

## Accessibility

- The section is named by its title, and the headline is a level 1 heading.
- The field has the screen reader name `EmailLabel`; errors are announced as they appear and the confirmation is
  announced when the address is accepted.
- The faces and the dot field are decorative and hidden from screen readers.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move between the field and the button, or to "Use a different email" after a signup. |
| `Enter` in the field | Check and send the address. |
| `Enter` / `Space` on a button | Press it. |

After a signup the focus moves to "Use a different email"; after a reset it returns to the field.

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Form gives way to the confirmation | The form fades, the confirmation rises in | Fade | Immediate |
| Error under the field | Opens on a spring | Fade | Immediate |
| Button while sending | Spinner and label swap | Spinner, fade | Spinner |

The dot field and glow are still: they never animate.

## Responsive behavior

- Breakpoints use the block's own width. Below 760 (`:compact`) the headline steps down from 52 to 36 and the paddings
  tighten; below 480 (`:narrow`) the headline is 28 and the field and the button stack at full width.
- The form is at most 500 wide and the copy at most 600, so long lines never stretch across a wide screen.
- Let the block stretch to the page width instead of giving it a fixed `Width`.

## Related

- `NewsletterSignup` for a signup that shows past issues.
- `CtaSection` for a closing call to action.
- `Input` and `Button`, which the form is built from.
