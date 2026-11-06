using Avalonia;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace TKCode.Arc;

/// <summary>
/// A floating window of a <see cref="DockLayout"/> (spec 5.5): an <see cref="ArcWindow"/> owned by the layout's window (it
/// follows it when minimized and stays above it, and has no taskbar button) that shows one <see cref="DockFloat"/>'s tree,
/// titled after its active pane. Its place and size are the float's <see cref="DockFloat.Bounds"/> and stay in step with it.
/// Dragging the title bar moves the window from Dock's own drag, so the layout's compass can take it back; a double click on
/// the title bar docks it. Closing it closes the panes that can close and docks the others back.
/// </summary>
internal sealed class DockFloatingWindow : ArcWindow, IDockFloatHost
{
    private static bool? s_canTrackPosition;

    private readonly DockLayout _layout;
    private readonly Decorator _presenter;
    private readonly DockIndicatorLayer _layer;
    private readonly DockFloatTitle _title;
    private readonly DockDragGesture? _titleGesture;
    private IDisposable? _motion;
    private AvaloniaWindow? _owner;
    private PixelPoint _requested;
    private bool _dismissing;
    private bool _applying;
    private bool _writing;
    private bool _ready;

    public DockFloatingWindow(DockLayout layout, DockFloat dock)
    {
        _layout = layout;
        Float = dock;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        _presenter = new Decorator();
        _layer = new DockIndicatorLayer();
        Content = new Panel { Children = { _presenter, _layer } };
        Bind(PaddingProperty, this.GetResourceObservable(DockLayoutTokens.Key(DockLayoutTokenKind.Padding)));
        Bind(MinWidthProperty, this.GetResourceObservable(DockFloatingWindowTokens.Key(DockFloatingWindowTokenKind.MinWidth)));
        Bind(MinHeightProperty, this.GetResourceObservable(DockFloatingWindowTokens.Key(DockFloatingWindowTokenKind.MinHeight)));
        _motion = Bind(ArcMotion.LevelProperty, layout.GetObservable(ArcMotion.LevelProperty));
        _title = new DockFloatTitle(text => Title = text);
        ActivationStamp = DockActivation.Next();
        AddHandler(GotFocusEvent, (_, e) => _layout.OnFloatHostGotFocus(e), RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, e) => _layout.OnFloatHostKeyDown(e), RoutingStrategies.Bubble);
        AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        if (TitleBar is { } bar)
        {
            _titleGesture = new DockDragGesture(bar, layout, _ => CanTrackPosition ? _layout.ResolveHostPress(this) : null);
        }
        PositionChanged += (_, _) => WriteBounds();
        Activated += (_, _) => ActivationStamp = DockActivation.Next();
        ApplyBounds(dock.Bounds);
        WindowState = dock.IsMaximized ? WindowState.Maximized : WindowState.Normal;
        dock.PropertyChanged += OnFloatPropertyChanged;
        _ready = true;
        SyncContent();
    }

    /// <summary>Bounds or maximized state set on the model in code move the window.</summary>
    private void OnFloatPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_dismissing || _writing)
        {
            return;
        }
        if (e.Property == DockFloat.BoundsProperty)
        {
            ApplyBounds(Float.Bounds);
        }
        else if (e.Property == DockFloat.IsMaximizedProperty)
        {
            WindowState = Float.IsMaximized ? WindowState.Maximized : WindowState.Normal;
        }
    }

    /// <summary>
    /// False where a window's position cannot be set (Wayland): there windows open where the system puts them, title bars move
    /// them with the system's move, and floating windows dock back from the menu or by a double click (spec 5.5).
    /// </summary>
    public static bool CanTrackPosition => PositionTrackingOverride ?? s_canTrackPosition ?? true;

    /// <summary>Forces <see cref="CanTrackPosition"/> (tests); null detects it.</summary>
    internal static bool? PositionTrackingOverride { get; set; }

    public DockFloat Float { get; }

    DockFloat? IDockHost.Float => Float;

    public Visual Region => (Visual)Content!;

    public DockIndicatorLayer Layer => _layer;

    DockIndicatorLayer? IDockHost.Layer => _layer;

    public IEnumerable<DockGroup> Groups => Float.Root?.SelfAndDescendants().OfType<DockGroup>() ?? [];

    public long ActivationStamp { get; private set; }

    public bool IsWindow => true;

    /// <summary>The scale between desktop pixels (positions) and DIPs.</summary>
    private double Scaling => DesktopScaling > 0 ? DesktopScaling : 1;

    /// <summary>Shows the float in this window, owned by <paramref name="owner"/>.</summary>
    public void ShowOver(AvaloniaWindow owner)
    {
        _owner = owner;
        RequestedThemeVariant = owner.ActualThemeVariant;
        owner.ActualThemeVariantChanged += OnOwnerThemeVariantChanged;
        Show(owner);
        Dispatcher.UIThread.Post(CheckPosition, DispatcherPriority.Loaded);
        if (OperatingSystem.IsWindows() && TitleBar is { } bar)
        {
            // On Windows the bar is the system caption, whose presses never reach the app; Dock moves this window itself.
            Dispatcher.UIThread.Post(() => WindowDecorationProperties.SetElementRole(bar, WindowDecorationsElementRole.None),
                DispatcherPriority.Loaded);
        }
    }

    public void SyncContent()
    {
        if (Float.Root is { } root)
        {
            var view = _layout.ViewFor(root);
            if (!ReferenceEquals(_presenter.Child, view))
            {
                _presenter.Child = null;
                DockLayout.Adopt(view);
                _presenter.Child = view;
            }
        }
        _title.Follow(Float);
    }

    public void BringToFront()
    {
        ActivationStamp = DockActivation.Next();
        Activate();
    }

    public void Dismiss()
    {
        _dismissing = true;
        if (_owner is not null)
        {
            _owner.ActualThemeVariantChanged -= OnOwnerThemeVariantChanged;
        }
        Float.PropertyChanged -= OnFloatPropertyChanged;
        _title.Dispose();
        _titleGesture?.Dispose();
        _motion?.Dispose();
        _presenter.Child = null;
        Close();
    }

    /// <summary>Moves the window to a desktop-pixel position (a drag moving it).</summary>
    internal void MoveTo(PixelPoint position)
    {
        _requested = position;
        Position = position;
        if (IsWaylandSession && !PositionTrackingOverride.HasValue && IsVisible && !Near(Position, position))
        {
            s_canTrackPosition = false;
        }
    }

    private void ApplyBounds(Rect bounds)
    {
        _applying = true;
        try
        {
            var scale = Scaling;
            _requested = new PixelPoint((int)Math.Round(bounds.X * scale), (int)Math.Round(bounds.Y * scale));
            Position = _requested;
            if (bounds.Width > 0)
            {
                Width = bounds.Width;
            }
            if (bounds.Height > 0)
            {
                Height = bounds.Height;
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private void WriteBounds()
    {
        if (!_ready || _applying || _dismissing || WindowState != WindowState.Normal)
        {
            return;
        }
        var scale = Scaling;
        var size = ClientSize;
        _writing = true;
        try
        {
            Float.Bounds = new Rect(Position.X / scale, Position.Y / scale,
                size.Width > 0 ? size.Width : Float.Bounds.Width, size.Height > 0 ? size.Height : Float.Bounds.Height);
        }
        finally
        {
            _writing = false;
        }
    }

    private void OnOwnerThemeVariantChanged(object? sender, EventArgs e)
    {
        if (_owner is not null)
        {
            RequestedThemeVariant = _owner.ActualThemeVariant;
        }
    }

    /// <summary>
    /// Linux under Wayland ignores positions set by the app: detected by reading the position back, only in a Wayland session
    /// (an X11 window manager may nudge a new window, which must not switch the drags off).
    /// </summary>
    private void CheckPosition()
    {
        if (PositionTrackingOverride.HasValue || !IsWaylandSession || s_canTrackPosition.HasValue || !IsVisible)
        {
            return;
        }
        s_canTrackPosition = Near(Position, _requested);
    }

    private static bool IsWaylandSession
        => OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    private static bool Near(PixelPoint a, PixelPoint b) => Math.Abs(a.X - b.X) <= 2 && Math.Abs(a.Y - b.Y) <= 2;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ClientSizeProperty)
        {
            WriteBounds();
        }
        else if (change.Property == WindowStateProperty && _ready && !_dismissing)
        {
            _writing = true;
            try
            {
                Float.IsMaximized = change.GetNewValue<WindowState>() == WindowState.Maximized;
            }
            finally
            {
                _writing = false;
            }
        }
    }

    /// <summary>
    /// Presses on the title bar: a double click docks the window; otherwise, where Dock can place windows, the press is
    /// taken from the bar (which would start the system's move) so the bar's drag gesture moves the window instead.
    /// </summary>
    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (TitleBar is not { } bar || e.Source is not Visual source || !(ReferenceEquals(source, bar) || bar.IsVisualAncestorOf(source)))
        {
            return;
        }
        if (source.FindAncestorOfType<AvaloniaButton>(includeSelf: true) is { } button && bar.IsVisualAncestorOf(button))
        {
            return;
        }
        if (!e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (e.ClickCount == 2)
        {
            e.Handled = true;
            _layout.DockFloat(Float);
            return;
        }
        if (CanTrackPosition)
        {
            e.Handled = true;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_dismissing || e.Cancel)
        {
            return;
        }
        if (e.CloseReason != WindowCloseReason.WindowClosing)
        {
            // The owner or the application is closing: the float stays in the model (a saved layout keeps it).
            _dismissing = true;
            _layout.ForgetFloatHost(this);
            return;
        }
        // The user closed the window: close its panes (the window goes when its float empties), keeping it while some stay.
        e.Cancel = true;
        Dispatcher.UIThread.Post(() => _layout.CloseFloat(Float));
    }
}
