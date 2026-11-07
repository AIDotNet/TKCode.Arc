using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class VideoPlayer
{
    #region Pointer on the picture

    private void OnRootPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
        {
            Activity();
        }
    }

    private void OnRootPointerExited(object? sender, PointerEventArgs e)
    {
        // Leaving the player hides the bar at once while playing (no reason to wait for the countdown).
        if (e.Pointer.Type != PointerType.Touch && _seek is not { IsDragging: true })
        {
            Deadlines.Cancel(HideKey);
            _active = false;
            UpdateControls();
        }
    }

    private static bool FromButton(RoutedEventArgs e) =>
        e.Source is Visual visual && (visual is AvaloniaButton || visual.FindAncestorOfType<AvaloniaButton>() is not null);

    private void OnPicturePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || FromButton(e))
        {
            return;
        }
        var point = e.GetCurrentPoint(_stateLayer);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        _root?.Focus(NavigationMethod.Pointer);
        Activity();
        if (e.ClickCount > 2)
        {
            // A third click of a quick series does nothing more.
            e.Handled = true;
            return;
        }
        if (e.ClickCount == 2)
        {
            // Double click: full screen, and the first click's play / pause never happens (spec section 11).
            Deadlines.Cancel(ClickKey);
            ToggleFullScreen();
        }
        else
        {
            Deadlines.Schedule(ClickKey, s_clickWait, OnSingleClick);
        }
        e.Handled = true;
    }

    private void OnSingleClick()
    {
        TogglePlay();
        _stateLayer?.Flash(IsPlaying);
    }

    private void OnPictureReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Handled || e.Pointer.Type != PointerType.Touch || FromButton(e) || _stateLayer is null)
        {
            return;
        }
        e.Handled = true;
        var x = e.GetPosition(_stateLayer).X;
        var width = Math.Max(1, _stateLayer.Bounds.Width);
        var side = x < width / 3 ? -1 : x > width * 2 / 3 ? 1 : 0;
        OnTap(side);
    }

    /// <summary>
    /// A tap on the picture (spec section 11): a single tap shows the bar when hidden and otherwise plays or pauses; a
    /// double tap on the left or right third skips 10 s back or forward, and further taps on that side keep adding 10 s; a
    /// double tap in the middle toggles full screen.
    /// </summary>
    internal void OnTap(int side)
    {
        if (_streakSide != 0 && side == _streakSide)
        {
            _streakSeconds += (int)s_jump.TotalSeconds;
            SkipFromTap(side);
            return;
        }
        if (Deadlines.IsPending(TapKey))
        {
            Deadlines.Cancel(TapKey);
            if (side != 0)
            {
                _streakSide = side;
                _streakSeconds = (int)s_jump.TotalSeconds;
                SkipFromTap(side);
            }
            else
            {
                ToggleFullScreen();
            }
            return;
        }
        Deadlines.Schedule(TapKey, s_clickWait, OnSingleTap);
    }

    private void SkipFromTap(int side)
    {
        SeekBy(TimeSpan.FromSeconds(side * s_jump.TotalSeconds));
        _stateLayer?.ShowSkip(side, _streakSeconds);
        Deadlines.Schedule(TapStreakKey, s_tapStreak, () =>
        {
            _streakSide = 0;
            _streakSeconds = 0;
        });
    }

    private void OnSingleTap()
    {
        if (!_controlsVisible)
        {
            Activity();
            return;
        }
        Activity();
        TogglePlay();
        _stateLayer?.Flash(IsPlaying);
    }

    private void OnStateClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Control source)
        {
            return;
        }
        switch (source.Name)
        {
            case VideoStateLayer.BigPlayPart:
            case VideoStateLayer.ReplayPart:
                Play();
                e.Handled = true;
                break;
            case VideoStateLayer.RetryPart:
                Retry();
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Control bar

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        TogglePlay();
        Activity();
    }

    private void OnMuteClick(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsMutedProperty, !IsMuted);
        Activity();
    }

    private void OnVolumeUserChanged(double value)
    {
        SetCurrentValue(VolumeProperty, value);
        if (value > 0 && IsMuted)
        {
            SetCurrentValue(IsMutedProperty, false);
        }
        Activity();
    }

    private void OnVolumeWheel(double delta) => ChangeVolume(delta);

    private void ChangeVolume(double delta)
    {
        var volume = Math.Clamp(Math.Round((Volume + delta) * 100) / 100, 0, 1);
        SetCurrentValue(VolumeProperty, volume);
        if (delta > 0 && IsMuted)
        {
            SetCurrentValue(IsMutedProperty, false);
        }
        Activity();
    }

    private void OnFullScreenClick(object? sender, RoutedEventArgs e) => ToggleFullScreen();

    #endregion

    #region Seek bar

    private void OnSeekDragging(bool dragging)
    {
        if (_player is null || !_isLoaded)
        {
            return;
        }
        if (dragging)
        {
            // Playback pauses while dragging and resumes afterwards (spec section 11).
            _scrubbing = true;
            _hasStarted = true;
            _scrubWasPlaying = IsPlaying && State is VideoPlaybackState.Playing or VideoPlaybackState.Buffering;
            if (_scrubWasPlaying)
            {
                _player.Pause();
            }
            _lastScrubSeek = long.MinValue;
            UpdateStateLayer();
        }
        else
        {
            Deadlines.Cancel(ScrubKey);
            var target = TimeSpan.FromSeconds(_seek?.Value ?? 0);
            _player.Seek(target, VideoSeekMode.Accurate);
            _scrubbing = false;
            Report(_player.Position);
            if (_scrubWasPlaying)
            {
                _player.Play();
            }
            _scrubWasPlaying = false;
            UpdateSeekValueTextFull();
            HideSeekTip();
        }
        UpdateControls();
    }

    private void OnSeekUserChanged(double seconds)
    {
        Activity();
        if (_seek is { IsDragging: true })
        {
            ShowSeekTip(seconds);
            UpdateTimes(force: false);
            ScrubTo(seconds);
            return;
        }
        Seek(TimeSpan.FromSeconds(seconds));
        UpdateSeekValueTextFull();
    }

    /// <summary>A fast seek while dragging, at most one per 100 ms; the last position waits for the next slot.</summary>
    private void ScrubTo(double seconds)
    {
        if (_player is null)
        {
            return;
        }
        var clock = Clock;
        var now = clock.GetTimestamp();
        var interval = (long)(s_scrubInterval.TotalSeconds * clock.TimestampFrequency);
        if (_lastScrubSeek == long.MinValue || now - _lastScrubSeek >= interval)
        {
            _lastScrubSeek = now;
            Deadlines.Cancel(ScrubKey);
            _player.Seek(TimeSpan.FromSeconds(seconds), VideoSeekMode.Fast);
            return;
        }
        var wait = TimeSpan.FromSeconds((interval - (now - _lastScrubSeek)) / (double)clock.TimestampFrequency);
        Deadlines.Schedule(ScrubKey, wait, () =>
        {
            if (_seek is { IsDragging: true } seek && _player is not null)
            {
                _lastScrubSeek = Clock.GetTimestamp();
                _player.Seek(TimeSpan.FromSeconds(seek.Value), VideoSeekMode.Fast);
            }
        });
    }

    private void OnSeekFocus(object? sender, FocusChangedEventArgs e) => UpdateSeekValueTextFull();

    private void OnSeekHover(double? seconds)
    {
        if (seconds is { } value && _controlsVisible)
        {
            ShowSeekTip(_seek is { IsDragging: true } seek ? seek.Value : value);
        }
        else if (_seek is not { IsDragging: true })
        {
            HideSeekTip();
        }
    }

    /// <summary>The time tip above the seek bar: the time under the pointer (or the thumb while dragging) and the chapter title.</summary>
    private void ShowSeekTip(double seconds)
    {
        if (_seekTip is null || _seekTipText is null || _seekTipHost is null || _seek is null)
        {
            return;
        }
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        var text = VideoText.Clock(time);
        if (ChapterAt(time) is { Length: > 0 } chapter)
        {
            text = chapter + " · " + text;
        }
        _seekTipText.Text = text;
        _seekTip.IsVisible = true;
        _seekTip.Measure(Size.Infinity);
        var tip = _seekTip.DesiredSize;
        var x = _seek.OffsetOf(seconds);
        var origin = _seek.TranslatePoint(new Point(x, 0), _seekTipHost) ?? new Point(x, 0);
        var hostWidth = _seekTipHost.Bounds.Width;
        var left = Math.Clamp(origin.X - tip.Width / 2, 0, Math.Max(0, hostWidth - tip.Width));
        Canvas.SetLeft(_seekTip, left);
        Canvas.SetTop(_seekTip, origin.Y - tip.Height - 8);
    }

    private void HideSeekTip()
    {
        if (_seekTip is not null)
        {
            _seekTip.IsVisible = false;
        }
    }

    #endregion

    #region Keyboard

    /// <summary>
    /// The player's keys (spec section 11) anywhere inside it, unless a control already handled them (buttons take Space,
    /// sliders take the arrows) or focus is in text input or a menu.
    /// </summary>
    private void OnRootKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Source is TextBox || (e.Source is Visual visual && visual.FindAncestorOfType<MenuSurface>(includeSelf: true) is not null))
        {
            return;
        }
        var modifiers = e.KeyModifiers;
        if ((modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return;
        }
        var shift = (modifiers & KeyModifiers.Shift) != 0;
        var handled = true;
        switch (e.Key)
        {
            case Key.OemPeriod when shift:
                StepRate(1);
                break;
            case Key.OemComma when shift:
                StepRate(-1);
                break;
            case var _ when shift:
                handled = false;
                break;
            case Key.Space:
            case Key.K:
                TogglePlay();
                break;
            case Key.Left:
                SeekBy(-SeekStep);
                break;
            case Key.Right:
                SeekBy(SeekStep);
                break;
            case Key.J:
                SeekBy(-s_jump);
                break;
            case Key.L:
                SeekBy(s_jump);
                break;
            case Key.Up:
                ChangeVolume(0.05);
                break;
            case Key.Down:
                ChangeVolume(-0.05);
                break;
            case Key.M:
                SetCurrentValue(IsMutedProperty, !IsMuted);
                break;
            case Key.F:
                ToggleFullScreen();
                break;
            case Key.Escape when IsFullScreen:
                ExitFullScreen();
                break;
            case Key.C:
                ToggleCaptions();
                break;
            case Key.Home:
                Seek(TimeSpan.Zero);
                break;
            case Key.End:
                Seek(Duration);
                break;
            case >= Key.D0 and <= Key.D9:
                Seek(Duration * ((e.Key - Key.D0) / 10.0));
                break;
            case >= Key.NumPad0 and <= Key.NumPad9:
                Seek(Duration * ((e.Key - Key.NumPad0) / 10.0));
                break;
            default:
                handled = false;
                break;
        }
        if (handled)
        {
            e.Handled = true;
            Activity();
        }
    }

    #endregion
}
