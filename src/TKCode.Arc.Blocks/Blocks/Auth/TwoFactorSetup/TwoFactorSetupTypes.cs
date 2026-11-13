using System.Globalization;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How the person will receive their sign-in codes in a <see cref="TwoFactorSetup"/>.</summary>
public enum TwoFactorSetupMethod
{
    /// <summary>An authenticator app that reads the QR code and shows a new code every 30 seconds.</summary>
    AuthenticatorApp,

    /// <summary>A text message with a code sent to a phone number.</summary>
    TextMessage
}

/// <summary>The step a <see cref="TwoFactorSetup"/> shows.</summary>
public enum TwoFactorSetupStep
{
    /// <summary>Choose the method.</summary>
    Method,

    /// <summary>Scan the QR code, or enter the phone number.</summary>
    Connect,

    /// <summary>Enter a code to prove the method works.</summary>
    Verify,

    /// <summary>Save the backup codes.</summary>
    BackupCodes,

    /// <summary>Two-factor sign-in is on.</summary>
    Done
}

/// <summary>What a <see cref="TwoFactorSetup.CodeVerifier"/> checks: the method, the code typed and, for a text message, the phone number.</summary>
/// <param name="Method">The chosen method.</param>
/// <param name="Code">The six digits the person entered.</param>
/// <param name="Secret">The secret shown in the QR code (normalized Base32).</param>
/// <param name="PhoneNumber">The phone number for a text message; null for an authenticator app.</param>
public sealed record TwoFactorSetupVerifyRequest(TwoFactorSetupMethod Method, string Code, string Secret, string? PhoneNumber);

/// <summary>Arguments of <see cref="TwoFactorSetup.StepChanged"/>.</summary>
public sealed class TwoFactorSetupStepChangedEventArgs : RoutedEventArgs
{
    public TwoFactorSetupStepChangedEventArgs(RoutedEvent routedEvent, TwoFactorSetupStep oldStep, TwoFactorSetupStep newStep) : base(routedEvent)
    {
        OldStep = oldStep;
        NewStep = newStep;
    }

    /// <summary>The step that was shown.</summary>
    public TwoFactorSetupStep OldStep { get; }

    /// <summary>The step now shown.</summary>
    public TwoFactorSetupStep NewStep { get; }
}

/// <summary>Arguments of <see cref="TwoFactorSetup.TextMessageRequested"/>: send a code to <see cref="PhoneNumber"/>.</summary>
public sealed class TwoFactorSetupTextMessageEventArgs : RoutedEventArgs
{
    public TwoFactorSetupTextMessageEventArgs(RoutedEvent routedEvent, string phoneNumber, bool isResend) : base(routedEvent)
    {
        PhoneNumber = phoneNumber;
        IsResend = isResend;
    }

    /// <summary>The number as the person typed it.</summary>
    public string PhoneNumber { get; }

    /// <summary>True when the person asked for another code.</summary>
    public bool IsResend { get; }
}

/// <summary>Arguments of <see cref="TwoFactorSetup.Completed"/>: the method that is now on and the backup codes the person saved.</summary>
public sealed class TwoFactorSetupCompletedEventArgs : RoutedEventArgs
{
    public TwoFactorSetupCompletedEventArgs(RoutedEvent routedEvent, TwoFactorSetupMethod method, string secret, string? phoneNumber,
        IReadOnlyList<string> backupCodes) : base(routedEvent)
    {
        Method = method;
        Secret = secret;
        PhoneNumber = phoneNumber;
        BackupCodes = backupCodes;
    }

    /// <summary>The method that was verified.</summary>
    public TwoFactorSetupMethod Method { get; }

    /// <summary>The secret the authenticator app now holds (normalized Base32); store it on the server.</summary>
    public string Secret { get; }

    /// <summary>The verified phone number, or null for an authenticator app.</summary>
    public string? PhoneNumber { get; }

    /// <summary>The backup codes that were shown.</summary>
    public IReadOnlyList<string> BackupCodes { get; }
}

/// <summary>Arguments of <see cref="TwoFactorSetup.BackupCodesSaving"/>. Mark it handled to save the file yourself.</summary>
public sealed class TwoFactorSetupSaveEventArgs : RoutedEventArgs
{
    public TwoFactorSetupSaveEventArgs(RoutedEvent routedEvent, string fileName, string text) : base(routedEvent)
    {
        FileName = fileName;
        Text = text;
    }

    /// <summary>The suggested file name.</summary>
    public string FileName { get; }

    /// <summary>The file's text: a heading line and one code per line.</summary>
    public string Text { get; }
}

/// <summary>
/// The built-in text of a <see cref="TwoFactorSetup"/>. Replace the whole object (with an object initializer) to localize
/// or reword it; the card rebuilds its text when <see cref="TwoFactorSetup.Labels"/> changes.
/// </summary>
public sealed class TwoFactorSetupLabels
{
    /// <summary>The culture of numbers in the text (default en-US).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The card's heading: "Two-factor authentication".</summary>
    public string Title { get; init; } = "Two-factor authentication";

    /// <summary>The sentence under the heading.</summary>
    public string Description { get; init; } = "Add a second step to signing in, so a stolen password alone is not enough.";

    /// <summary>The step names of the progress: Method, Connect, Verify, Backup codes.</summary>
    public string[] StepNames { get; init; } = ["Method", "Connect", "Verify", "Backup codes"];

    /// <summary>The title of the method step.</summary>
    public string MethodTitle { get; init; } = "How do you want to get codes?";

    /// <summary>The authenticator app option.</summary>
    public string AppOption { get; init; } = "Authenticator app";

    /// <summary>The sentence under the authenticator app option.</summary>
    public string AppOptionDescription { get; init; } = "Scan a QR code once; the app shows a new code every 30 seconds, even offline.";

    /// <summary>The tag beside the authenticator app option.</summary>
    public string Recommended { get; init; } = "Recommended";

    /// <summary>The text message option.</summary>
    public string TextOption { get; init; } = "Text message";

    /// <summary>The sentence under the text message option.</summary>
    public string TextOptionDescription { get; init; } = "We send a code to your phone each time you sign in.";

    /// <summary>The title of the QR step.</summary>
    public string ScanTitle { get; init; } = "Scan this code with your app";

    /// <summary>The numbered instructions beside the QR code.</summary>
    public string[] ScanSteps { get; init; } =
    [
        "Open your authenticator app and choose to add an account.",
        "Point the camera at the code, or choose to enter a key.",
        "The app adds the account and starts showing codes."
    ];

    /// <summary>The label over the secret key.</summary>
    public string ManualKey { get; init; } = "Can't scan? Enter this key";

    /// <summary>The accessible name of the QR code: issuer and account ⇒ "QR code for Lumenfield, mara@…".</summary>
    public Func<string, string, string> QrName { get; init; } = static (issuer, account) => $"QR code for {issuer}, {account}";

    /// <summary>The copy button beside the key.</summary>
    public string CopyKey { get; init; } = "Copy key";

    /// <summary>The title of the phone step.</summary>
    public string PhoneTitle { get; init; } = "Which phone should get the codes?";

    /// <summary>The phone field's label.</summary>
    public string PhoneLabel { get; init; } = "Phone number";

    /// <summary>The phone field's placeholder.</summary>
    public string PhonePlaceholder { get; init; } = "+1 555 010 0199";

    /// <summary>The phone field's description.</summary>
    public string PhoneDescription { get; init; } = "Include the country code. Message and data rates may apply.";

    /// <summary>The error under a number that is too short.</summary>
    public string PhoneInvalid { get; init; } = "Enter a phone number with at least 7 digits.";

    /// <summary>The title of the verify step.</summary>
    public string VerifyTitle { get; init; } = "Enter the 6-digit code";

    /// <summary>The sentence of the verify step for an authenticator app.</summary>
    public string VerifyAppDescription { get; init; } = "Type the code your app shows for this account.";

    /// <summary>The sentence of the verify step for a text message: masked number ⇒ "We sent a code to •••• 0199.".</summary>
    public Func<string, string> VerifyTextDescription { get; init; } = static masked => $"We sent a code to {masked}.";

    /// <summary>The label of the code field.</summary>
    public string CodeLabel { get; init; } = "Verification code";

    /// <summary>The error under a wrong code.</summary>
    public string CodeInvalid { get; init; } = "That code didn't work. Check the time on your device and try the newest code.";

    /// <summary>The button that sends another text message.</summary>
    public string Resend { get; init; } = "Send a new code";

    /// <summary>Said after another code is sent.</summary>
    public string Resent { get; init; } = "A new code is on its way.";

    /// <summary>The title of the backup codes step.</summary>
    public string BackupTitle { get; init; } = "Save your backup codes";

    /// <summary>The sentence of the backup codes step.</summary>
    public string BackupDescription { get; init; } = "If you lose your phone, each code signs you in once. Keep them somewhere safe, like a password manager.";

    /// <summary>The accessible name of the code list.</summary>
    public string BackupListName { get; init; } = "Backup codes";

    /// <summary>The copy button.</summary>
    public string CopyCodes { get; init; } = "Copy all";

    /// <summary>The download button.</summary>
    public string Download { get; init; } = "Download";

    /// <summary>The suggested name of the downloaded file.</summary>
    public string FileName { get; init; } = "backup-codes.txt";

    /// <summary>The first line of the copied or downloaded text: issuer and account ⇒ "Lumenfield backup codes for mara@…".</summary>
    public Func<string, string, string> FileHeading { get; init; } = static (issuer, account) => $"{issuer} backup codes for {account}";

    /// <summary>The checkbox that confirms the codes are saved.</summary>
    public string SavedConfirm { get; init; } = "I saved these codes somewhere safe";

    /// <summary>The title of the last step.</summary>
    public string DoneTitle { get; init; } = "Two-factor authentication is on";

    /// <summary>The sentence of the last step: method ⇒ text.</summary>
    public Func<TwoFactorSetupMethod, string> DoneDescription { get; init; } = static method => method == TwoFactorSetupMethod.AuthenticatorApp
        ? "From now on, signing in asks for a code from your authenticator app."
        : "From now on, signing in asks for a code we text to your phone.";

    /// <summary>The Back button.</summary>
    public string Back { get; init; } = "Back";

    /// <summary>The Continue button.</summary>
    public string Continue { get; init; } = "Continue";

    /// <summary>The button that sends the first text message.</summary>
    public string SendCode { get; init; } = "Send code";

    /// <summary>The Verify button.</summary>
    public string Verify { get; init; } = "Verify";

    /// <summary>The button on the backup codes step.</summary>
    public string Finish { get; init; } = "Turn on";

    /// <summary>The button on the last step that starts over.</summary>
    public string Restart { get; init; } = "Set up another method";

    /// <summary>Said when a step opens: (step number, total, title) ⇒ "Step 2 of 4: Scan this code with your app".</summary>
    public Func<int, int, string, string> StepAnnouncement { get; init; } = static (step, total, title) => $"Step {step} of {total}: {title}";
}
