---
title: Magic link sign-in
summary: Passwordless sign-in by email link: enter an address, check your inbox with a resend countdown, and send a fresh link when one has expired.
---

<Demo name="Preview" />

## When to use

`MagicLinkSignIn` signs people in without a password. They type their email and ask for a link; the card turns into
"Check your inbox" with their address, a resend button that waits out `ResendDelay` on a small countdown ring, and a
way back to change the address. When your app opens a link that is too old, show the expired state: one button sends
a fresh link to the same address. Your app sends the mail from `LinkRequested`, or from an async `LinkSender`, in
which case the button shows a spinner and a failure message if sending fails.

- **Products where most people sign in rarely** and forget passwords.
- **Invitations and first sign-ins** where the email address must be confirmed anyway.
- **A second way to sign in** next to a password or a single sign-on button.

## When not to use

- **Apps used many times a day**: a password or a passkey is quicker than opening the mailbox each time.
- **A code typed into the same screen**: use `OtpInput`.
- **A full sign-in page with providers and a password**: use `Login` or `SignIn`.

## Usage

```csharp
signIn.LinkSender = async request =>
{
    var response = await api.SendMagicLinkAsync(request.Email);
    return response.IsSuccessStatusCode;
};
// When the app opens a link that is too old:
signIn.ShowExpired();
```

`State` is two-way, so a router can also bind it. The address is checked when the person leaves the field or presses
the button, not while they type.

<Demo name="Expired" />

## API reference

<!-- api: MagicLinkSignIn -->

## Accessibility

- Enter in the email field sends the link; on the expired state it sends a fresh one.
- After each change of state, focus moves to its title (or back to the field) and the title is read out. Errors and
  "A new link is on its way" are read out too.
- The resend button is disabled while it waits, and its text says how long is left.

## Motion

- The states swap with a short lift while the card's height follows on a spring; the paper plane arrives from the
  lower left on "Check your inbox", and the countdown ring empties as the wait runs out.
- Reduced motion cross-fades the states and shows the plane in place. Off changes everything at once.

## Responsive behavior

- The card is at most 420 px wide and fills a phone. Below 360 px its padding tightens.
- The buttons always fill the card's width, so they stay easy to tap.

## Related

- `OtpInput` for typed codes, `TwoFactorSetup` for a second step at sign-in.
- `Login`, `LoginCentered` and `LoginImmersive` for full sign-in pages.
