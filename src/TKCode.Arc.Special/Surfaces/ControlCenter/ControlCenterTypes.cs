using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A focus mode of a <see cref="ControlCenter"/>: one segment of the mode switch in the Focus detail, with the icon the
/// Focus tile shows while a session in this mode runs, a one-line description under the switch and the session length the
/// dial jumps to when the mode is picked.
/// </summary>
public sealed class ControlCenterFocusMode
{
    /// <summary>A stable id, reported in <see cref="ControlCenterFocusSession.ModeId"/>.</summary>
    public string Id { get; set; } = "";

    /// <summary>The mode's short name, shown in the mode switch and as the Focus tile's title during a session.</summary>
    public string Label { get; set; } = "";

    /// <summary>A 24×24 stroked icon (Lucide geometry, such as <c>ArcIcons.Moon</c>); null shows the Focus icon.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>One short sentence under the mode switch: who or what still gets through in this mode.</summary>
    public string Description { get; set; } = "";

    /// <summary>The session length the dial starts at for this mode, in minutes (5 to 120, rounded to 5; 30 by default).</summary>
    public int DefaultMinutes { get; set; } = 30;
}

/// <summary>
/// A notification channel of a <see cref="ControlCenter"/>: a row with its own switch in the Notifications detail. Its
/// <see cref="IsOn"/> state survives turning all notifications off and on again.
/// </summary>
public sealed class ControlCenterChannel : AvaloniaObject
{
    /// <summary>Whether the channel delivers; the row's switch changes it, and so may your code.</summary>
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<ControlCenterChannel, bool>(nameof(IsOn));

    /// <summary>A stable id, the key of <see cref="ControlCenterState.Channels"/>.</summary>
    public string Id { get; set; } = "";

    /// <summary>The channel's name.</summary>
    public string Label { get; set; } = "";

    /// <summary>An optional second line under the name.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the channel delivers; the row's switch changes it, and so may your code.</summary>
    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }
}

/// <summary>
/// Someone whose messages are always let through during a focus session, shown as an avatar in the footer of the Notifications
/// detail. Without an <see cref="Avatar"/> image the avatar shows the initials on a generated gradient.
/// </summary>
public sealed class ControlCenterPerson
{
    /// <summary>A stable id.</summary>
    public string Id { get; set; } = "";

    /// <summary>The person's name; the first word is used in the footer sentence and the initials come from it.</summary>
    public string Name { get; set; } = "";

    /// <summary>An optional picture, drawn in the 28px circle; null draws the initials.</summary>
    public IImage? Avatar { get; set; }
}

/// <summary>A running focus session: the mode, its length and the moment it ends.</summary>
/// <param name="ModeId">The <see cref="ControlCenterFocusMode.Id"/> of the session's mode.</param>
/// <param name="Minutes">The length chosen on the dial, in minutes (5 to 120, a multiple of 5).</param>
/// <param name="Until">When the session ends: the moment it was started or updated plus <paramref name="Minutes"/>.</param>
public sealed record ControlCenterFocusSession(string ModeId, int Minutes, DateTimeOffset Until);

/// <summary>Everything the panel controls, reported after every change.</summary>
/// <param name="Focus">The running focus session, or null.</param>
/// <param name="NotificationsOn">Whether notifications are on at all.</param>
/// <param name="Channels">Each channel's switch, by <see cref="ControlCenterChannel.Id"/>.</param>
/// <param name="Presenting">Whether presenting mode is on.</param>
/// <param name="Volume">The alert volume, 0 to 100.</param>
/// <param name="TextSize">The text size in percent, 85 to 130 in steps of 5.</param>
public sealed record ControlCenterState(
    ControlCenterFocusSession? Focus,
    bool NotificationsOn,
    IReadOnlyDictionary<string, bool> Channels,
    bool Presenting,
    int Volume,
    int TextSize);

/// <summary>Which detail of a <see cref="ControlCenter"/> is open.</summary>
public enum ControlCenterDetail
{
    /// <summary>No detail: the tiles and meters are showing.</summary>
    None,

    /// <summary>The Focus detail: mode switch, session-length dial and session buttons.</summary>
    Focus,

    /// <summary>The Notifications detail: the master switch, one switch per channel and who is always let through.</summary>
    Notifications
}

/// <summary>What changed in a <see cref="ControlCenter"/>.</summary>
public enum ControlCenterChangeReason
{
    /// <summary>A focus session started (from the tile or Start session).</summary>
    FocusStarted,

    /// <summary>The running session got a new mode or length (Update session).</summary>
    FocusUpdated,

    /// <summary>The session ended (from the tile or End session).</summary>
    FocusEnded,

    /// <summary>Notifications were turned on or off (the tile or the master switch).</summary>
    NotificationsToggled,

    /// <summary>A channel's switch changed.</summary>
    ChannelToggled,

    /// <summary>Presenting was turned on or off.</summary>
    PresentingToggled,

    /// <summary>The alert volume changed (once per whole percent while dragging).</summary>
    VolumeChanged,

    /// <summary>The text size changed (once per step while dragging).</summary>
    TextSizeChanged
}

/// <summary>Arguments of <see cref="ControlCenter.StateChanged"/>.</summary>
public sealed class ControlCenterStateChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ControlCenterStateChangedEventArgs(RoutedEvent routedEvent, ControlCenterState state, ControlCenterChangeReason reason)
        : base(routedEvent)
    {
        State = state;
        Reason = reason;
    }

    /// <summary>The whole state after the change.</summary>
    public ControlCenterState State { get; }

    /// <summary>What changed.</summary>
    public ControlCenterChangeReason Reason { get; }
}

/// <summary>Arguments of <see cref="ControlCenter.DetailOpened"/> and <see cref="ControlCenter.DetailClosed"/>.</summary>
public sealed class ControlCenterDetailEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ControlCenterDetailEventArgs(RoutedEvent routedEvent, ControlCenterDetail detail) : base(routedEvent)
    {
        Detail = detail;
    }

    /// <summary>The detail that opened or closed.</summary>
    public ControlCenterDetail Detail { get; }
}
