using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every built-in text of <see cref="SecuritySettings"/> (English defaults). Replace the whole object through
/// <see cref="SecuritySettings.Labels"/> to localize; texts with values are functions.
/// </summary>
public sealed class SecuritySettingsLabels
{
    /// <summary>Culture for dates and relative times (default en-US).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Summary title when all four safeguards are on.</summary>
    public string SummaryProtected { get; init; } = "Your account is well protected";

    /// <summary>Summary title when a safeguard is missing.</summary>
    public string SummaryImprove { get; init; } = "Add another safeguard";

    /// <summary>Summary line and the meter's accessible name, from the number of safeguards on.</summary>
    public Func<int, string> SummaryCount { get; init; } = count => $"{count} of 4 safeguards on";

    /// <summary>Header of the password row.</summary>
    public string PasswordHeader { get; init; } = "Password";

    /// <summary>Password row description from a relative time ("3 months ago", "Yesterday") or a date.</summary>
    public Func<string, string> PasswordChanged { get; init; } = relative =>
        $"Last changed {(relative == "Yesterday" ? "yesterday" : relative)}";

    /// <summary>Password row description when the change date is unknown.</summary>
    public string PasswordNeverChanged { get; init; } = "Never changed";

    /// <summary>Password row description during the first minute after a change.</summary>
    public string PasswordUpdatedNow { get; init; } = "Updated just now";

    /// <summary>Password row description when the account has no password.</summary>
    public string PasswordNotSet { get; init; } = "Not set. You sign in with a passkey or an email link.";

    /// <summary>Button that opens the form when the account has a password.</summary>
    public string ChangePassword { get; init; } = "Change password";

    /// <summary>Button that opens the form, and the form's submit button, when the account has no password.</summary>
    public string SetPassword { get; init; } = "Set password";

    /// <summary>Label of the current password field.</summary>
    public string CurrentPassword { get; init; } = "Current password";

    /// <summary>Label of the new password field.</summary>
    public string NewPassword { get; init; } = "New password";

    /// <summary>Button that closes the form without saving.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The form's submit button.</summary>
    public string UpdatePassword { get; init; } = "Update password";

    /// <summary>Header of the two-step verification card.</summary>
    public string TwoStepHeader { get; init; } = "Two-step verification";

    /// <summary>Description of the two-step verification card.</summary>
    public string TwoStepDescription { get; init; } = "Ask for a second step when you sign in on a new device.";

    /// <summary>Header of the authenticator app row.</summary>
    public string AuthenticatorHeader { get; init; } = "Authenticator app";

    /// <summary>Description of the authenticator app row.</summary>
    public string AuthenticatorDescription { get; init; } = "Use a six-digit code from an app on your phone.";

    /// <summary>Header of the text message row.</summary>
    public string TextMessageHeader { get; init; } = "Text message";

    /// <summary>Text message row description from the masked number ("•••• 4821").</summary>
    public Func<string, string> TextMessageDescription { get; init; } = masked => $"Codes go to {masked}";

    /// <summary>Text message row description when there is no phone number (the switch is disabled).</summary>
    public string TextMessageNoPhone { get; init; } = "Add a phone number first";

    /// <summary>Header of the passkeys row.</summary>
    public string PasskeysHeader { get; init; } = "Passkeys";

    /// <summary>Passkeys row description from the number of passkeys (1 or more).</summary>
    public Func<int, string> PasskeyCount { get; init; } = count => count == 1 ? "1 passkey" : $"{count} passkeys";

    /// <summary>Passkeys row description when there are none.</summary>
    public string NoPasskeys { get; init; } = "No passkeys yet";

    /// <summary>Button that asks the host to add a passkey.</summary>
    public string AddPasskey { get; init; } = "Add passkey";

    /// <summary>Header of the recovery codes row.</summary>
    public string RecoveryCodesHeader { get; init; } = "Recovery codes";

    /// <summary>Recovery codes row description from the creation date ("Jun 2, 2026").</summary>
    public Func<string, string> RecoveryCodesCreated { get; init; } = date => $"Created {date}";

    /// <summary>Recovery codes row description when no codes exist.</summary>
    public string RecoveryCodesMissing { get; init; } = "Not created yet";

    /// <summary>Button when recovery codes exist.</summary>
    public string ViewCodes { get; init; } = "View codes";

    /// <summary>Button when no recovery codes exist.</summary>
    public string CreateCodes { get; init; } = "Create codes";

    /// <summary>Header of the sessions card.</summary>
    public string SessionsHeader { get; init; } = "Where you're signed in";

    /// <summary>Description of the sessions card, from the number of other sessions.</summary>
    public Func<int, string> SessionsDescription { get; init; } = others => others == 0
        ? "Only this device is signed in."
        : "Sign out of any device you don't recognize.";

    /// <summary>Shown in the sessions card when there are no sessions at all.</summary>
    public string NoSessions { get; init; } = "No sessions to show.";

    /// <summary>Badge on the current session.</summary>
    public string ThisDevice { get; init; } = "This device";

    /// <summary>Activity text of the current session.</summary>
    public string ActiveNow { get; init; } = "Active now";

    /// <summary>Button on every other session.</summary>
    public string SignOut { get; init; } = "Sign out";

    /// <summary>Accessible name of a session's sign-out button, from the device name.</summary>
    public Func<string, string> SignOutSession { get; init; } = device => $"Sign out {device}";

    /// <summary>Resting label of the button that signs out every other session.</summary>
    public string SignOutOthers { get; init; } = "Sign out other sessions";

    /// <summary>Question asked before signing out the other sessions, from their number.</summary>
    public Func<int, string> SignOutOthersPrompt { get; init; } = count => count == 1 ? "Sign out 1 session?" : $"Sign out {count} sessions?";

    /// <summary>The same question on a narrow page, where the button sits under the list and has less room.</summary>
    public string SignOutOthersCompactPrompt { get; init; } = "Sign out all?";

    /// <summary>Confirm answer of that question.</summary>
    public string SignOutConfirm { get; init; } = "Sign out";

    /// <summary>Shown while the other sessions are being signed out.</summary>
    public string SigningOut { get; init; } = "Signing out";

    /// <summary>Shown once the other sessions are signed out.</summary>
    public string SignedOut { get; init; } = "Signed out";
}
