using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// When the base theme's overlay scroll bars show (<c>ScrollViewerBaseTheme.axaml</c>; design/architecture/foundation.md
/// 14.2), ScrollArea's default: while a mouse is over the viewer, while the offset changes (wheel, keyboard, touch,
/// code) and <see cref="HideDelay"/> after the last change, and while a bar's thumb is dragged. With
/// <see cref="ScrollViewer.AllowAutoHide"/> false the bars always show. The theme reads <see cref="IsShownProperty"/>;
/// the fade itself is ControlTheme transitions.
/// </summary>
/// <remarks>
/// Code because a ControlTheme cannot express two of the triggers: "for a while after the offset last changed" needs a
/// timer, and hover counts only for a mouse (ScrollArea: hover reveals only for fine pointers), while
/// <c>:pointerover</c> also matches a finger on the screen. Turned on by the ScrollViewer theme's
/// <see cref="IsEnabledProperty"/> setter, so a viewer under another theme carries none of it.
/// </remarks>
internal static class ScrollViewerAutoHide
{
    /// <summary>How long the bars stay after the offset last changed or a drag ended (ScrollArea's HideDelay).</summary>
    public static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(900);

    /// <summary>Hooks the viewer up (set by the base theme).</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("IsEnabled", typeof(ScrollViewerAutoHide));

    /// <summary>True while the viewer's bars show (written here, read by the theme).</summary>
    public static readonly AttachedProperty<bool> IsShownProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("IsShown", typeof(ScrollViewerAutoHide));

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, Tracker?>("Tracker", typeof(ScrollViewerAutoHide));

    /// <summary>Creates the hide timer: a delay and its callback (tests swap in a manual clock).</summary>
    internal static Func<TimeSpan, Action, IHideTimer> CreateTimer { get; set; } =
        static (delay, elapsed) => new DispatcherHideTimer(delay, elapsed);

    static ScrollViewerAutoHide()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(ScrollViewer viewer) => viewer.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(ScrollViewer viewer, bool value) => viewer.SetValue(IsEnabledProperty, value);

    public static bool GetIsShown(ScrollViewer viewer) => viewer.GetValue(IsShownProperty);

    public static void SetIsShown(ScrollViewer viewer, bool value) => viewer.SetValue(IsShownProperty, value);

    private static void OnIsEnabledChanged(ScrollViewer viewer, AvaloniaPropertyChangedEventArgs e)
    {
        viewer.GetValue(TrackerProperty)?.Detach();
        viewer.ClearValue(TrackerProperty);
        if (e.NewValue is true)
        {
            viewer.SetValue(TrackerProperty, new Tracker(viewer));
        }
        else
        {
            viewer.ClearValue(IsShownProperty);
        }
    }

    /// <summary>A restartable one-shot delay.</summary>
    internal interface IHideTimer
    {
        /// <summary>Starts the delay over.</summary>
        void Restart();

        void Stop();
    }

    private sealed class DispatcherHideTimer : IHideTimer
    {
        private readonly DispatcherTimer _timer;
        private readonly Action _elapsed;

        public DispatcherHideTimer(TimeSpan delay, Action elapsed)
        {
            _elapsed = elapsed;
            _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = delay };
            _timer.Tick += OnTick;
        }

        public void Restart()
        {
            _timer.Stop();
            _timer.Start();
        }

        public void Stop() => _timer.Stop();

        private void OnTick(object? sender, EventArgs e)
        {
            _timer.Stop();
            _elapsed();
        }
    }

    private sealed class Tracker
    {
        private readonly ScrollViewer _viewer;
        private IHideTimer? _timer;
        private bool _hovering;
        private bool _scrolling;
        private bool _dragging;

        public Tracker(ScrollViewer viewer)
        {
            _viewer = viewer;
            viewer.PropertyChanged += OnPropertyChanged;
            viewer.AddHandler(InputElement.PointerEnteredEvent, OnPointerEntered, RoutingStrategies.Direct, true);
            viewer.AddHandler(InputElement.PointerExitedEvent, OnPointerExited, RoutingStrategies.Direct, true);
            viewer.AddHandler(Thumb.DragStartedEvent, OnDragStarted, RoutingStrategies.Bubble, true);
            viewer.AddHandler(Thumb.DragCompletedEvent, OnDragCompleted, RoutingStrategies.Bubble, true);
            viewer.DetachedFromVisualTree += OnDetached;
            Update();
        }

        public void Detach()
        {
            _timer?.Stop();
            _viewer.PropertyChanged -= OnPropertyChanged;
            _viewer.RemoveHandler(InputElement.PointerEnteredEvent, OnPointerEntered);
            _viewer.RemoveHandler(InputElement.PointerExitedEvent, OnPointerExited);
            _viewer.RemoveHandler(Thumb.DragStartedEvent, OnDragStarted);
            _viewer.RemoveHandler(Thumb.DragCompletedEvent, OnDragCompleted);
            _viewer.DetachedFromVisualTree -= OnDetached;
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.OffsetProperty)
            {
                Wake();
            }
            else if (e.Property == ScrollViewer.AllowAutoHideProperty)
            {
                Update();
            }
        }

        private void OnPointerEntered(object? sender, PointerEventArgs e)
        {
            if (e.Pointer.Type == PointerType.Mouse)
            {
                _hovering = true;
                Update();
            }
        }

        private void OnPointerExited(object? sender, PointerEventArgs e)
        {
            _hovering = false;
            Update();
        }

        private void OnDragStarted(object? sender, VectorEventArgs e)
        {
            if (IsOwnThumb(e.Source))
            {
                _dragging = true;
                Update();
            }
        }

        private void OnDragCompleted(object? sender, VectorEventArgs e)
        {
            if (IsOwnThumb(e.Source) && _dragging)
            {
                _dragging = false;
                Wake();
            }
        }

        // A slider or a nested viewer in the content raises the same events; only this viewer's bars count.
        private bool IsOwnThumb(object? source) =>
            source is Thumb { TemplatedParent: ScrollBar bar } && ReferenceEquals(bar.TemplatedParent, _viewer);

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _timer?.Stop();
            _hovering = false;
            _scrolling = false;
            _dragging = false;
            Update();
        }

        private void Wake()
        {
            _scrolling = true;
            _timer ??= CreateTimer(HideDelay, OnHideElapsed);
            _timer.Restart();
            Update();
        }

        private void OnHideElapsed()
        {
            _scrolling = false;
            Update();
        }

        private void Update()
        {
            var shown = !_viewer.AllowAutoHide || _hovering || _scrolling || _dragging;
            if (GetIsShown(_viewer) != shown)
            {
                SetIsShown(_viewer, shown);
            }
        }
    }
}
