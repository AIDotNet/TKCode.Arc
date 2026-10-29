using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile;

/// <summary>What <see cref="Toast"/> does with the toasts already showing when a new one arrives.</summary>
public enum ToastPolicy
{
    /// <summary>Toasts stack, newest at the bottom; beyond <see cref="Toast.MaxVisible"/> the oldest leaves.</summary>
    Stack,

    /// <summary>One at a time: a new toast sends the one showing away.</summary>
    Replace,
}

/// <summary>
/// Transient messages near the bottom of the page. Put one <see cref="Toast"/> in <see cref="Page.Overlay"/> and call
/// <see cref="Show"/>: the message slides up and fades in above the page's footer and bottom safe area, stays for
/// <see cref="DefaultDuration"/> and leaves by itself. A finger on a toast holds its countdown; a swipe to either side
/// sends it away. <see cref="ToastPolicy"/> chooses between a short stack and one at a time. The host does not catch
/// touches except on the toasts themselves, so the page under it stays usable.
/// <para>
/// Several overlays (a toast, a dialog, a sheet) share the one <see cref="Page.Overlay"/> slot: put them together in a
/// <c>Grid</c> with the toast last, so it shows above the others.
/// </para>
/// <para>
/// Each toast's presence and swipe offset are <see cref="MotionValue{T}"/>s rather than ControlTheme transitions: a swipe
/// writes the offset on every move and a release hands the finger's velocity to the spring. The countdown is a
/// <see cref="MotionTimeline"/>, so it can be paused and still ends on time. Under reduced motion a toast only fades,
/// and with motion off it appears and disappears at once.
/// </para>
/// </summary>
[TemplatePart("PART_Stack", typeof(StackPanel))]
public class Toast : TemplatedControl
{
    /// <summary>A toast starts this far below its place and rises while it fades in.</summary>
    private const double Rise = 16;

    /// <summary>A swipe past this distance (px) sends the toast away.</summary>
    private const double SwipeDistance = 72;

    /// <summary>A swipe faster than this (px/s) sends the toast away from anywhere.</summary>
    private const double SwipeVelocity = 600;

    /// <summary>How toasts that are showing are treated when a new one arrives: <see cref="ToastPolicy.Stack"/>.</summary>
    public static readonly StyledProperty<ToastPolicy> PolicyProperty =
        AvaloniaProperty.Register<Toast, ToastPolicy>(nameof(Policy));

    /// <summary>The most toasts shown at once under <see cref="ToastPolicy.Stack"/>: 3.</summary>
    public static readonly StyledProperty<int> MaxVisibleProperty =
        AvaloniaProperty.Register<Toast, int>(nameof(MaxVisible), 3);

    /// <summary>How long a toast stays when <see cref="Show"/> is not given a duration: 3 seconds. <see cref="TimeSpan.Zero"/> keeps it until dismissed.</summary>
    public static readonly StyledProperty<TimeSpan> DefaultDurationProperty =
        AvaloniaProperty.Register<Toast, TimeSpan>(nameof(DefaultDuration), TimeSpan.FromSeconds(3));

    /// <summary>Space between the toasts and whatever is below them: the page's footer and bottom safe area plus this value: 16.</summary>
    public static readonly StyledProperty<double> BottomOffsetProperty =
        AvaloniaProperty.Register<Toast, double>(nameof(BottomOffset), 16);

    private readonly List<ToastEntry> _entries = [];

    // Toasts asked for before the template exists (for example from a Loaded handler) wait here and show once it does.
    private readonly List<(string Message, TimeSpan? Duration, Geometry? Icon)> _pending = [];
    private StackPanel? _stack;
    private double _lastInset = double.NaN;

    /// <inheritdoc cref="PolicyProperty"/>
    public ToastPolicy Policy
    {
        get => GetValue(PolicyProperty);
        set => SetValue(PolicyProperty, value);
    }

    /// <inheritdoc cref="MaxVisibleProperty"/>
    public int MaxVisible
    {
        get => GetValue(MaxVisibleProperty);
        set => SetValue(MaxVisibleProperty, value);
    }

    /// <inheritdoc cref="DefaultDurationProperty"/>
    public TimeSpan DefaultDuration
    {
        get => GetValue(DefaultDurationProperty);
        set => SetValue(DefaultDurationProperty, value);
    }

    /// <inheritdoc cref="BottomOffsetProperty"/>
    public double BottomOffset
    {
        get => GetValue(BottomOffsetProperty);
        set => SetValue(BottomOffsetProperty, value);
    }

    /// <summary>The number of toasts showing that are not already leaving.</summary>
    public int Count => _entries.Count(e => !e.IsLeaving);

    protected override Type StyleKeyOverride => typeof(Toast);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        foreach (var entry in _entries.ToList())
        {
            entry.Dispose();
        }
        _entries.Clear();
        _stack = e.NameScope.Find<StackPanel>("PART_Stack");
        _lastInset = double.NaN;
        UpdateInset();
        if (_stack is not null && _pending.Count > 0)
        {
            var pending = _pending.ToList();
            _pending.Clear();
            foreach (var (message, duration, icon) in pending)
            {
                Show(message, duration, icon);
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BottomOffsetProperty)
        {
            UpdateInset();
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_entries.Count > 0)
        {
            UpdateInset();
        }
    }

    /// <summary>Keeps the stack above the page's footer and bottom safe area (the overlay itself covers both).</summary>
    private void UpdateInset()
    {
        if (_stack is null)
        {
            return;
        }
        var inset = BottomOffset;
        if (this.FindAncestorOfType<Page>() is { } page)
        {
            inset += page.SafeArea.Bottom;
            if (page.Footer is Control { IsVisible: true } footer)
            {
                inset += footer.Bounds.Height;
            }
        }
        if (!double.IsNaN(_lastInset) && Math.Abs(inset - _lastInset) < 0.01)
        {
            return;
        }
        _lastInset = inset;
        _stack.Margin = new Thickness(0, 0, 0, inset);
    }

    /// <summary>
    /// Shows a toast. Under <see cref="ToastPolicy.Replace"/> the toasts showing leave first; under
    /// <see cref="ToastPolicy.Stack"/> the oldest leaves when there would be more than <see cref="MaxVisible"/>.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="duration">How long it stays; null uses <see cref="DefaultDuration"/>, <see cref="TimeSpan.Zero"/> keeps it until swiped away or <see cref="DismissAll"/>.</param>
    /// <param name="icon">An optional glyph geometry shown before the text, for example a Lucide icon.</param>
    public void Show(string message, TimeSpan? duration = null, Geometry? icon = null)
    {
        if (_stack is null)
        {
            _pending.Add((message, duration, icon));
            return;
        }
        UpdateInset();
        var limit = Policy == ToastPolicy.Replace ? 1 : Math.Max(1, MaxVisible);
        foreach (var old in _entries.Where(e => !e.IsLeaving).Reverse().Skip(limit - 1).ToList())
        {
            old.Dismiss(0);
        }
        var entry = new ToastEntry(this, message, icon);
        _entries.Add(entry);
        _stack.Children.Add(entry.Root);
        entry.Enter(duration ?? DefaultDuration);
    }

    /// <summary>Sends every toast showing away.</summary>
    public void DismissAll()
    {
        foreach (var entry in _entries.ToList())
        {
            entry.Dismiss(0);
        }
    }

    /// <summary>The text of the toasts showing now (not leaving), oldest first. For tests and diagnostics.</summary>
    internal IReadOnlyList<string> Messages => _entries.Where(e => !e.IsLeaving).Select(e => e.Message).ToList();

    /// <summary>The toasts' visuals, in stack order. For tests and diagnostics.</summary>
    internal IReadOnlyList<Control> ToastVisuals => _stack?.Children.ToList() ?? [];

    private void Remove(ToastEntry entry)
    {
        _entries.Remove(entry);
        _stack?.Children.Remove(entry.Root);
        entry.Dispose();
    }

    private ArcMotionLevel Level => ArcMotion.GetEffectiveLevel(this);

    /// <summary>One toast: its visual, presence, swipe offset and countdown.</summary>
    private sealed class ToastEntry : IDisposable
    {
        private readonly Toast _host;
        private readonly TranslateTransform _translate = new();
        private readonly MotionValue<double> _presence;
        private readonly MotionValue<double> _offset;
        private readonly MotionTimeline _countdown;
        private readonly DragTracker _drag;
        private double _dragOrigin;
        private bool _disposed;

        public ToastEntry(Toast host, string message, Geometry? icon)
        {
            _host = host;
            Message = message;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            if (icon is not null)
            {
                row.Children.Add(new PathIcon { Data = icon });
            }
            var text = new TextBlock { Text = message };
            AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
            row.Children.Add(text);
            Root = new Border { Classes = { "toast" }, Child = row, RenderTransform = _translate, Opacity = 0 };
            _presence = new MotionValue<double>(Root, 0, MotionVectors.Double);
            _offset = new MotionValue<double>(Root, 0, MotionVectors.Double);
            _presence.Changed += _ => Apply();
            _offset.Changed += _ => Apply();
            _countdown = new MotionTimeline(Root);
            _countdown.Completed += () => Dismiss(0);
            _drag = new DragTracker(Root, new DragOptions { Axis = DragAxis.Horizontal, Threshold = 4, RejectCrossAxis = false });
            _drag.Pressed += (_, _) => _countdown.Pause();
            _drag.Started += (_, _) =>
            {
                _offset.Stop();
                _dragOrigin = _offset.Value;
            };
            _drag.Moved += (_, args) =>
            {
                // A reduced-motion swipe does not follow the finger; it only decides on release.
                if (!IsLeaving && _host.Level == ArcMotionLevel.Full)
                {
                    _offset.Jump(_dragOrigin + args.Offset.X);
                }
            };
            _drag.Completed += (_, args) => EndSwipe(args.Offset.X, args.Velocity.X);
            _drag.Cancelled += (_, _) => EndSwipe(0, 0);
            _drag.Released += (_, _) => _countdown.Resume();
        }

        public Border Root { get; }

        public string Message { get; }

        public bool IsLeaving { get; private set; }

        private void Apply()
        {
            var swipeFade = 1 - Math.Min(1, Math.Abs(_offset.Value) / Math.Max(1, Root.Bounds.Width + 32));
            Root.Opacity = Math.Clamp(_presence.Value, 0, 1) * swipeFade;
            _translate.X = _offset.Value;
            // Reduced motion fades only: no travel.
            _translate.Y = _host.Level == ArcMotionLevel.Full ? (1 - Math.Clamp(_presence.Value, 0, 1)) * Rise : 0;
        }

        public void Enter(TimeSpan duration)
        {
            switch (_host.Level)
            {
                case ArcMotionLevel.Off:
                    _presence.Jump(1);
                    break;
                case ArcMotionLevel.Reduced:
                    _ = _presence.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
                    break;
                default:
                    _ = _presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
                    break;
            }
            if (duration > TimeSpan.Zero)
            {
                _countdown.Start(duration);
            }
        }

        private void EndSwipe(double distance, double velocity)
        {
            if (IsLeaving)
            {
                return;
            }
            if (Math.Abs(distance) > SwipeDistance || Math.Abs(velocity) > SwipeVelocity)
            {
                Dismiss(velocity != 0 ? Math.Sign(velocity) : Math.Sign(distance));
                _countdown.Stop();
                return;
            }
            _countdown.Resume();
            if (_host.Level == ArcMotionLevel.Full)
            {
                _ = _offset.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth), 0.0);
            }
            else
            {
                _offset.Jump(0);
            }
        }

        /// <param name="direction">-1 or 1 to leave sideways (a swipe), 0 to fade out where it is.</param>
        public void Dismiss(int direction)
        {
            if (IsLeaving || _disposed)
            {
                return;
            }
            IsLeaving = true;
            _countdown.Stop();
            var level = _host.Level;
            if (level == ArcMotionLevel.Off)
            {
                _host.Remove(this);
                return;
            }
            // Leaving is faster than arriving and never overshoots, so it is a short tween rather than a spring.
            var spec = level == ArcMotionLevel.Reduced
                ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
                : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Exit);
            Task<bool> exit;
            if (direction != 0 && level == ArcMotionLevel.Full)
            {
                exit = _offset.AnimateTo(direction * (Root.Bounds.Width + 32), spec);
            }
            else
            {
                exit = _presence.AnimateTo(0, spec);
            }
            _ = exit.ContinueWith(t => Root.Dispatcher.Post(() =>
            {
                if (t.Result || IsLeaving)
                {
                    _host.Remove(this);
                }
            }));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _drag.Dispose();
            _countdown.Dispose();
            _presence.Dispose();
            _offset.Dispose();
        }
    }
}
