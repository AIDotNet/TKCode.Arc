using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One session's row in <see cref="SecuritySettings"/>: a <see cref="SettingsRow"/> inside a <see cref="PresenceHost"/>
/// that collapses its height as the row leaves. Created once per session and reused while the session is listed.
/// </summary>
internal sealed class SecuritySettingsSessionView
{
    private readonly Action<SecuritySettingsSessionView> _signOut;
    private readonly Action<SecuritySettingsSessionView> _exited;
    private Button? _button;
    private Badge? _badge;

    public SecuritySettingsSessionView(SecuritySession session, Action<SecuritySettingsSessionView> signOut, Action<SecuritySettingsSessionView> exited)
    {
        Session = session;
        _signOut = signOut;
        _exited = exited;
        Row = new SettingsRow();
        Host = new PresenceHost { Collapse = PresenceCollapse.Height, Mode = PresenceMode.Fade, Child = Row };
        Host.Exited += OnExited;
    }

    public SecuritySession Session { get; }

    public PresenceHost Host { get; }

    public SettingsRow Row { get; }

    /// <summary>The sign-out button (null for the current session until it is needed).</summary>
    public Button? SignOutButton => Session.IsCurrent ? null : _button;

    /// <summary>The row has started to leave.</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>The row has left and the session was taken off the list.</summary>
    public bool IsFinished { get; set; }

    /// <summary>The "sign out other sessions" run this row leaves with, if any.</summary>
    public SecuritySettingsSignOutBatch? Batch { get; set; }

    /// <summary>Refreshes the texts, glyph and trailing control from the session, the labels and "now".</summary>
    public void Update(SecuritySettingsLabels labels, DateTimeOffset now)
    {
        var session = Session;
        Row.Icon = SecuritySettingsGlyphs.For(session.Kind);
        Row.Header = string.IsNullOrWhiteSpace(session.Client) ? session.Device : $"{session.Device} · {session.Client}";
        var activity = session.IsCurrent ? labels.ActiveNow : SettingsFormat.Relative(session.LastActive, now, labels.Culture);
        // In a narrow row the time wraps as a whole ("4 days ago"), after the separator, never inside it.
        activity = activity.Replace(' ', ' ');
        Row.Description = string.IsNullOrWhiteSpace(session.Location) ? activity : $"{session.Location} · {activity}";
        if (session.IsCurrent)
        {
            _badge ??= new Badge { Tone = BadgeTone.Success, Size = BadgeSize.Small };
            _badge.Content = labels.ThisDevice;
            AutomationProperties.SetName(_badge, labels.ThisDevice);
            Row.Content = _badge;
        }
        else
        {
            if (_button is null)
            {
                _button = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
                _button.Click += OnSignOutClick;
            }
            _button.Content = labels.SignOut;
            AutomationProperties.SetName(_button, labels.SignOutSession(session.Device));
            Row.Content = _button;
        }
    }

    /// <summary>Starts the exit: the button stops taking input and the host fades and collapses the row.</summary>
    public void Leave()
    {
        IsLeaving = true;
        if (_button is not null)
        {
            _button.IsEnabled = false;
        }
        Host.IsPresent = false;
    }

    /// <summary>Stops listening (the session is no longer listed).</summary>
    public void Detach()
    {
        Host.Exited -= OnExited;
        if (_button is not null)
        {
            _button.Click -= OnSignOutClick;
        }
    }

    private void OnSignOutClick(object? sender, RoutedEventArgs e) => _signOut(this);

    private void OnExited(object? sender, EventArgs e) => _exited(this);
}

/// <summary>One "sign out other sessions" run: the sessions it signs out and how many rows are still leaving.</summary>
internal sealed class SecuritySettingsSignOutBatch
{
    public SecuritySettingsSignOutBatch(IReadOnlyList<SecuritySession> sessions)
    {
        Sessions = sessions;
        Remaining = sessions.Count;
    }

    public IReadOnlyList<SecuritySession> Sessions { get; }

    public int Remaining { get; set; }
}
