---
title: Security settings
summary: A security checkup, password change, two-step verification and signed-in sessions, each handled in place.
---

<Demo name="Overview" />

## When to use

- The security page of an account: one place to check how well the account is protected and to fix what is missing.
- Changing a password without leaving the page, with the strength shown while the new one is typed.
- Turning two-step methods on and off, adding passkeys and reaching recovery codes.
- Reviewing where the account is signed in and signing out a lost or unknown device.

## When not to use

- Signing in or signing up: use a sign-in form. This block assumes the person is already signed in.
- Workspace-wide policies (enforced two-step verification, allowed sign-in methods): those are admin settings, best
  written as `SettingsGroup` rows on a settings page.
- A single password field in a form: use `PasswordField` or `PasswordStrength` directly.

## Usage

Set the account's state and the block shows it in four cards: a checkup, the password, two-step verification and the
signed-in sessions. Every action happens in place. Switches write `IsAuthenticatorEnabled` and `IsTextMessageEnabled`
back (two-way). The password form, "Add passkey", the recovery codes and signing out tell the host through routed
events.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SecuritySettings IsAuthenticatorEnabled="True"
                          PhoneNumber="+31 6 2045 4821"
                          PasskeyCount="2">
        <arc:SecuritySettings.Sessions>
            <arc:SecuritySession Device="MacBook Air" Client="Firefox" Location="Rotterdam, NL"
                                 Kind="Laptop" IsCurrent="True" />
            <arc:SecuritySession Device="Pixel 9" Client="Larkspur app" Location="Rotterdam, NL"
                                 Kind="Phone" LastActive="2026-10-20T08:00:00+02:00" />
        </arc:SecuritySettings.Sessions>
    </arc:SecuritySettings>
</UserControl>
```

Times are shown relative to `Clock`, a `TimeProvider` that defaults to the system clock and is read again every 30
seconds while the block is shown. Pass a fixed clock in tests:

```csharp
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}

security.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(2)));
```

### Security checkup

The checkup counts four safeguards: a password (`HasPassword`), two-step verification by either method, at least one
passkey (`PasskeyCount`) and recovery codes (`RecoveryCodesCreatedAt`). `ProtectionScore` is that count, 0 to 4. Each
safeguard has its own segment in the meter, and the shield takes the score's tone: success at 4, accent at 2 or 3,
warning below. `Sections` picks the cards to show; here only the checkup and two-step verification. Turn a method on
or add a passkey and the meter fills in.

<Demo name="Checkup" />

Hide the checkup with `IsSummaryVisible="False"` when the page shows its own summary.

### Changing the password

"Change password" opens a form under the row: the current password, a new password with its strength meter and
requirement list, Cancel and "Update password". The update button needs the current password and a new one of at least
fair strength (`MinimumPasswordLevel`, level 2). Enter submits, Escape cancels. Submitting raises
`PasswordChangeRequested` with both passwords. Leave `ErrorMessage` empty to accept: the form closes,
`PasswordChangedAt` becomes the clock's time and the row reads "Updated just now". Set it to reject: the message shows
under the current password and focus goes back there. In this demo the current password is "correct horse".

<Demo name="ChangePassword" />

Without a password (`HasPassword="False"`) the row offers "Set password" and the form asks only for the new one. A
rejection then shows under the new password. `OpenPasswordForm()`, `ClosePasswordForm()` and `SubmitPasswordChange()`
do the same from code.

### Signed-in sessions

Sessions are listed with the current device first, then by most recent activity. The current device has a "This
device" badge and cannot be signed out here. Every other session has a "Sign out" button. Its row fades and collapses,
the session is removed from `Sessions` and `SessionSignedOut` is raised. "Sign out other sessions" asks in place and,
once confirmed, the rows leave one after another before `OtherSessionsSignedOut` carries the signed-out sessions.
`SignOut(session)` and `SignOutOtherSessions()` do the same from code. A read-only list, such as an array, is left
as it is and its signed-out rows are hidden instead.

<Demo name="Sessions" />

On a narrow page the meter moves under the checkup text and "Sign out other sessions" moves to a strip under the list.
Rows whose control is a switch, a badge or "Sign out" keep it beside the text. Rows with a wider button stack it under the
text.

### Labels

Every built-in text is in `SecuritySettingsLabels`. Replace the whole object to localize; texts with a value are
functions. `Culture` formats the dates.

```csharp
security.Labels = new SecuritySettingsLabels
{
    Culture = CultureInfo.GetCultureInfo("nl-NL"),
    PasswordHeader = "Wachtwoord",
    ChangePassword = "Wachtwoord wijzigen",
    SessionsHeader = "Waar je bent ingelogd",
    SignOut = "Afmelden",
    SignOutSession = device => $"{device} afmelden",
    SummaryCount = count => $"{count} van 4 beveiligingen aan"
};
```

## API reference

<!-- api: SecuritySettings -->

**SecuritySession** (a plain object, can be written in XAML)

| Property | Type | Description |
|---|---|---|
| `Device` | `string` | The device's name, such as "MacBook Air". |
| `Client` | `string?` | The browser or app, shown after the device name. |
| `Location` | `string?` | Where the device signed in from, such as "Rotterdam, NL". |
| `LastActive` | `DateTimeOffset` | When the session was last used; shown relative to `Clock`. |
| `Kind` | `SecurityDeviceKind` | `Desktop`, `Laptop`, `Phone`, `Tablet` or `Other`; picks the glyph. |
| `IsCurrent` | `bool` | The device in use: listed first, marked "This device", cannot be signed out here. |

**Event arguments**

| Type | Property | Description |
|---|---|---|
| `SecurityPasswordChangeEventArgs` | `CurrentPassword` | What was typed as the current password (empty without a password). |
| | `NewPassword` | The new password. |
| | `ErrorMessage` | Set to reject the change; the form stays open and shows it. |
| `SecuritySessionEventArgs` | `Session` | The session that was signed out. |
| `SecuritySessionsEventArgs` | `Sessions` | The sessions signed out by "Sign out other sessions", in list order. |

**SecuritySettingsSections** (flags): `Summary`, `Password`, `TwoStep`, `Sessions`, `All` (default), `None`.

**SecuritySettingsLabels**: `Culture` and one property per built-in text, such as `SummaryProtected`,
`SummaryCount`, `PasswordChanged`, `ChangePassword`, `UpdatePassword`, `TextMessageDescription`, `PasskeyCount`,
`SessionsHeader`, `SignOut`, `SignOutSession`, `SignOutOthers` and `SignOutOthersPrompt`.

## Accessibility

- Every switch and button has a name. Switches take their row's header ("Authenticator app") and description. A
  session's button is named after its device, such as "Sign out ThinkPad X1", and the badge reads "This device".
- The checkup meter is named "{n} of 4 safeguards on". The checkup title and the password row's description are polite
  live regions, so "Your account is well protected" and "Updated just now" are announced when they change.
- Opening the form moves focus to its first field. Enter submits when "Update password" is enabled and Escape cancels.
  After closing, focus returns to "Change password". A rejected change focuses the field with the error and selects
  its text.
- When a focused "Sign out" row leaves, focus moves to the next session's button, or the previous one.
- "Sign out other sessions" follows `ConfirmMorph`: it asks first, and Escape or Cancel keeps the sessions. It is
  disabled when no other session is left.
- Tab order follows the cards from top to bottom. The text message switch is disabled without a phone number, and the
  row says why.

## Motion

- Session rows leave with `PresenceHost`: they fade and their height collapses on a smooth spring before the session is
  removed. "Sign out other sessions" starts each row 35ms after the one above it.
- The password form springs open and closed with `SpringSizeDecorator`. The password row's description changes with a
  blur swap. "Sign out other sessions" morphs into its question and result in place.
- The checkup title blurs to its new text. The count rolls up or down with the score. Meter segments and the shield
  fade to their new color.
- Reduced motion keeps short fades and drops the springs, the blur and the roll. Off places everything at once, and a
  signed-out row leaves the list at once.

## Related

- `SettingsGroup` and `SettingsRow` build the cards and rows. The other Settings blocks use them too: Settings page,
  Settings command, Billing overview, Usage billing and Cancel flow.
- `PasswordField` and `PasswordStrength` for the password form, `Switch` for the two-step methods, `Badge` for the
  current device, `Button` for the row actions and `ConfirmMorph` for signing out every other session.
