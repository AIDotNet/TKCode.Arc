using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The cards a <see cref="SecuritySettings"/> shows; combine them, for example <c>Sections="Password, Sessions"</c>.</summary>
[Flags]
public enum SecuritySettingsSections
{
    /// <summary>No card.</summary>
    None = 0,

    /// <summary>The security checkup (also needs <see cref="SecuritySettings.IsSummaryVisible"/>).</summary>
    Summary = 1,

    /// <summary>The password row and its change form.</summary>
    Password = 2,

    /// <summary>Two-step verification: authenticator app, text message, passkeys and recovery codes.</summary>
    TwoStep = 4,

    /// <summary>The signed-in sessions.</summary>
    Sessions = 8,

    /// <summary>Every card (default).</summary>
    All = Summary | Password | TwoStep | Sessions
}

/// <summary>The kind of device a <see cref="SecuritySession"/> runs on; it picks the row's glyph.</summary>
public enum SecurityDeviceKind
{
    /// <summary>A desktop computer (monitor glyph).</summary>
    Desktop,

    /// <summary>A laptop (laptop glyph).</summary>
    Laptop,

    /// <summary>A phone (smartphone glyph).</summary>
    Phone,

    /// <summary>A tablet (tablet glyph).</summary>
    Tablet,

    /// <summary>Anything else, such as a browser on an unknown device (globe glyph).</summary>
    Other
}

/// <summary>
/// A place where the account is signed in, shown as one row of <see cref="SecuritySettings"/>. Can be written in XAML
/// inside <c>SecuritySettings.Sessions</c>.
/// </summary>
public class SecuritySession
{
    /// <summary>The device's name, such as "MacBook Air".</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>The browser or app, such as "Firefox"; shown after the device name.</summary>
    public string? Client { get; set; }

    /// <summary>Where the device signed in from, such as "Rotterdam, NL".</summary>
    public string? Location { get; set; }

    /// <summary>When the session was last used; shown relative to <see cref="SecuritySettings.Clock"/>.</summary>
    public DateTimeOffset LastActive { get; set; }

    /// <summary>The kind of device, which picks the glyph.</summary>
    public SecurityDeviceKind Kind { get; set; }

    /// <summary>True for the device in use: it is listed first, marked "This device" and cannot be signed out here.</summary>
    public bool IsCurrent { get; set; }
}

/// <summary>Arguments of <see cref="SecuritySettings.PasswordChangeRequested"/>: the passwords, and a way to reject them.</summary>
public sealed class SecurityPasswordChangeEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SecurityPasswordChangeEventArgs(RoutedEvent routedEvent, string currentPassword, string newPassword) : base(routedEvent)
    {
        CurrentPassword = currentPassword;
        NewPassword = newPassword;
    }

    /// <summary>What was typed as the current password (empty when the account had no password).</summary>
    public string CurrentPassword { get; }

    /// <summary>The new password.</summary>
    public string NewPassword { get; }

    /// <summary>
    /// Set it to reject the change: the message appears under the current password field, the form stays open and focus
    /// returns to that field. Leave it null to accept: the form closes and the password row reads "Updated just now".
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Arguments of <see cref="SecuritySettings.SessionSignedOut"/>.</summary>
public sealed class SecuritySessionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SecuritySessionEventArgs(RoutedEvent routedEvent, SecuritySession session) : base(routedEvent)
    {
        Session = session;
    }

    /// <summary>The session that was signed out.</summary>
    public SecuritySession Session { get; }
}

/// <summary>Arguments of <see cref="SecuritySettings.OtherSessionsSignedOut"/>.</summary>
public sealed class SecuritySessionsEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SecuritySessionsEventArgs(RoutedEvent routedEvent, IReadOnlyList<SecuritySession> sessions) : base(routedEvent)
    {
        Sessions = sessions;
    }

    /// <summary>The sessions that were signed out, in the order they were listed.</summary>
    public IReadOnlyList<SecuritySession> Sessions { get; }
}
