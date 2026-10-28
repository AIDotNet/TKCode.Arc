using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// Settings → Security, uiarc.dev's security settings block with its default data (the "Fieldnote" account): a two-factor
/// setup wizard (scan, verify, save backup codes, on), a password form with a strength meter, and the active sessions
/// with Revoke and hold-to-sign-out. Our <see cref="SecuritySettings"/> block follows an older Arc version of this block
/// (toggles, passkeys, a protection score), so the view is composed here from the same controls the Arc block uses.
/// </summary>
internal sealed partial class DemoSettingsSecurity : Border
{
    private const string Account = "maya@fieldnote.example";
    private const string SetupKey = "N4XG K3LQ 7RPM 2VWD Y6HT A5CJ";
    private const string DemoCode = "246810";

    private static readonly string[] s_backupCodes =
        ["7kq2-m9xp", "c4tn-8wzr", "p3hd-6yvb", "x9fm-2rkt", "b5wl-4qne", "r8jc-7tgs", "m2vx-9dph", "t6nz-3kwf", "h4gy-5bmq", "w7rs-8cjd"];

    private sealed record Session(string Id, Geometry Icon, string Device, string Client, string City, string Seen, bool IsCurrent = false, string? Flag = null);

    private static readonly Session[] s_sessions =
    [
        new("mbp", ArcIcons.Laptop, "MacBook Pro", "Chrome 129", "San Francisco, US", "Active now", IsCurrent: true),
        new("iphone", ArcIcons.Smartphone, "iPhone 15 Pro", "Fieldnote for iOS", "San Francisco, US", "2 hours ago"),
        new("windows", ArcIcons.Monitor, "Windows desktop", "Edge 128", "Austin, US", "Yesterday at 4:12 PM"),
        new("ipad", DemoSettingsIcons.Tablet, "iPad Air", "Safari 18", "Lisbon, PT", "Sep 14"),
        new("thinkpad", ArcIcons.Laptop, "ThinkPad X1", "Firefox 130", "Berlin, DE", "Sep 9", Flag: "New location")
    ];

    private readonly TextBlock _live = DemoSettingsUi.LiveRegion();
    private readonly List<Control> _sectionPads = [];
    private Border _header = null!;
    private TextBlock _lastTitle = null!;

    public DemoSettingsSecurity()
    {
        MaxWidth = 880;
        HorizontalAlignment = HorizontalAlignment.Center;
        BorderThickness = new Thickness(1);
        ClipToBounds = true;
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);

        var head = new StackPanel { Spacing = 4 };
        head.Children.Add(DemoUi.Text("Security", ArcDesignTokenKind.Foreground, 15, FontWeight.Medium));
        head.Children.Add(DemoUi.Text($"Control how you sign in to Fieldnote as {Account}.", ArcDesignTokenKind.TextSecondary));
        _header = new Border { Padding = new Thickness(20, 16), Child = head };

        var column = new StackPanel();
        column.Children.Add(_header);
        column.Children.Add(BuildTwoFactor());
        column.Children.Add(BuildPassword());
        column.Children.Add(BuildSessions());
        column.Children.Add(_live);
        Child = column;
        SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);
    }

    private void Announce(string text) => _live.Text = text;

    /// <summary>The block is <c>min(100%, 880px)</c> wide whatever its content asks for.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MaxWidth : Math.Min(availableSize.Width, MaxWidth);
        var size = base.MeasureOverride(availableSize.WithWidth(width));
        return new Size(width, size.Height);
    }

    /// <summary>At 560 or narrower the header and sections tighten to 14 16; the scan step stacks; the codes take two columns.</summary>
    private void ApplyWidth(double width)
    {
        var pad = width <= 560 ? new Thickness(16, 14) : new Thickness(20, 16);
        _header.Padding = pad;
        foreach (var section in _sectionPads)
        {
            ((Border)section).Padding = pad;
        }
        _isNarrow = width <= 560;
        _codeColumns = width <= 640 ? 2 : 5;
        ApplyStageWidth();
    }

    /// <summary>A section: a top border, a head (title and text left, an aside right) and the body under it.</summary>
    private Border Section(string title, Control description, Control? aside, Control body, Control? titleAccessory = null)
    {
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        _lastTitle = DemoUi.Text(title, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
        titleRow.Children.Add(_lastTitle);
        if (titleAccessory is not null)
        {
            titleRow.Children.Add(titleAccessory);
        }
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(titleRow);
        text.Children.Add(description);
        var head = new DockPanel();
        if (aside is not null)
        {
            aside.VerticalAlignment = VerticalAlignment.Top;
            aside.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(aside, Dock.Right);
            head.Children.Add(aside);
        }
        head.Children.Add(text);
        var stack = new StackPanel();
        stack.Children.Add(head);
        stack.Children.Add(body);
        var section = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 16), Child = stack };
        ArcTokens.Bind(section, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        _sectionPads.Add(section);
        return section;
    }

    /// <summary>Content that grows in (height and opacity) while it shows.</summary>
    private static PresenceHost Expander(Control child, bool isPresent)
        => new() { Mode = PresenceMode.Fade, Collapse = PresenceCollapse.Height, IsPresent = isPresent, Child = child };

    private static Button SmallButton(string label, ButtonVariant variant, Geometry? icon = null) => DemoUi.Button(label, variant, icon);

    #region Password

    private ArcContentSwap _passwordNote = null!;
    private Button _passwordToggle = null!;
    private PresenceHost _passwordForm = null!;
    private PasswordField _current = null!;
    private PasswordStrength _new = null!;
    private Checkbox _signOutOthers = null!;
    private Button _update = null!;
    private bool _passwordSaving;

    private Control BuildPassword()
    {
        _passwordNote = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, HorizontalAlignment = HorizontalAlignment.Left };
        SetPasswordNote("Last changed March 4, 2026");
        _passwordToggle = SmallButton("Change password", ButtonVariant.Secondary);
        _passwordToggle.Click += (_, _) =>
        {
            if (_passwordForm.IsPresent)
            {
                ClosePasswordForm();
                Announce("Password change canceled");
            }
            else
            {
                _passwordForm.IsPresent = true;
                _passwordToggle.Variant = ButtonVariant.Ghost;
                _passwordToggle.Content = "Cancel";
                Dispatcher.UIThread.Post(() => _current.Focus(), DispatcherPriority.Loaded);
            }
        };

        _current = new PasswordField { Label = "Current password", Size = ControlSize.Small };
        _current.TextChanged += (_, _) => _current.ErrorMessage = null;
        _new = new PasswordStrength { Label = "New password", Size = ControlSize.Small };
        _new.TextChanged += (_, _) => _new.ErrorMessage = null;
        _signOutOthers = new Checkbox { Content = "Sign out of other sessions", VerticalAlignment = VerticalAlignment.Center };
        _update = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, Content = "Update password" };
        _update.Click += async (_, _) => await UpdatePasswordAsync();
        var foot = new DockPanel();
        DockPanel.SetDock(_update, Dock.Right);
        foot.Children.Add(_update);
        foot.Children.Add(_signOutOthers);

        var form = new StackPanel { Spacing = 16, MaxWidth = 480, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 16, 0, 4) };
        form.Children.AddRange([_current, _new, foot]);
        _passwordForm = Expander(form, false);
        return Section("Password", _passwordNote, _passwordToggle, _passwordForm);
    }

    private void SetPasswordNote(string text) => _passwordNote.Content = DemoUi.Text(text, ArcDesignTokenKind.TextSecondary);

    private void ClosePasswordForm()
    {
        _passwordForm.IsPresent = false;
        _passwordToggle.Variant = ButtonVariant.Secondary;
        _passwordToggle.Content = "Change password";
        _current.ErrorMessage = null;
        _new.ErrorMessage = null;
    }

    private async Task UpdatePasswordAsync()
    {
        if (_passwordSaving)
        {
            return;
        }
        var current = _current.Text ?? string.Empty;
        var next = _new.Text ?? string.Empty;
        if (current.Length == 0)
        {
            _current.ErrorMessage = "Enter your current password";
            _current.Focus();
            return;
        }
        string? error = next.Length == 0 ? "Choose a new password"
            : next == current ? "Use a password that differs from your current one"
            : _new.Level < 3 ? "Meet at least three of the requirements below"
            : null;
        if (error is not null)
        {
            _new.ErrorMessage = error;
            _new.Focus();
            return;
        }

        _passwordSaving = true;
        _passwordToggle.IsEnabled = false;
        _update.IsLoading = true;
        await Task.Delay(900);
        _update.IsLoading = false;
        _update.Content = IconLabel(ArcIcons.Check, "Password updated");
        var others = OpenOtherSessions().ToList();
        if (_signOutOthers.IsChecked == true && others.Count > 0)
        {
            RevokeAll(others);
            Announce($"Password updated and signed out of {others.Count} other {(others.Count == 1 ? "session" : "sessions")}");
        }
        else
        {
            Announce("Password updated");
        }
        await Task.Delay(1400);
        ClosePasswordForm();
        _current.Text = string.Empty;
        _new.Text = string.Empty;
        _signOutOthers.IsChecked = false;
        _update.Content = "Update password";
        _passwordToggle.IsEnabled = true;
        _passwordSaving = false;
        SetPasswordNote("Last changed just now");
    }

    private static Control IconLabel(Geometry icon, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new ArcGlyph { Data = icon, Size = 14, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    #endregion

    #region Sessions

    private readonly Dictionary<string, PresenceHost> _sessionRows = new();
    private readonly HashSet<string> _revoked = [];
    private AnimatedCounter _sessionCount = null!;
    private TextBlock _sessionsHeading = null!;
    private PresenceHost _sessionsFoot = null!;
    private PresenceHost _onlyThisDevice = null!;
    private ArcContentSwap _holdNote = null!;
    private HoldToConfirm _signOutAll = null!;

    private Control BuildSessions()
    {
        _sessionCount = new AnimatedCounter { Value = s_sessions.Length, FontSize = 14, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        ArcTokens.Bind(_sessionCount, TemplatedControl.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        var description = DemoUi.Text("Devices signed in to your account in the last 30 days.", ArcDesignTokenKind.TextSecondary);

        var list = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var session in s_sessions)
        {
            var row = SessionRow(session);
            _sessionRows[session.Id] = row;
            list.Children.Add(row);
        }

        _holdNote = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, VerticalAlignment = VerticalAlignment.Center };
        SetHoldNote("You stay signed in on this device");
        _signOutAll = new HoldToConfirm { Label = "Sign out of other sessions", ConfirmedLabel = "Signed out", Icon = ArcIcons.LogOut };
        _signOutAll.HoldingChanged += (_, e) =>
        {
            var count = OpenOtherSessions().Count();
            SetHoldNote(e.IsHolding ? $"Keep holding to sign out {count} {(count == 1 ? "device" : "devices")}" : "You stay signed in on this device");
        };
        _signOutAll.Confirmed += (_, _) =>
        {
            var others = OpenOtherSessions().ToList();
            RevokeAll(others);
            Announce($"Signed out of {others.Count} other {(others.Count == 1 ? "session" : "sessions")}");
        };
        var foot = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(_signOutAll, Dock.Right);
        foot.Children.Add(_signOutAll);
        foot.Children.Add(_holdNote);
        _sessionsFoot = Expander(foot, true);
        var only = DemoUi.Text("Only this device is signed in.", ArcDesignTokenKind.TextMuted);
        only.Margin = new Thickness(0, 12, 0, 0);
        _onlyThisDevice = Expander(only, false);

        var body = new StackPanel();
        body.Children.AddRange([list, _sessionsFoot, _onlyThisDevice]);
        var section = Section("Active sessions", description, null, body, _sessionCount);
        _sessionsHeading = _lastTitle;
        _sessionsHeading.Focusable = true;
        return section;
    }

    private void SetHoldNote(string text) => _holdNote.Content = DemoUi.Text(text, ArcDesignTokenKind.TextMuted);

    private PresenceHost SessionRow(Session session)
    {
        var icon = DemoUi.Glyph(session.Icon, 16);
        icon.VerticalAlignment = VerticalAlignment.Center;
        icon.Margin = new Thickness(0, 0, 12, 0);
        var name = new WrapPanel { ItemSpacing = 6, LineSpacing = 4 };
        name.Children.Add(DemoUi.Text(session.Device, ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium));
        if (session.IsCurrent)
        {
            name.Children.Add(new Badge { Content = "This device", Size = BadgeSize.Small, VerticalAlignment = VerticalAlignment.Center });
        }
        if (session.Flag is { } flag)
        {
            name.Children.Add(new Badge { Content = flag, Size = BadgeSize.Small, Tone = BadgeTone.Warning, VerticalAlignment = VerticalAlignment.Center });
        }
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8) };
        text.Children.Add(name);
        text.Children.Add(DemoUi.Text($"{session.Client} · {session.City} · {session.Seen}", ArcDesignTokenKind.TextMuted, 12));

        var row = new DockPanel { MinHeight = 52 };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        if (!session.IsCurrent)
        {
            var revoke = SmallButton("Revoke", ButtonVariant.Ghost);
            AutomationProperties.SetName(revoke, $"Revoke {session.Device} session in {session.City}");
            revoke.VerticalAlignment = VerticalAlignment.Center;
            revoke.Click += (_, _) =>
            {
                var next = NextRevoke(session.Id);
                Revoke(session);
                Announce($"Signed out of {session.Device} in {session.City}");
                Dispatcher.UIThread.Post(() => (next ?? (Control)_sessionsHeading).Focus(), DispatcherPriority.Loaded);
            };
            DockPanel.SetDock(revoke, Dock.Right);
            row.Children.Add(revoke);
        }
        row.Children.Add(text);
        return Expander(row, true);
    }

    private IEnumerable<Session> OpenOtherSessions() => s_sessions.Where(session => !session.IsCurrent && !_revoked.Contains(session.Id));

    private void Revoke(Session session)
    {
        _revoked.Add(session.Id);
        _sessionRows[session.Id].IsPresent = false;
        RefreshSessions();
    }

    /// <summary>Every row in <paramref name="sessions"/> collapses, one after the other.</summary>
    private async void RevokeAll(IReadOnlyList<Session> sessions)
    {
        foreach (var session in sessions)
        {
            Revoke(session);
            await Task.Delay(60);
        }
    }

    private void RefreshSessions()
    {
        _sessionCount.Value = s_sessions.Length - _revoked.Count;
        var anyOther = OpenOtherSessions().Any();
        _sessionsFoot.IsPresent = anyOther;
        _onlyThisDevice.IsPresent = !anyOther;
        _signOutOthers.IsEnabled = anyOther;
    }

    private Control? NextRevoke(string id)
    {
        var index = Array.FindIndex(s_sessions, session => session.Id == id);
        var open = s_sessions.Select((session, i) => (session, i)).Where(item => !item.session.IsCurrent && !_revoked.Contains(item.session.Id) && item.session.Id != id).ToList();
        var target = open.FirstOrDefault(item => item.i > index).session ?? open.LastOrDefault(item => item.i < index).session;
        return target is null ? null : _sessionRows[target.Id].GetVisualDescendantsOfType<Button>().FirstOrDefault();
    }

    #endregion
}
