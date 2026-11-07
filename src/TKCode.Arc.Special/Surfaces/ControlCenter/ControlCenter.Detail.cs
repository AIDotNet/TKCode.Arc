using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public sealed partial class ControlCenter
{
    private static readonly Easing s_scrimEase = new SplineEasing(0, 0, 0.58, 1);

    private MotionValue<double> _morph = null!;
    private MotionValue<double> _bodyOpacity = null!;
    private MotionValue<double> _scrimOpacity = null!;
    private MotionValue<double> _detailFade = null!;
    private ControlCenterFlight _iconFlight = null!;
    private ControlCenterFlight _titleFlight = null!;
    private ControlCenterDetail _shown;
    private ControlCenterTileHost? _source;
    private Rect _sourceRect;
    private bool _closing;
    private bool _opening;
    private int _draftMode;
    private int _draftMinutes = ControlCenterMath.DialMinimum;
    private ITimer? _clock;

    /// <summary>The detail mounted in the frame (it stays mounted while it closes).</summary>
    internal ControlCenterDetail ShownDetail => _shown;

    /// <summary>The morph's progress (0 at the tile, 1 filling the panel).</summary>
    internal double MorphProgress => _morph.Value;

    internal bool IsClosing => _closing;

    internal int DraftMode => _draftMode;

    internal int DraftMinutes => _draftMinutes;

    internal double BodyOpacity => _bodyOpacity.Value;

    private void BuildMotion()
    {
        _morph = new MotionValue<double>(this, 0, MotionVectors.Double);
        _morph.Changed += _ => ApplyMorph();
        _bodyOpacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _bodyOpacity.Changed += v =>
        {
            _bodyHost.Opacity = v;
            _close.Opacity = v;
        };
        _scrimOpacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _scrimOpacity.Changed += v => _scrim.Opacity = v;
        _detailFade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _detailFade.Changed += _ => ApplyMorph();
        _iconFlight = new ControlCenterFlight(_detailBadge);
        _titleFlight = new ControlCenterFlight(_detailTitle);
    }

    private void OnActiveDetailChanged(ControlCenterDetail old, ControlCenterDetail now)
    {
        if (now == ControlCenterDetail.None)
        {
            if (_shown != ControlCenterDetail.None && !_closing)
            {
                Close(false);
            }
            return;
        }
        if (now != _shown || _closing)
        {
            Open(now, false);
        }
    }

    private ControlCenterTileHost HostOf(ControlCenterDetail detail) =>
        detail == ControlCenterDetail.Focus ? _focusHost : _notifHost;

    private ControlCenterButton ChevronOf(ControlCenterDetail detail) =>
        detail == ControlCenterDetail.Focus ? _focusChevron : _notifChevron;

    /// <summary>Grows the tile of <paramref name="detail"/> into the detail. A detail that is closing turns back from where it is.</summary>
    private void Open(ControlCenterDetail detail, bool keyboard)
    {
        if (detail == ControlCenterDetail.None || _opening)
        {
            return;
        }
        if (_shown == detail && !_closing)
        {
            return;
        }
        _opening = true;
        try
        {
            var resume = _shown == detail && _closing;
            if (_shown != ControlCenterDetail.None && !resume)
            {
                FinishClose();
            }
            _closing = false;
            _shown = detail;
            _source = HostOf(detail);
            if (!resume)
            {
                _bodyHost.Children.Clear();
                _bodyHost.Children.Add(detail == ControlCenterDetail.Focus ? _focusBody : _notifBody);
                if (detail == ControlCenterDetail.Focus)
                {
                    LoadFocusDraft();
                    StartClock();
                }
            }
            UpdateDetailHeader(false);
            AutomationProperties.SetName(_detail, _detailTitle.Text);
            SetInert(true);
            _frame.IsVisible = true;
            _scrim.IsVisible = true;
            _frame.Opacity = 1;
            _detailFade.Jump(1);
            // Lay the detail out once so the tile's box and the shared icon and title can be measured against it.
            UpdateLayout();
            MeasureFlight();
            var level = ArcMotion.GetEffectiveLevel(this);
            if (!resume)
            {
                _morph.Jump(level == ArcMotionLevel.Full ? 0 : 1);
                _bodyOpacity.Jump(0);
            }
            _source.Opacity = 0;
            if (level == ArcMotionLevel.Full)
            {
                _ = _morph.AnimateTo(1, MotionSpec.Spring(ControlCenterMath.LayoutSpring));
                _ = _bodyOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(60)));
            }
            else
            {
                _morph.Jump(1);
                _ = _bodyOpacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(150), ArcEasings.Enter));
            }
            _ = _scrimOpacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(230), s_scrimEase));
            ApplyMorph();
            SetCurrentValue(ActiveDetailProperty, detail);
            _close.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
            RaiseEvent(new ControlCenterDetailEventArgs(DetailOpenedEvent, detail));
        }
        finally
        {
            _opening = false;
        }
    }

    /// <summary>Shrinks the detail back into its tile (unsaved edits are dropped) and returns focus to the chevron.</summary>
    private void Close(bool keyboard)
    {
        if (_shown == ControlCenterDetail.None || _closing)
        {
            return;
        }
        var detail = _shown;
        var focusInside = _frame.IsKeyboardFocusWithin || keyboard;
        _closing = true;
        StopClock();
        SetInert(false);
        SetCurrentValue(ActiveDetailProperty, ControlCenterDetail.None);
        var level = ArcMotion.GetEffectiveLevel(this);
        _ = _scrimOpacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(230), s_scrimEase));
        if (level == ArcMotionLevel.Off)
        {
            FinishClose();
        }
        else if (level == ArcMotionLevel.Full)
        {
            _ = _bodyOpacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(110), s_scrimEase));
            FinishWhenDone(_morph.AnimateTo(0, MotionSpec.Spring(ControlCenterMath.LayoutSpring)));
        }
        else
        {
            // Reduced: no travel; the detail fades out where it is and the tile is simply there again.
            _source?.SetValue(OpacityProperty, 1.0);
            FinishWhenDone(_detailFade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(150), s_scrimEase)));
        }
        if (focusInside)
        {
            ChevronOf(detail).Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        RaiseEvent(new ControlCenterDetailEventArgs(DetailClosedEvent, detail));
    }

    private async void FinishWhenDone(Task<bool> run)
    {
        var done = await run;
        if (done && _closing)
        {
            FinishClose();
        }
    }

    /// <summary>Unmounts the detail: the frame and scrim go, the tile is fully back.</summary>
    private void FinishClose()
    {
        _closing = false;
        _frame.IsVisible = false;
        _frame.Opacity = 1;
        _frame.SetFrame(null);
        _scrimOpacity.Jump(0);
        _scrim.IsVisible = false;
        if (_source is not null)
        {
            _source.Opacity = 1;
        }
        _bodyOpacity.Jump(0);
        _iconFlight.Apply(1);
        _titleFlight.Apply(1);
        _shown = ControlCenterDetail.None;
        _source = null;
    }

    /// <summary>
    /// The tiles and meters under an open detail: Avalonia has no inert, so they leave hit testing, the tab order and the
    /// automation tree by hand while the detail traps focus (its root cycles Tab and Shift+Tab).
    /// </summary>
    private void SetInert(bool inert)
    {
        _main.IsHitTestVisible = !inert;
        KeyboardNavigation.SetTabNavigation(_main, inert ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        AutomationProperties.SetAccessibilityView(_main, inert ? AccessibilityView.Raw : AccessibilityView.Default);
    }

    /// <summary>Measures the tile's box and the shared icon's and title's offsets in the frame, for the current morph.</summary>
    private void MeasureFlight()
    {
        if (_source is null)
        {
            return;
        }
        var tile = _source.Tile;
        if (tile.TranslatePoint(default, _frame) is { } origin)
        {
            _sourceRect = new Rect(origin, tile.Bounds.Size);
        }
        var sourceBadge = _source == _focusHost ? _focusBadge : _notifBadge;
        var sourceTitle = _source == _focusHost ? _focusTitle : _notifTitle;
        _iconFlight.From = Offset(sourceBadge, _detailBadge);
        _titleFlight.From = Offset(sourceTitle, _detailTitle);
    }

    private Vector Offset(Control from, Control to)
    {
        var a = from.TranslatePoint(default, _frame);
        var b = to.TranslatePoint(default, _frame);
        if (a is null || b is null)
        {
            return default;
        }
        // The flight's transform is already applied to the target: measure against its resting place.
        var current = to.RenderTransform is TranslateTransform t ? new Vector(t.X, t.Y) : default;
        return a.Value - (b.Value - current);
    }

    /// <summary>Writes the morph: the frame's box between the tile and the detail, the shared icon and title, and the crossfade.</summary>
    private void ApplyMorph()
    {
        if (_shown == ControlCenterDetail.None)
        {
            return;
        }
        var p = _morph.Value;
        var full = new Rect(_frame.Bounds.Size);
        if (p >= 0.9999 || full.Width <= 0)
        {
            _frame.SetFrame(null);
        }
        else
        {
            var a = _sourceRect;
            _frame.SetFrame(new Rect(
                a.X + (full.X - a.X) * p,
                a.Y + (full.Y - a.Y) * p,
                Math.Max(0, a.Width + (full.Width - a.Width) * p),
                Math.Max(0, a.Height + (full.Height - a.Height) * p)));
        }
        _iconFlight.Apply(p);
        _titleFlight.Apply(p);
        if (_closing && ArcMotion.IsFull(this))
        {
            var (tile, detail) = ControlCenterMath.CloseCrossfade(1 - p);
            if (_source is not null)
            {
                _source.Opacity = tile;
            }
            _frame.Opacity = detail;
        }
        else if (_closing)
        {
            _frame.Opacity = _detailFade.Value;
        }
    }

    private void UpdateDetailHeader(bool animate)
    {
        var labels = Labels;
        if (_shown == ControlCenterDetail.Notifications)
        {
            _detailTitle.Text = labels.NotificationsTitle;
            ShowIcon(_detailIcon, AreNotificationsOn ? ArcIcons.Bell : ArcIcons.BellOff, animate);
        }
        else
        {
            _detailTitle.Text = labels.FocusTitle;
            var mode = _draftMode >= 0 && _draftMode < _focusModes.Count ? _focusModes[_draftMode] : null;
            ShowIcon(_detailIcon, mode?.Icon ?? ControlCenterIcons.Headphones, animate);
        }
    }

    /// <summary>Loads the Focus detail's draft: the running session's mode and length, or the first mode and its default.</summary>
    private void LoadFocusDraft()
    {
        var session = FocusSession;
        var index = 0;
        var minutes = _focusModes.Count > 0 ? _focusModes[0].DefaultMinutes : 30;
        if (session is not null)
        {
            for (var i = 0; i < _focusModes.Count; i++)
            {
                if (string.Equals(_focusModes[i].Id, session.ModeId, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }
            minutes = session.Minutes;
        }
        _draftMode = _focusModes.Count == 0 ? -1 : index;
        _draftMinutes = ControlCenterMath.SnapMinutes(minutes);
        _modeBar.Select(_draftMode, false);
        _dial.Value = _draftMinutes;
        _dial.SyncKnob();
        var description = _draftMode >= 0 ? _focusModes[_draftMode].Description : "";
        ShowText(_modeDescription, description, false, alignment: Avalonia.Layout.HorizontalAlignment.Center);
        UpdateFocusActions();
        UpdateDetailHeader(false);
        UpdateDialTexts();
    }

    private void OnModePicked(int index)
    {
        if (index < 0 || index >= _focusModes.Count)
        {
            return;
        }
        _draftMode = index;
        var mode = _focusModes[index];
        _draftMinutes = ControlCenterMath.SnapMinutes(mode.DefaultMinutes);
        _dial.AnimateTo(_draftMinutes);
        ShowText(_modeDescription, mode.Description, true, alignment: Avalonia.Layout.HorizontalAlignment.Center);
        UpdateDetailHeader(true);
        UpdateDialTexts();
    }

    private void UpdateFocusActions()
    {
        var running = FocusSession is not null;
        _start.IsVisible = !running;
        _end.IsVisible = running;
        _update.IsVisible = running;
    }

    /// <summary>The dial's end time (now plus the shown minutes, recomputed as time passes) and its value text.</summary>
    private void UpdateDialTexts()
    {
        var labels = Labels;
        var now = TimeProvider.GetLocalNow();
        var shown = _dial.ShownNumber > 0 ? _dial.ShownNumber : _draftMinutes;
        _dial.UntilText = labels.Until(labels.FormatTime(ControlCenterMath.Until(now, shown)));
        AutomationProperties.SetHelpText(_dial,
            labels.DialValueText(_dial.IntValue, labels.FormatTime(ControlCenterMath.Until(now, _dial.IntValue))));
    }

    private void StartClock()
    {
        StopClock();
        _clock = TimeProvider.CreateTimer(_ => Dispatcher.UIThread.Post(UpdateDialTexts), null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private void StopClock()
    {
        _clock?.Dispose();
        _clock = null;
    }

    private void OnFocusTileClicked()
    {
        if (FocusSession is null)
        {
            StartFocus();
        }
        else
        {
            EndFocus();
        }
    }

    /// <summary>Start or Update session: the draft becomes the session (ending now plus its minutes) and the detail closes.</summary>
    private void CommitFocus(bool keyboard)
    {
        if (_draftMode >= 0 && _draftMode < _focusModes.Count)
        {
            StartFocus(_focusModes[_draftMode].Id, _draftMinutes);
        }
        Close(keyboard);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _shown != ControlCenterDetail.None && !_closing)
        {
            e.Handled = true;
            Close(true);
            return;
        }
        base.OnKeyDown(e);
    }
}
