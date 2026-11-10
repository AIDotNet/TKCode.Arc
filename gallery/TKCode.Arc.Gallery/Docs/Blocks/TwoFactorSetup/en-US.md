---
title: Two-factor setup
summary: Turn on two-factor sign-in: choose an authenticator app or text messages, scan a real QR code, confirm a six-digit code and save backup codes.
---

<Demo name="Preview" />

## When to use

`TwoFactorSetup` is the card that opens when a person turns on two-factor sign-in. It walks through four steps with a
progress bar on top: choose an authenticator app or text messages; scan a QR code (or type the key shown under it) or
enter a phone number; type a six-digit code to prove it works; and save the backup codes. The QR code is real: it
holds an `otpauth://` address with the issuer, the account and the secret, drawn by an encoder in this block, so any
authenticator app can scan it. Without a `CodeVerifier` the card checks codes itself as time-based codes (RFC 6238,
30 second steps, one step of drift either way).

- **Security settings** of an account, next to the password and the signed-in devices.
- **Required setup after sign-up** for teams that ask every member to use two-factor sign-in.
- **Admin consoles** where the method must be verified before it is saved.

## When not to use

- **Asking for a code at sign-in**: use `OtpInput` on the sign-in page.
- **Showing whether two-factor sign-in is on**: use `SecuritySettings`, which lists the methods and their state.
- **Sign-in without a password**: use `MagicLinkSignIn`.

## Usage

```csharp
setup.Issuer = "Lumenfield";
setup.AccountName = user.Email;
setup.Secret = server.CreateSecret(user);
setup.CodeVerifier = request => server.VerifyAsync(user, request.Code);
setup.TextMessageRequested += (_, e) => sms.SendCode(e.PhoneNumber);
setup.Completed += (_, e) => server.Enable(user, e.Method, e.BackupCodes);
```

`ComputeCode`, `GenerateSecret` and `GenerateBackupCodes` help on the server side or in tests. "Download" opens a save
dialog for a text file of the codes; handle `BackupCodesSaving` to save them yourself.

<Demo name="AppOnly" />

## API reference

<!-- api: TwoFactorSetup -->

## Accessibility

- After each step change, focus moves to what comes next: the code field, the phone field, or the step's title, and
  the step is read out ("Step 2 of 4: Scan this code with your app").
- The QR code is an image named after the issuer and the account; the key under it can be selected and copied.
- Enter runs the step's main action, Alt+Left goes back, and a wrong code is read out with a hint about the clock.

## Motion

- Steps slide in the direction of travel while the card's height follows on a spring. The QR code fades in from the
  top left corner in diagonal bands, and the check on the last step draws itself.
- Reduced motion cross-fades the steps and shows the QR code and the check at once. Off changes everything at once.

## Responsive behavior

- The card is at most 560 px wide. Below 460 px the QR code stacks over its instructions, the padding tightens and
  the buttons fill the row.
- The QR code keeps whole-pixel modules and a light quiet zone in both themes, so cameras read it reliably.

## Related

- `OtpInput`, `RadioCards`, `Stepper`, `Checkbox` and `CopyButton`, which the card is built from.
- `SecuritySettings` for the overview page and `MagicLinkSignIn` for passwordless sign-in.
