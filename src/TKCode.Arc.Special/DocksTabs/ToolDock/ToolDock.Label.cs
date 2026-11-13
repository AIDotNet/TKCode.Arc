using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class ToolDock
{
    /// <summary>The first mouse hover waits this long before the label appears (measured 400–440 ms).</summary>
    internal static TimeSpan HoverDelay { get; set; } = TimeSpan.FromMilliseconds(400);

    /// <summary>Keyboard focus shows the label sooner (measured about 200 ms).</summary>
    internal static TimeSpan FocusDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>After the pointer leaves the dock the label stays this long before it hides (measured 180 ms).</summary>
    internal static TimeSpan LeaveDelay { get; set; } = TimeSpan.FromMilliseconds(180);

    /// <summary>After the focus leaves the dock the label hides this soon (measured about 100 ms).</summary>
    internal static TimeSpan FocusLeaveDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>For this long after the pointer left, coming back shows the label at once (warm: 800 ms yes, 900 ms no).</summary>
    internal static TimeSpan WarmWindow { get; set; } = TimeSpan.FromMilliseconds(800);

    private ToolDockLabel? _label;
    private Control? _hoverTarget;
    private Control? _focusTarget;
    private Control? _labelTarget;
    private Control? _suppressed;
    private DispatcherTimer? _showTimer;
    private DispatcherTimer? _hideTimer;
    private long _warmUntil;

    /// <summary>The label part once it was needed.</summary>
    internal ToolDockLabel? LabelPart => _label;

    /// <summary>The slot or member the label shows (or is about to show).</summary>
    internal Control? LabelTarget => _labelTarget;

    private bool IsWarm => (_label?.IsShown ?? false) || Stopwatch.GetTimestamp() < _warmUntil;

    /// <summary>The mouse entered or left a slot or a tray member.</summary>
    internal void OnHover(Control target, bool entered)
    {
        if (entered)
        {
            if (ReferenceEquals(target, _suppressed) || _drag is not null)
            {
                return;
            }
            _hoverTarget = target;
            StopTimer(ref _hideTimer);
            RequestLabel(target, HoverDelay);
            return;
        }
        if (ReferenceEquals(target, _suppressed))
        {
            _suppressed = null;
        }
        if (!ReferenceEquals(_hoverTarget, target))
        {
            return;
        }
        _hoverTarget = null;
        if (_label is { IsShown: true })
        {
            // Only a label that was up stays warm; a hover that left before the label appeared starts cold again.
            _warmUntil = Stopwatch.GetTimestamp() + (long)(WarmWindow.TotalSeconds * Stopwatch.Frequency);
        }
        if (_focusTarget is { } focus && focus.IsKeyboardFocusWithin && CanLabel(focus))
        {
            ShowLabel(focus);
            return;
        }
        StartTimer(ref _hideTimer, LeaveDelay, OnHideTimer);
    }

    /// <summary>The keyboard moved the focus to a slot or a tray member.</summary>
    private void OnKeyboardTarget(Control target)
    {
        _focusTarget = target;
        StopTimer(ref _hideTimer);
        if (_hoverTarget is not null && _label is { IsShown: true })
        {
            // The mouse keeps the label where it is.
            return;
        }
        RequestLabel(target, FocusDelay);
    }

    /// <summary>A slot or member lost the focus: if it left the dock, the label goes after a moment.</summary>
    internal void OnFocusLeaving()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsKeyboardFocusWithin)
            {
                return;
            }
            _focusTarget = null;
            if (_hoverTarget is null)
            {
                StopTimer(ref _showTimer);
                StartTimer(ref _hideTimer, FocusLeaveDelay, OnHideTimer);
            }
        });
    }

    private void RequestLabel(Control target, TimeSpan coldDelay)
    {
        if (!CanLabel(target))
        {
            if (ReferenceEquals(_labelTarget, target) || _label is { IsShown: true })
            {
                HideLabel();
            }
            return;
        }
        if (IsWarm)
        {
            StopTimer(ref _showTimer);
            ShowLabel(target);
            return;
        }
        _labelTarget = target;
        if (_showTimer is not null)
        {
            // The first delay counts from the moment the pointer reached the dock, not from each slot it crosses.
            return;
        }
        StartTimer(ref _showTimer, coldDelay, () =>
        {
            _showTimer = null;
            if ((_hoverTarget ?? _focusTarget) is { } current && CanLabel(current))
            {
                ShowLabel(current);
            }
        });
    }

    /// <summary>Whether <paramref name="target"/> may show the label now (not a group whose tray is open).</summary>
    private bool CanLabel(Control target) => target switch
    {
        ToolDockItem slot => !slot.IsTrayOpen && slot.IsVisible && _drag is null,
        ToolDockTrayButton button => _openGroup is not null && ReferenceEquals(button.Member.Group, _openGroup),
        _ => false
    };

    private void ShowLabel(Control target)
    {
        if (_stage is null)
        {
            return;
        }
        _labelTarget = target;
        var label = EnsureLabel();
        string text;
        string? shortcut;
        Rect rect;
        double bottom;
        if (target is ToolDockTrayButton button && _tray is { } tray)
        {
            text = button.Member.DisplayName;
            shortcut = button.Member.Shortcut;
            rect = new Rect(tray.Placement.X + button.Bounds.X, tray.Placement.Y + button.Bounds.Y, button.Bounds.Width, button.Bounds.Height);
            bottom = tray.Placement.Y - label.Gap;
        }
        else if (target is ToolDockItem slot)
        {
            text = slot.LabelText;
            shortcut = slot.LabelShortcut;
            rect = SlotRect(slot);
            bottom = -label.Gap;
        }
        else
        {
            return;
        }
        label.ShowAt(text, shortcut, rect.Center.X, bottom);
    }

    /// <summary>Hides the label (it stays warm as long as <see cref="_warmUntil"/> allows).</summary>
    private void HideLabel()
    {
        StopTimer(ref _showTimer);
        _label?.Hide();
    }

    private void OnHideTimer()
    {
        if (_hoverTarget is not null || (_focusTarget?.IsKeyboardFocusWithin ?? false))
        {
            return;
        }
        HideLabel();
        _labelTarget = null;
    }

    /// <summary>A click hides the label at once and cools it: the next hover waits again.</summary>
    private void CoolLabel(Control? clicked)
    {
        StopTimer(ref _showTimer);
        StopTimer(ref _hideTimer);
        _warmUntil = 0;
        _suppressed = clicked;
        _hoverTarget = null;
        _label?.Hide();
    }

    /// <summary>A slot's text changed: a label showing it follows.</summary>
    internal void OnItemTextChanged(ToolDockItem slot)
    {
        if (ReferenceEquals(_labelTarget, slot) && _label is { IsShown: true })
        {
            ShowLabel(slot);
        }
    }

    private void ForgetLabelTarget(Control target)
    {
        if (ReferenceEquals(_hoverTarget, target))
        {
            _hoverTarget = null;
        }
        if (ReferenceEquals(_focusTarget, target))
        {
            _focusTarget = null;
        }
        if (ReferenceEquals(_labelTarget, target))
        {
            _labelTarget = null;
            HideLabel();
        }
    }

    private ToolDockLabel EnsureLabel()
    {
        if (_label is not null)
        {
            return _label;
        }
        _label = new ToolDockLabel();
        _stage!.Children.Add(_label);
        return _label;
    }

    private void StopLabelTimers()
    {
        StopTimer(ref _showTimer);
        StopTimer(ref _hideTimer);
    }

    private static void StartTimer(ref DispatcherTimer? timer, TimeSpan delay, Action tick)
    {
        StopTimer(ref timer);
        if (delay <= TimeSpan.Zero)
        {
            tick();
            return;
        }
        var created = new DispatcherTimer { Interval = delay };
        created.Tick += (_, _) =>
        {
            created.Stop();
            tick();
        };
        timer = created;
        created.Start();
    }

    private static void StopTimer(ref DispatcherTimer? timer)
    {
        timer?.Stop();
        timer = null;
    }
}
