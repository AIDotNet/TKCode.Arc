---
title: Security settings
summary: A security checkup, password change, two-step verification and signed-in sessions, each handled in place.
---

<Demo name="Overview" />

## When to use

`SecuritySettings` is the security page of an account. It sits in the settings area of a product and gathers the four
things a person checks about their account: how well it is protected, the password, the two-step methods and the
devices that are signed in. Each action (changing the password, turning a method on, signing out a device) happens on
the page, so the person does not have to move between screens to fix what the checkup flags.

- **A customer account page in a SaaS product or a bank portal.** The checkup tells the person how many of four
  safeguards are on and what to add next, and the password row shows when the password was last changed.
- **An admin's own security settings in an internal tool**, where the admin manages the passkeys and recovery codes of
  their account before they manage anyone else's.
- **A developer platform's account page** that lists the devices holding an API session, so a developer can sign out a
  laptop they no longer have.
- **A fintech app that asks for a second step** by authenticator app or text message, where the switches and the masked
  phone number show exactly which method signs the person in.
- **A recovery flow after a lost phone**, where the person signs out every other session with one confirmed action.

## When not to use

- **Signing in or creating an account.** Use a sign-in form built from `Input` and `PasswordField`; this block assumes
  that the person is already signed in.
- **Workspace-wide security policies**, such as enforced two-step verification or the allowed sign-in methods. Those are
  admin settings: write them as `SettingsGroup` and `SettingsRow` rows on a settings page.
- **A single password field in any other form**, such as a change request inside a checkout. Use `PasswordField` with
  `PasswordStrength` directly.
- **Reviewing an audit trail of events** (who signed in, from where, when, and what changed). Use `Timeline` for the
  history, and keep this block for the current state and the actions on it.
- **Choosing one security setting from a list**, such as a session timeout. Use `Select` in a settings form.

## Usage

Set the account's state and the block shows it in four cards: a checkup, the password, two-step verification and the
signed-in sessions. Every action happens in place. The two switches write `IsAuthenticatorEnabled` and
`IsTextMessageEnabled` back (two-way), and the password form writes `PasswordChangedAt` and `HasPassword` back when it
succeeds. "Add passkey", "View codes" or "Create codes", and signing out, are raised as routed events for the host to
handle.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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

`SecuritySettings` derives from Avalonia's `TemplatedControl`. Its cards are `SettingsGroup` and `SettingsRow` from the
Settings blocks, the switches are the Arc `Switch`, the password fields are `PasswordField` with `PasswordStrength`, and
the sign-out confirmation is `ConfirmMorph`.

Times are shown relative to `Clock`, a `TimeProvider` that defaults to the system clock. The block reads it again every
30 seconds while it is shown, so "2 hours ago" stays current. Pass a fixed clock in tests:

```csharp
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}

security.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(2)));
```

### Anatomy

1. **Checkup card** (`IsSummaryVisible`): a 44 px shield tile with a 22 px glyph, the title, the count line and a
   four-segment meter on the right. Each segment is 28 by 6 px with a 4 px gap, one per safeguard.
2. **Password row** (`HasPassword`, `PasswordChangedAt`): the header "Password", the description ("Last changed 3
   months ago", "Never changed" or "Updated just now") and a button: "Change password", or "Set password" without a
   password.
3. **Password form**: an inline form that opens under the row, at most 400 px wide and aligned with the row text. It holds
   the current password, the new password with its strength meter and requirement list, Cancel and "Update password".
4. **Two-step rows** (`IsAuthenticatorEnabled`, `IsTextMessageEnabled`, `PhoneNumber`, `PasskeyCount`,
   `RecoveryCodesCreatedAt`): an authenticator app switch, a text message switch with the masked number, a passkeys row
   with "Add passkey" and a recovery codes row with "View codes" or "Create codes".
5. **Sessions card** (`Sessions`): a header with the "Sign out other sessions" confirmation, and one row per session: the
   device glyph, "Device · Client", "Location · relative time" and either a "This device" badge or a "Sign out" button.

### Security checkup

The checkup counts four safeguards: a password (`HasPassword`), two-step verification by either method, at least one
passkey (`PasskeyCount`) and recovery codes (`RecoveryCodesCreatedAt`). `ProtectionScore` is that count, from 0 to 4,
and it is read-only. Each safeguard fills its own meter segment. The shield takes the score's tone: success at 4, accent
at 2 or 3, and warning at 0 or 1. The title changes at 4 to "Your account is well protected" and otherwise reads "Add
another safeguard".

Use the checkup to show what is missing, not to pressure the person. Turning a method on or adding a passkey fills the
meter, so the page explains its own result as the person acts.

<Demo name="Checkup" />

`Sections` chooses the cards to show, and it combines flags such as `Password, Sessions`. `IsSummaryVisible` hides the
checkup alone; use it when the page already has its own summary, as the demo below does.

### Changing the password

"Change password" opens the form under the row. The form asks for the current password and a new one. "Update password"
needs both: the current password is not empty, and the new password reaches at least fair strength (the
`MinimumPasswordLevel` constant, level 2). Enter submits the form when the button is enabled, and Escape cancels.

Submitting raises `PasswordChangeRequested` with `CurrentPassword` and `NewPassword`. The host checks the password and
then decides:

- **Accept** by leaving `ErrorMessage` empty. The form closes, `PasswordChangedAt` becomes the clock's time and the row
  reads "Updated just now".
- **Reject** by setting `ErrorMessage`. The message appears under the current password field, the form stays open and
  focus returns to that field. Without a password the message appears under the new password field.

Without a password (`HasPassword="False"`) the row offers "Set password" and the form asks only for the new one. A
successful change sets `HasPassword` to true. `OpenPasswordForm()`, `ClosePasswordForm()` and `SubmitPasswordChange()`
do the same from code.

<Demo name="ChangePassword" />

### Signed-in sessions

Sessions are listed with the current device first, then by most recent activity. The current device has a "This device"
badge and cannot be signed out here. Every other session has a "Sign out" button. Choosing it fades and collapses the
row, removes the session from `Sessions` when that list can be changed, and raises `SessionSignedOut`.

"Sign out other sessions" is a `ConfirmMorph`. It is enabled only when another session exists, and it asks first: the
question names the count ("Sign out 3 sessions?"), and Escape or Cancel keeps every session. When the person confirms,
the rows leave one after another, 35 ms apart, and `OtherSessionsSignedOut` carries the sessions that were removed. When
the list is read-only (for example an array), the rows are hidden instead of removed.

`SignOut(session)` and `SignOutOtherSessions()` do the same from code.

<Demo name="Sessions" />

On a narrow page the meter moves under the checkup text and "Sign out other sessions" moves under the list. Rows with a
small control, such as a badge or "Sign out", keep it beside the text; rows with a wider button stack it under the text.

### Binding to a view model

The view model owns the security state and the rules for it. The block shows the state, raises requests, and applies the
result that the view model sets.

```csharp
public partial class AccountSecurityViewModel : ObservableObject
{
    private readonly IAccountSecurity _security;

    public AccountSecurityViewModel(IAccountSecurity security)
    {
        _security = security;
    }

    [ObservableProperty]
    private bool _isAuthenticatorEnabled;

    [ObservableProperty]
    private DateTimeOffset? _passwordChangedAt;

    [ObservableProperty]
    private int _passkeyCount;

    [ObservableProperty]
    private DateTimeOffset? _recoveryCodesCreatedAt;

    public ObservableCollection<SecuritySession> Sessions { get; } = [];

    public async Task<string?> ChangePasswordAsync(string current, string next)
    {
        var result = await _security.ChangePasswordAsync(current, next);
        return result.IsSuccess ? null : result.Error;
    }
}
```

```xml
<arc:SecuritySettings IsAuthenticatorEnabled="{Binding IsAuthenticatorEnabled, Mode=TwoWay}"
                      PasswordChangedAt="{Binding PasswordChangedAt, Mode=TwoWay}"
                      Sessions="{Binding Sessions}"
                      PasskeyCount="{Binding PasskeyCount}"
                      RecoveryCodesCreatedAt="{Binding RecoveryCodesCreatedAt}" />
```

- `IsAuthenticatorEnabled`, `IsTextMessageEnabled`, `HasPassword` and `PasswordChangedAt` are two-way, so the view model
  receives the change from the switch or the form.
- The password change has no command. Handle `PasswordChangeRequested`, call `ChangePasswordAsync` and set the
  returned message as `ErrorMessage` on the event arguments.
- `Sessions` is an `IList<SecuritySession>` that the block updates when a session is signed out. Bind it to an
  `ObservableCollection<SecuritySession>` so the list stays in step with the server.
- `PasskeyCount` and `RecoveryCodesCreatedAt` are one-way. Update them after the host finishes "Add passkey" or "Create
  codes".

### Example: Account security for a bank customer

A bank's portal shows a customer their account security, without the sessions card (the Sessions demo covers that
card). The customer has an authenticator app and two passkeys but no recovery codes yet, so the checkup reads three of
four and the recovery codes row offers "Create codes". The scene has no open form, so the screenshot shows the resting
state.

![Account security with a three of four checkup, two-step methods and two signed-in devices](images/SecuritySettings.png)

```xml
<arc:SecuritySettings Width="640"
                      Sections="Summary, Password, TwoStep"
                      PasswordChangedAt="2026-07-14T09:00:00+02:00"
                      IsAuthenticatorEnabled="True"
                      IsTextMessageEnabled="False"
                      PhoneNumber="+31 6 2045 4821"
                      PasskeyCount="2" />
```

### Content guidelines

The block's labels are short and written for the person who owns the account. Use the sentence case and the verbs the
defaults use, and describe a state in the row rather than in an alert. Describe a method by what it does for the person
("Codes go to •••• 4821"), not by its protocol name.

| Instead of | Write |
|---|---|
| Two factor authentication | Two-step verification |
| Password was changed 3 months ago | Last changed 3 months ago |
| Remove session | Sign out (on each row) and Sign out other sessions (for the list) |
| Are you sure? | Sign out 3 sessions? (the count is in the question) |
| 2FA: Off | Text message switch with its row description |
| Error: wrong password | That isn't your current password. (the reason, under the field) |

Keep the labels in `SecuritySettingsLabels` when you localize. Use the `Culture` property for dates, because relative
times and dates follow it.

### Best practices

**Do**

- Turn on the checkup for every account page, so the meter and the next step are always visible. Hide it only when the
  page already has its own summary.
- Set `RecoveryCodesCreatedAt` and `PasskeyCount` from the server before showing the page. Their rows and the score
  depend on them, so a stale value gives a false checkup.
- Set `ErrorMessage` for any password the server rejects, with the server's reason in words the person can act on. The
  form stays open, so they can correct the value without retyping everything.
- Bind `Sessions` to the server's list. Signing out a session in the block removes it from the list only when the list is
  writable, so the server call belongs in the `SessionSignedOut` handler.

**Avoid**

- Showing the phone number in full. The block shows only its last four digits, and the app should not show more elsewhere
  on the page.
- Using the sessions card for the sign-in history. It lists the sessions that are open now, not past events.
- Replacing the whole `Labels` object for a single word. Build a new `SecuritySettingsLabels` with the properties you need
  changed, so the other texts keep their defaults.

### Common questions

**Why is "Update password" disabled when the new password looks fine?** The button needs both a non-empty current
password and a new password that reaches at least fair strength (level 2).

**How do I handle a sign-out the server refuses?** The row leaves before `SessionSignedOut` is raised, so the host cannot
stop it. When the server refuses, add the session back to `Sessions` and show your own message.

**Why does the text message switch stay off?** It is disabled until `PhoneNumber` has a value, and its row says "Add a
phone number first". Set the number to enable it.

**Can I show only the sessions on a page of my own?** Yes. Set `Sections="Sessions"`, or combine flags such as
`Password, Sessions`. The cards that are not chosen are not shown at all.

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
  After closing, focus returns to "Change password". A rejected change focuses the field with the error.
- When a focused "Sign out" row leaves, focus moves to the next session's button, or the previous one.
- "Sign out other sessions" follows `ConfirmMorph`: it asks first, and Escape or Cancel keeps the sessions. It is
  disabled when no other session is left.
- Tab order follows the cards from top to bottom. The text message switch is disabled without a phone number, and the
  row says why.

### Keyboard

| Keys | Where | Action |
|---|---|---|
| `Tab` / `Shift+Tab` | Anywhere | Move through the cards and rows from top to bottom |
| `Enter` | "Change password" or "Set password" | Open the password form; focus moves to the current password field |
| `Enter` | Password form | Submit when "Update password" is enabled |
| `Escape` | Password form | Cancel: close the form without saving and return focus to "Change password" |
| `Escape` or Cancel | "Sign out other sessions" question | Keep every session |

## Motion

- Session rows leave with a fade and a height collapse on a smooth spring, before the session is removed. "Sign out other
  sessions" starts each row 35 ms after the one above it.
- The password form springs open and closed under its row. The password row's description changes with a blur swap.
  "Sign out other sessions" morphs into its question and result in place.
- The checkup title blurs to its new text. The count rolls up or down with the score. Meter segments and the shield
  fade to their new color.
- Reduced motion keeps short fades and drops the springs, the blur and the roll. Off places everything at once, and a
  signed-out row leaves the list at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Session row leaves | Fades and collapses on a smooth spring | Fades | Leaves at once |
| Password form opens or closes | Springs open or closed | Shown or hidden with a short fade | Immediate |
| Password description changes | Blur swap to the new text | Short fade | Immediate |
| Checkup count and meter | Count rolls; segments fade to their color | Color fades | Immediate |

## Related

- `SettingsGroup` and `SettingsRow` build the cards and rows. The other Settings blocks use them too: Settings page,
  Settings command, Billing overview, Usage billing and Cancel flow.
- `PasswordField` and `PasswordStrength` for the password form, `Switch` for the two-step methods, `Badge` for the
  current device, `Button` for the row actions and `ConfirmMorph` for signing out every other session.

| Component | Choose it when |
|---|---|
| `SettingsPage` | The security card sits inside a page of settings with a navigation list |
| `PasswordField` / `PasswordStrength` | You need only a password field, or a password with its rules, outside this page |
| `Switch` | A single on or off setting with a label and description |
| `ConfirmMorph` | A single destructive action needs a confirmation in place |
| `Timeline` | The person needs the history of account events, not the current state |
