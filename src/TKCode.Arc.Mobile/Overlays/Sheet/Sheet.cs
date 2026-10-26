using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile;

/// <summary>Resting heights of a <see cref="Sheet"/> as fractions of the sheet's height. Parses from "0.4, 0.9".</summary>
public sealed class SheetDetents : AvaloniaList<double>
{
    /// <summary>Creates an empty list.</summary>
    public SheetDetents()
    {
    }

    /// <summary>Creates a list holding <paramref name="values"/>.</summary>
    public SheetDetents(IEnumerable<double> values) : base(values)
    {
    }

    /// <summary>Parses comma or space separated fractions ("0.4, 0.9").</summary>
    public static SheetDetents Parse(string text)
    {
        var values = new SheetDetents();
        foreach (var part in text.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            values.Add(double.Parse(part, NumberStyles.Float, CultureInfo.InvariantCulture));
        }
        return values;
    }
}

/// <summary>Raised when a <see cref="Sheet"/> comes to rest on another detent.</summary>
public sealed class SheetDetentChangedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    public SheetDetentChangedEventArgs(int index, double fraction)
    {
        Index = index;
        Fraction = fraction;
    }

    /// <summary>Index into the sorted detents.</summary>
    public int Index { get; }

    /// <summary>Height as a fraction of the sheet's height.</summary>
    public double Fraction { get; }
}

/// <summary>
/// A bottom sheet: a panel that rises over a <see cref="Page"/> with a scrim behind it. The person closes it by tapping
/// the scrim, pulling the handle down or flicking it, or with Escape. Put it in <see cref="Page.Overlay"/> and open it
/// with <see cref="IsOpen"/>; it sizes to its content, up to <see cref="MaxHeightFraction"/> of the page, and the
/// content scrolls beyond that. Closed, it does not catch touches.
/// <para>
/// With <see cref="Detents"/> the sheet rests at several heights instead (a peek and a full view): a pull follows the
/// finger, stretches like a rubber band past the tallest detent, and on release looks 0.2 s ahead to pick the nearest
/// detent or close; a flick always moves at least one detent. The content scrolls only at the tallest detent, and a pull
/// down from its top moves the sheet. The scrim follows the position.
/// </para>
/// <para>
/// The panel's position is a <see cref="MotionValue{T}"/>, not a ControlTheme transition: a drag writes it on every
/// move, a release hands the finger's velocity to the spring, and the scrim is computed from the position each frame.
/// Under reduced motion the panel jumps instead of gliding.
/// </para>
/// </summary>
[TemplatePart("PART_Scrim", typeof(Border))]
[TemplatePart("PART_Panel", typeof(Border))]
[TemplatePart("PART_Handle", typeof(Control))]
[TemplatePart("PART_Body", typeof(ScrollViewer))]
public class Sheet : ContentControl
{
    /// <summary>The panel slides this far past its own height when closed, so its outline and shadow are out of sight.</summary>
    private const double ClosedGap = 24;

    /// <summary>A release coasts this long at its speed before it is decided whether the sheet closes.</summary>
    private static readonly TimeSpan Projection = TimeSpan.FromSeconds(0.2);

    /// <summary>A release faster than this (px/s) moves at least one detent, or closes the sheet from its lowest one.</summary>
    private const double FlickVelocity = 500;

    /// <summary>The limit a pull approaches past the end of its range.</summary>
    private const double Stretch = 80;

    /// <summary>Share of the full scrim that remains at the lowest of several detents.</summary>
    private const double LowDim = 0.78;

    /// <summary>Whether the sheet is open.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Sheet, bool>(nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The title shown in the handle area, under the grabber.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Sheet, string?>(nameof(Title));

    /// <summary>Whether the person can close the sheet by tapping the scrim, pulling the panel down or pressing Escape.</summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<Sheet, bool>(nameof(IsDismissible), true);

    /// <summary>The tallest the panel gets, as a fraction of the sheet's height: 0.9.</summary>
    public static readonly StyledProperty<double> MaxHeightFractionProperty =
        AvaloniaProperty.Register<Sheet, double>(nameof(MaxHeightFraction), 0.9);

    /// <summary>
    /// The resting heights as fractions of the sheet's height, for example "0.4, 0.9". Null or empty: the panel sizes to its
    /// content. One value is a fixed height.
    /// </summary>
    public static readonly StyledProperty<SheetDetents?> DetentsProperty =
        AvaloniaProperty.Register<Sheet, SheetDetents?>(nameof(Detents));

    /// <summary>Index into the sorted detents the sheet opens at (default 0, the lowest).</summary>
    public static readonly StyledProperty<int> InitialDetentIndexProperty =
        AvaloniaProperty.Register<Sheet, int>(nameof(InitialDetentIndex));

    /// <summary>The detent the sheet rests on (index into the sorted detents).</summary>
    public static readonly DirectProperty<Sheet, int> DetentIndexProperty =
        AvaloniaProperty.RegisterDirect<Sheet, int>(nameof(DetentIndex), o => o.DetentIndex);

    /// <summary>
    /// The height of the bottom safe area (the home indicator) in px, kept free under the content. The sheet does not read
    /// the platform: bind it to the page, for example <c>SafeAreaBottom="{Binding SafeArea.Bottom, ElementName=Page}"</c>.
    /// </summary>
    public static readonly StyledProperty<double> SafeAreaBottomProperty =
        AvaloniaProperty.Register<Sheet, double>(nameof(SafeAreaBottom));

    private readonly TranslateTransform _translate = new();
    private Border? _scrim;
    private Border? _panel;
    private Control? _handle;
    private ScrollViewer? _body;
    private MotionValue<double>? _y;
    private DragTracker? _drag;
    private DragTracker? _bodyDrag;
    private double _dragOrigin;
    private int _dragFrom;
    private bool _dragging;
    private bool _mounted;
    private bool _present;
    private double[] _stops = [1];
    private bool _hasDetents;
    private int _aim;
    private int _detentIndex;
    private SheetDetents? _observedDetents;
    private bool _watching;

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="IsDismissibleProperty"/>
    public bool IsDismissible
    {
        get => GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    /// <inheritdoc cref="MaxHeightFractionProperty"/>
    public double MaxHeightFraction
    {
        get => GetValue(MaxHeightFractionProperty);
        set => SetValue(MaxHeightFractionProperty, value);
    }

    /// <inheritdoc cref="DetentsProperty"/>
    public SheetDetents? Detents
    {
        get => GetValue(DetentsProperty);
        set => SetValue(DetentsProperty, value);
    }

    /// <inheritdoc cref="InitialDetentIndexProperty"/>
    public int InitialDetentIndex
    {
        get => GetValue(InitialDetentIndexProperty);
        set => SetValue(InitialDetentIndexProperty, value);
    }

    /// <inheritdoc cref="DetentIndexProperty"/>
    public int DetentIndex
    {
        get => _detentIndex;
        private set => SetAndRaise(DetentIndexProperty, ref _detentIndex, value);
    }

    /// <inheritdoc cref="SafeAreaBottomProperty"/>
    public double SafeAreaBottom
    {
        get => GetValue(SafeAreaBottomProperty);
        set => SetValue(SafeAreaBottomProperty, value);
    }

    /// <summary>The sheet came to rest on another detent.</summary>
    public event EventHandler<SheetDetentChangedEventArgs>? DetentChanged;

    /// <summary>The sorted detents in use: a single 1 when the panel sizes to its content.</summary>
    public IReadOnlyList<double> EffectiveDetents => _stops;

    /// <summary>The panel's offset below its open position, in px (0 when open at the tallest detent). For tests and diagnostics.</summary>
    public double PanelOffset => _y?.Value ?? 0;

    /// <summary>Moves the open sheet to the detent at <paramref name="index"/> (in sorted order, clamped).</summary>
    public void SnapTo(int index)
    {
        if (IsOpen && _y is not null)
        {
            Rest(Math.Clamp(index, 0, Top));
        }
    }

    /// <summary>Raises the open sheet to its tallest detent.</summary>
    public void Expand() => SnapTo(Top);

    /// <summary>Lowers the open sheet to its lowest detent.</summary>
    public void Collapse() => SnapTo(0);

    protected override Type StyleKeyOverride => typeof(Sheet);

    private int Top => _stops.Length - 1;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _panel = e.NameScope.Find<Border>("PART_Panel");
        _handle = e.NameScope.Find<Control>("PART_Handle");
        _body = e.NameScope.Find<ScrollViewer>("PART_Body");
        if (_panel is null)
        {
            return;
        }
        _panel.RenderTransform = _translate;
        _y = new MotionValue<double>(_panel, 4000, MotionVectors.Double);
        _y.Changed += _ => Apply();
        _panel.SizeChanged += OnPanelSizeChanged;
        if (_scrim is not null)
        {
            _scrim.PointerPressed += OnScrimPressed;
        }
        if (_handle is not null)
        {
            _drag = new DragTracker(_handle, new DragOptions { Axis = DragAxis.Vertical, Threshold = 3, RejectCrossAxis = false })
            {
                CanBegin = _ => IsOpen,
            };
            _drag.Started += (_, _) => BeginDrag(0);
            _drag.Moved += (_, args) => MoveDrag(args.Offset.Y);
            _drag.Completed += (_, args) => EndDrag(args.Velocity.Y);
            _drag.Cancelled += (_, _) => EndDrag(0);
        }
        if (_body is { } body)
        {
            // A finger on the content moves the sheet until it is at its tallest detent; there the content scrolls, and a
            // pull down from its top moves the sheet again.
            _bodyDrag = new DragTracker(body, new DragOptions { Axis = DragAxis.Vertical, Threshold = 4, HandledEventsToo = true })
            {
                CanBegin = args => IsOpen && args.Pointer.Type == PointerType.Touch,
            };
            _bodyDrag.Starting += (_, args) =>
            {
                if (DetentIndex == Top)
                {
                    var scrollable = body.Extent.Height > body.Viewport.Height + 1;
                    args.Cancel = scrollable && !(body.Offset.Y <= 0 && args.Offset.Y > 0);
                }
            };
            _bodyDrag.Started += (_, args) => BeginDrag(args.Offset.Y);
            _bodyDrag.Moved += (_, args) => MoveDrag(args.Offset.Y);
            _bodyDrag.Completed += (_, args) => EndDrag(args.Velocity.Y);
            _bodyDrag.Cancelled += (_, _) => EndDrag(0);
            body.AddHandler(PointerWheelChangedEvent, OnBodyWheel, RoutingStrategies.Tunnel);
        }
        _mounted = false;
        _present = false;
        UpdateStops();
        UpdateSize();
        if (IsOpen)
        {
            // Open from the start (IsOpen="True" in markup): rest on the initial detent, no entrance.
            _aim = Math.Clamp(InitialDetentIndex, 0, Top);
            DetentIndex = _aim;
        }
        UpdatePresence();
        Apply();
    }

    private void Detach()
    {
        _drag?.Dispose();
        _drag = null;
        _bodyDrag?.Dispose();
        _bodyDrag = null;
        _y?.Dispose();
        _y = null;
        if (_panel is not null)
        {
            _panel.SizeChanged -= OnPanelSizeChanged;
        }
        if (_scrim is not null)
        {
            _scrim.PointerPressed -= OnScrimPressed;
        }
        _body?.RemoveHandler(PointerWheelChangedEvent, OnBodyWheel);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                Open();
            }
            else
            {
                Close(null);
            }
        }
        else if (change.Property == MaxHeightFractionProperty || change.Property == BoundsProperty)
        {
            UpdateSize();
        }
        else if (change.Property == DetentsProperty)
        {
            WatchDetents();
            OnDetentsChanged(null, null);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchDetents();
        OnDetentsChanged(null, null);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchDetents();
    }

    /// <summary>Listens to the current detents while in a visual tree (a view model's list may outlive the sheet).</summary>
    private void WatchDetents()
    {
        if (_observedDetents is not null)
        {
            _observedDetents.CollectionChanged -= OnDetentsChanged;
        }
        _observedDetents = _watching ? Detents : null;
        if (_observedDetents is not null)
        {
            _observedDetents.CollectionChanged += OnDetentsChanged;
        }
    }

    private void OnDetentsChanged(object? sender, NotifyCollectionChangedEventArgs? e)
    {
        var before = _stops;
        var hadDetents = _hasDetents;
        UpdateStops();
        if (hadDetents == _hasDetents && before.AsSpan().SequenceEqual(_stops))
        {
            return;
        }
        UpdateSize();
        if (IsOpen && _mounted && _y is not null)
        {
            _panel?.UpdateLayout();
            Rest(Math.Clamp(_aim, 0, Top));
        }
    }

    private void UpdateStops()
    {
        var stops = Detents?.Where(v => v > 0 && double.IsFinite(v)).Select(v => Math.Min(v, 1)).Distinct().Order().ToArray() ?? [];
        _hasDetents = stops.Length > 0;
        _stops = _hasDetents ? stops : [1];
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsOpen && IsDismissible)
        {
            e.Handled = true;
            IsOpen = false;
        }
    }

    private bool Reduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    /// <summary>Height of the panel at its tallest detent.</summary>
    private double Full
    {
        get
        {
            if (_panel is { } panel && panel.Bounds.Height > 0)
            {
                return panel.Bounds.Height;
            }
            return _hasDetents ? Bounds.Height * _stops[^1] : 0;
        }
    }

    /// <summary>The offset below the tallest position at which detent <paramref name="stop"/> rests.</summary>
    private double OffsetOf(int stop) => _hasDetents ? Full * (1 - _stops[stop] / _stops[^1]) : 0;

    private double ClosedOffset => Full + ClosedGap;

    /// <summary>Sizes the panel: to the tallest detent when there are detents, otherwise to its content up to the maximum.</summary>
    private void UpdateSize()
    {
        if (_panel is null || Bounds.Height <= 0)
        {
            return;
        }
        if (_hasDetents)
        {
            _panel.MaxHeight = double.PositiveInfinity;
            _panel.Height = Bounds.Height * _stops[^1];
        }
        else
        {
            _panel.ClearValue(Avalonia.Layout.Layoutable.HeightProperty);
            _panel.MaxHeight = Bounds.Height * Math.Clamp(MaxHeightFraction, 0.1, 1);
        }
    }

    /// <summary>The sheet catches touches while it is open or still leaving; otherwise the page under it does.</summary>
    private void UpdatePresence()
    {
        IsHitTestVisible = IsOpen || _present;
        if (_panel is not null)
        {
            _panel.IsVisible = IsOpen || _present;
        }
    }

    private void Apply()
    {
        if (_y is null)
        {
            return;
        }
        var y = _y.Value;
        _translate.Y = y;
        if (_scrim is not null)
        {
            // The scrim follows the panel: full at the tallest detent, LowDim at the lowest of several, clear when closed.
            var closed = ClosedOffset;
            if (closed <= ClosedGap)
            {
                _scrim.Opacity = IsOpen ? 1 : 0;
                return;
            }
            var low = OffsetOf(0);
            var lowDim = _stops.Length > 1 ? LowDim : 1;
            var dim = y <= low
                ? 1 - (1 - lowDim) * (low > 0 ? y / low : 0)
                : lowDim * (1 - (y - low) / (closed - low));
            _scrim.Opacity = Math.Clamp(dim, 0, 1);
        }
    }

    private void Open()
    {
        if (_y is null || _panel is null)
        {
            return;
        }
        _present = true;
        UpdatePresence();
        UpdateStops();
        UpdateSize();
        _aim = Math.Clamp(InitialDetentIndex, 0, Top);
        DetentIndex = _aim;
        _panel.UpdateLayout();
        if (!_mounted)
        {
            _mounted = true;
            _y.Jump(ClosedOffset);
        }
        if (Reduced)
        {
            _y.Jump(OffsetOf(_aim));
            return;
        }
        _ = _y.AnimateTo(OffsetOf(_aim), MotionSpec.Spring(ArcSpring.Smooth));
        _panel.Focus();
    }

    /// <param name="velocity">The finger's downward speed in px/s when a flick closed the sheet, or null.</param>
    private void Close(double? velocity)
    {
        if (_y is null)
        {
            return;
        }
        if (Reduced)
        {
            _y.Jump(ClosedOffset);
            Finish();
            return;
        }
        var spec = MotionSpec.Spring(SpringSpec.FromVisual(0.3, 0));
        _ = _y.AnimateTo(ClosedOffset, spec, velocity).ContinueWith(
            t => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (t.Result)
                {
                    Finish();
                }
            }));
    }

    /// <summary>The exit landed: the sheet stops catching touches.</summary>
    private void Finish()
    {
        if (!IsOpen)
        {
            _present = false;
            UpdatePresence();
        }
    }

    /// <summary>Moves to a detent, from wherever the panel is, carrying the finger's velocity.</summary>
    private void Rest(int stop, double? velocity = null)
    {
        if (_y is null)
        {
            return;
        }
        if (stop != DetentIndex)
        {
            DetentIndex = stop;
            if (_hasDetents)
            {
                DetentChanged?.Invoke(this, new SheetDetentChangedEventArgs(stop, _stops[stop]));
            }
        }
        _aim = stop;
        // Below the tallest detent the content shows its start.
        if (stop != Top && _body is { } body && body.Offset.Y > 0)
        {
            body.Offset = body.Offset.WithY(0);
        }
        _ = _y.AnimateTo(OffsetOf(stop), MotionSpec.Spring(ArcSpring.Smooth), velocity);
    }

    private void OnPanelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_y is null || _dragging || _y.IsAnimating)
        {
            return;
        }
        // The panel changed size (content or window): keep it where it rests.
        _y.Jump(IsOpen ? OffsetOf(_aim) : ClosedOffset);
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsOpen && IsDismissible)
        {
            e.Handled = true;
            IsOpen = false;
        }
    }

    private void OnBodyWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DetentIndex == Top || !_hasDetents)
        {
            return;
        }
        // Below the tallest detent the content does not scroll; scrolling down raises the sheet the rest of the way.
        e.Handled = true;
        if (e.Delta.Y < 0 && !_dragging)
        {
            Rest(Top);
        }
    }

    private void BeginDrag(double offsetAtStart)
    {
        if (_y is null)
        {
            return;
        }
        // Catch the panel where it is, even in flight; a stretched panel converts back to finger distance.
        _y.Stop();
        var current = _y.Value;
        var low = OffsetOf(0);
        double origin;
        if (current < 0)
        {
            origin = -RubberBand.Invert(-current, Stretch);
        }
        else if (!IsDismissible && current > low)
        {
            origin = low + RubberBand.Invert(current - low, Stretch);
        }
        else
        {
            origin = current;
        }
        _dragOrigin = origin - offsetAtStart;
        _dragFrom = DetentIndex;
        _dragging = true;
    }

    private void MoveDrag(double offset)
    {
        if (!_dragging || _y is null || Reduced)
        {
            return;
        }
        var raw = _dragOrigin + offset;
        var low = OffsetOf(0);
        // Between the detents (and down, when it may close) the panel follows the finger 1:1; past the tallest detent, and
        // past the lowest when it may not close, it resists like a rubber band.
        if (raw < 0)
        {
            _y.Jump(-RubberBand.Apply(-raw, Stretch));
        }
        else if (!IsDismissible && raw > low)
        {
            _y.Jump(low + RubberBand.Apply(raw - low, Stretch));
        }
        else
        {
            _y.Jump(raw);
        }
    }

    private void EndDrag(double velocity)
    {
        if (!_dragging || _y is null)
        {
            return;
        }
        _dragging = false;
        var current = _y.Value;
        var low = OffsetOf(0);
        // A stretched panel springs back without the finger's speed, so it never launches past the stretch.
        if (current < 0 || (!IsDismissible && current > low))
        {
            velocity = 0;
        }
        var count = _stops.Length;
        Span<double> points = stackalloc double[count + 1];
        for (var i = 0; i < count; i++)
        {
            points[i] = OffsetOf(i);
        }
        points[count] = ClosedOffset;
        var from = Math.Clamp(_dragFrom, 0, Top);
        var chosen = MomentumProjector.SelectSnapPoint(
            IsDismissible ? points : points[..count], current, velocity, SnapProjection.Linear(Projection, from, FlickVelocity));
        if (chosen == count)
        {
            // The exit starts in this frame with the finger's velocity; IsOpen = false then only waits for it to land.
            IsOpen = false;
            return;
        }
        Rest(chosen, velocity);
    }
}
