using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Resting heights of a <see cref="BottomSheet"/> as fractions of the window height. Parses from "0.45, 0.92".</summary>
public sealed class BottomSheetDetents : AvaloniaList<double>
{
    public BottomSheetDetents()
    {
    }

    public BottomSheetDetents(IEnumerable<double> values) : base(values)
    {
    }

    /// <summary>Parses comma or space separated fractions ("0.3, 0.6, 0.95").</summary>
    public static BottomSheetDetents Parse(string text)
    {
        var values = new BottomSheetDetents();
        foreach (var part in text.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            values.Add(double.Parse(part, NumberStyles.Float, CultureInfo.InvariantCulture));
        }
        return values;
    }
}

/// <summary>Raised when a <see cref="BottomSheet"/> comes to rest on another detent.</summary>
public sealed class BottomSheetDetentChangedEventArgs : EventArgs
{
    public BottomSheetDetentChangedEventArgs(int index, double fraction)
    {
        Index = index;
        Fraction = fraction;
    }

    /// <summary>Index into the sorted detents.</summary>
    public int Index { get; }

    /// <summary>Height as a fraction of the window.</summary>
    public double Fraction { get; }
}

/// <summary>
/// A modal sheet that rises from the bottom edge and rests at one or more heights (detents): trip details, share
/// options, filters, map and media views where a peek is useful before the full view.
/// <code>
/// &lt;arc:BottomSheet Title="Trip details" Detents="0.45, 0.92" IsOpen="{Binding ShowTrip}"&gt; … &lt;/arc:BottomSheet&gt;
/// </code>
/// One motion value drives the sheet: a drag follows the pointer 1:1, rubber-bands past the tallest detent, and on
/// release projects 0.2s of its velocity to pick the nearest detent or close; a flick faster than 320px/s always moves at
/// least one detent. The dim is a function of the sheet position. The grabber answers ArrowUp / ArrowDown / Home / End
/// and toggles between the lowest and highest detent; reading keys on the sheet open it fully before scrolling; touch
/// drags on the content move the sheet until it is fully open. Under reduced motion detents jump and the sheet fades.
/// <para>
/// The sheet position is a <see cref="MotionValue{T}"/>, not a ControlTheme transition: a drag writes it every move,
/// a release hands the pointer velocity to the spring, and the scrim is computed from it each frame.
/// </para>
/// </summary>
public class BottomSheet : ModalOverlay
{
    /// <summary>Hidden distance below the window edge a closed sheet travels (hides the shadow too).</summary>
    private const double ClosedGap = 40;

    /// <summary>A release coasts this long at its velocity before the nearest detent is chosen.</summary>
    private static readonly TimeSpan Projection = TimeSpan.FromSeconds(0.2);

    /// <summary>A release faster than this (px/s) always moves at least one detent.</summary>
    private const double FlickVelocity = 320;

    /// <summary>The limit an upward stretch approaches past the tallest detent.</summary>
    private const double Stretch = 120;

    /// <summary>Share of the full dim that remains at the smallest detent.</summary>
    private const double LowDim = 0.78;

    private static readonly double[] s_defaultDetents = [0.45, 0.92];

    public static readonly StyledProperty<BottomSheetDetents?> DetentsProperty =
        AvaloniaProperty.Register<BottomSheet, BottomSheetDetents?>(nameof(Detents));

    public static readonly StyledProperty<int> InitialDetentIndexProperty =
        AvaloniaProperty.Register<BottomSheet, int>(nameof(InitialDetentIndex));

    public static readonly DirectProperty<BottomSheet, int> DetentIndexProperty =
        AvaloniaProperty.RegisterDirect<BottomSheet, int>(nameof(DetentIndex), o => o.DetentIndex);

    /// <summary>Resting heights as fractions of the window height (default 0.45 and 0.92). A single value is a fixed height.</summary>
    public BottomSheetDetents? Detents { get => GetValue(DetentsProperty); set => SetValue(DetentsProperty, value); }

    /// <summary>Alias of <see cref="Detents"/> (snap points).</summary>
    public BottomSheetDetents? SnapPoints { get => Detents; set => Detents = value; }

    /// <summary>Index into the sorted detents the sheet opens at (default 0, the smallest).</summary>
    public int InitialDetentIndex { get => GetValue(InitialDetentIndexProperty); set => SetValue(InitialDetentIndexProperty, value); }

    private int _detentIndex;

    /// <summary>The detent the sheet rests on (index into the sorted detents).</summary>
    public int DetentIndex
    {
        get => _detentIndex;
        private set => SetAndRaise(DetentIndexProperty, ref _detentIndex, value);
    }

    /// <summary>The sheet came to rest on another detent.</summary>
    public event EventHandler<BottomSheetDetentChangedEventArgs>? DetentChanged;

    private BottomSheetSurface? _sheet;
    private MotionValue<double>? _y;
    private MotionValue<double>? _fade;
    private readonly TranslateTransform _translate = new();
    private double[] _stops = s_defaultDetents;
    private int _aim;
    private bool _aimClosed;
    private bool _mounted;
    private Task<bool>? _flight;
    private DragTracker? _headerDrag;
    private DragTracker? _bodyDrag;
    private SheetDrag? _drag;
    private bool _suppressGrabberClick;
    // Detents may be a view model's list: it is observed only while the sheet is in a visual tree, and re-read on return.
    private BottomSheetDetents? _observedDetents;
    private bool _watching;

    private sealed class SheetDrag
    {
        public double Origin;
        public int From;
        public bool Moved;
    }

    static BottomSheet()
    {
        CloseButtonNameProperty.OverrideDefaultValue<BottomSheet>("Close");
    }

    /// <summary>The sorted detents in use.</summary>
    public IReadOnlyList<double> EffectiveDetents => _stops;

    /// <summary>Current offset of the sheet below its tallest detent, in px (tests and diagnostics).</summary>
    public double SheetOffset => _y?.Value ?? 0;

    /// <summary>Moves the sheet to the detent at <paramref name="index"/> (sorted, clamped).</summary>
    public void SnapTo(int index)
    {
        if (IsOpen && _sheet is not null)
        {
            Rest(Math.Clamp(index, 0, Top));
        }
    }

    /// <summary>Opens the sheet to its tallest detent.</summary>
    public void Expand() => SnapTo(Top);

    /// <summary>Lowers the sheet to its smallest detent.</summary>
    public void Collapse() => SnapTo(0);

    private int Top => _stops.Length - 1;

    #region Surface and presence

    protected override ModalSurface CreateSurface()
    {
        var sheet = new BottomSheetSurface { RenderTransform = _translate };
        _sheet = sheet;
        _y = new MotionValue<double>(sheet, 4000, MotionVectors.Double);
        _fade = new MotionValue<double>(sheet, 1, MotionVectors.Double);
        _y.Changed += _ => ApplyPosition();
        _fade.Changed += _ => ApplyPosition();
        sheet.PartsReady += (_, _) => AttachParts();
        sheet.SizeChanged += OnSheetSizeChanged;
        sheet.AddHandler(KeyDownEvent, OnSheetKeyDown, RoutingStrategies.Bubble);
        return sheet;
    }

    protected override void ConfigurePresence(ModalPresence presence)
    {
        presence.Entrance = ModalEntrance.Custom;
        presence.InitialFocus = _sheet;
        presence.CustomMotion = (_, entering) => entering ? Enter() : Exit();
    }

    protected override void OnOpening()
    {
        UpdateStops();
        if (!_mounted)
        {
            var index = Math.Clamp(InitialDetentIndex, 0, Top);
            _aim = index;
            _aimClosed = false;
            DetentIndex = index;
            _sheet!.SetExpanded(index == Top);
            _sheet.SetScrolled(false);
        }
    }

    protected override void OnExited()
    {
        _mounted = false;
        _drag = null;
        _sheet?.SetDragging(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DetentsProperty)
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
        // The detents may have changed while the sheet was away; an open sheet moves only if they did.
        var before = _stops;
        UpdateStops();
        if (!before.AsSpan().SequenceEqual(_stops))
        {
            OnDetentsChanged(null, null);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchDetents();
    }

    /// <summary>Listens to the current detents while in a visual tree (and to nothing otherwise).</summary>
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
        UpdateStops();
        if (IsOpen && _mounted && !_aimClosed)
        {
            Rest(Math.Clamp(_aim, 0, Top));
        }
    }

    private void UpdateStops()
    {
        var source = (IEnumerable<double>?)Detents ?? s_defaultDetents;
        var stops = source.Where(v => v > 0 && double.IsFinite(v)).Select(v => Math.Min(v, 1)).Distinct().OrderBy(v => v).ToArray();
        _stops = stops.Length == 0 ? s_defaultDetents : stops;
        if (_sheet is not null)
        {
            _sheet.MaxDetent = _stops[^1];
        }
    }

    #endregion

    #region Geometry

    /// <summary>Height of the sheet at its tallest detent.</summary>
    private double Full
    {
        get
        {
            if (_sheet is { } sheet && sheet.Bounds.Height > sheet.Extension)
            {
                return sheet.Bounds.Height - sheet.Extension;
            }
            return (Presence?.LayerSize.Height ?? 0) * _stops[^1];
        }
    }

    private double OffsetOf(int stop) => Full * (1 - _stops[stop] / _stops[Top]);

    private double ClosedOffset => Full + ClosedGap;

    private void ApplyPosition()
    {
        if (_y is null || _fade is null || _sheet is null)
        {
            return;
        }
        var y = _y.Value;
        _translate.Y = y;
        _sheet.Opacity = Math.Clamp(_fade.Value, 0, 1);
        if (Presence is { } presence)
        {
            // The dim follows the sheet: full at the tallest detent, 78% at the smallest, clearing toward closed.
            var full = Full;
            double dim = 0;
            if (full > 0)
            {
                var low = OffsetOf(0);
                var closed = full + ClosedGap;
                dim = y <= low
                    ? 1 - (1 - LowDim) * (low > 0 ? y / low : 0)
                    : LowDim * (1 - (y - low) / (closed - low));
            }
            presence.ScrimOpacity = Math.Clamp(dim, 0, 1) * Math.Clamp(_fade.Value, 0, 1);
        }
    }

    #endregion

    #region Motion

    private bool Reduced => _sheet is not null && ArcMotion.GetEffectiveLevel(_sheet) != ArcMotionLevel.Full;

    private Task<bool> Enter()
    {
        if (_y is null || _fade is null || _sheet is null)
        {
            return Task.FromResult(true);
        }
        var reduced = Reduced;
        if (!_mounted)
        {
            _mounted = true;
            // Starts below the fold, so the first frame never flashes.
            _y.Jump(ClosedOffset);
            _fade.Jump(reduced ? 0 : 1);
        }
        _aimClosed = false;
        if (reduced)
        {
            _y.Jump(OffsetOf(_aim));
            return _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        _fade.Jump(1);
        return Go(_aim, closed: false);
    }

    private Task<bool> Exit()
    {
        if (_y is null || _fade is null)
        {
            return Task.FromResult(true);
        }
        if (Reduced)
        {
            return _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        // A flick has already started the exit with its own velocity; removal only waits for it.
        if (_aimClosed && _y.IsAnimating && _flight is not null)
        {
            return _flight;
        }
        return Go(0, closed: true);
    }

    /// <summary>Retargets the one motion value from wherever it is.</summary>
    private Task<bool> Go(int stop, bool closed, double? velocity = null)
    {
        _aimClosed = closed;
        if (!closed)
        {
            _aim = stop;
        }
        var target = closed ? ClosedOffset : OffsetOf(stop);
        // Leaving is shorter than arriving (smooth with a 0.3s visual duration); a flick keeps its velocity.
        var spec = closed ? MotionSpec.Spring(SpringSpec.FromVisual(0.3, 0)) : MotionSpec.Spring(ArcSpring.Smooth);
        _flight = _y!.AnimateTo(target, spec, velocity);
        return _flight;
    }

    private void Rest(int stop, double? velocity = null)
    {
        if (stop != DetentIndex)
        {
            DetentIndex = stop;
            _sheet?.SetExpanded(stop == Top);
            DetentChanged?.Invoke(this, new BottomSheetDetentChangedEventArgs(stop, _stops[stop]));
            Announce(stop == Top ? "Sheet expanded" : stop == 0 ? "Sheet collapsed" :
                $"Sheet at {Math.Round(_stops[stop] * 100)} percent height");
        }
        // Collapsing takes the content back to its start, so the peek always shows the top of it.
        if (stop != Top && _sheet?.Body is { } body && body.Offset.Y > 0)
        {
            body.Offset = body.Offset.WithY(0);
        }
        _ = Go(stop, closed: false, velocity);
    }

    private void Announce(string text)
    {
        if (_sheet?.Announcer is { } announcer)
        {
            announcer.Text = text;
        }
    }

    private void OnSheetSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // A window resize moves every detent: keep the sheet on its detent, retargeting any flight.
        if (!_mounted || _y is null || _drag is not null || _aimClosed)
        {
            return;
        }
        if (_y.IsAnimating)
        {
            _ = Go(_aim, closed: false);
        }
        else
        {
            _y.Jump(OffsetOf(_aim));
        }
    }

    #endregion

    #region Input

    private void AttachParts()
    {
        if (_sheet is null)
        {
            return;
        }
        _headerDrag?.Dispose();
        _bodyDrag?.Dispose();
        _headerDrag = null;
        _bodyDrag = null;
        if (_sheet.Header is { } header)
        {
            // Presses on the grabber button count (it is part of the handle); the close button keeps its click.
            _headerDrag = new DragTracker(header, new DragOptions { Axis = DragAxis.Vertical, Threshold = 3, RejectCrossAxis = false, HandledEventsToo = true })
            {
                CanBegin = e => IsOpen && !IsOnCloseButton(e.Source as Visual)
            };
            _headerDrag.Pressed += (_, _) => BeginDrag(fromPress: true, 0);
            _headerDrag.Moved += (_, e) => MoveDrag(e.Offset.Y);
            _headerDrag.Completed += (_, e) => EndDrag(e.Velocity.Y);
            _headerDrag.Released += (_, _) => EndDrag(0);
            _headerDrag.Cancelled += (_, _) => EndDrag(0);
        }
        if (_sheet.Grabber is { } grabber)
        {
            grabber.Click += OnGrabberClick;
            grabber.AddHandler(KeyDownEvent, OnGrabberKeyDown, RoutingStrategies.Bubble);
        }
        if (_sheet.Body is { } body)
        {
            // Touch on the content moves the sheet until it is fully open; then the content scrolls, and a pull down from its top moves the sheet again.
            _bodyDrag = new DragTracker(body, new DragOptions { Axis = DragAxis.Vertical, Threshold = 4, HandledEventsToo = true })
            {
                CanBegin = e => IsOpen && e.Pointer.Type == PointerType.Touch
            };
            _bodyDrag.Starting += (_, e) =>
            {
                if (DetentIndex == Top)
                {
                    var scrollable = body.Extent.Height > body.Viewport.Height + 1;
                    e.Cancel = scrollable && !(body.Offset.Y <= 0 && e.Offset.Y > 0);
                }
            };
            _bodyDrag.Started += (_, e) => BeginDrag(fromPress: false, e.Offset.Y);
            _bodyDrag.Moved += (_, e) => MoveDrag(e.Offset.Y);
            _bodyDrag.Completed += (_, e) => EndDrag(e.Velocity.Y);
            _bodyDrag.Cancelled += (_, _) => EndDrag(0);
            body.AddHandler(PointerWheelChangedEvent, OnBodyWheel, RoutingStrategies.Tunnel);
            body.ScrollChanged += (_, _) => _sheet.SetScrolled(body.Offset.Y > 1);
            body.AddHandler(GotFocusEvent, OnBodyGotFocus, RoutingStrategies.Bubble);
        }
    }

    private bool IsOnCloseButton(Visual? source)
    {
        var close = _sheet?.CloseButton;
        for (var v = source; v is not null; v = v.GetVisualParent())
        {
            if (ReferenceEquals(v, close))
            {
                return true;
            }
            if (ReferenceEquals(v, _sheet))
            {
                return false;
            }
        }
        return false;
    }

    private void BeginDrag(bool fromPress, double offsetAtStart)
    {
        if (_y is null)
        {
            return;
        }
        // Catch the sheet where it is, even mid-flight; an overstretched sheet converts back to finger distance.
        _y.Stop();
        var current = _y.Value;
        var origin = current < 0 ? -RubberBand.Invert(-current, Stretch) : current;
        _drag = new SheetDrag { Origin = origin - offsetAtStart, From = DetentIndex, Moved = !fromPress };
        if (!fromPress)
        {
            _sheet?.SetDragging(true);
        }
    }

    private void MoveDrag(double offset)
    {
        if (_drag is null || _y is null || Reduced)
        {
            return;
        }
        if (!_drag.Moved)
        {
            _drag.Moved = true;
            _sheet?.SetDragging(true);
        }
        var raw = _drag.Origin + offset;
        // Down follows the pointer 1:1 so it can dismiss; up past the tallest detent resists like a rubber band.
        _y.Jump(raw < 0 ? -RubberBand.Apply(-raw, Stretch) : raw);
    }

    private void EndDrag(double velocity)
    {
        var state = _drag;
        _drag = null;
        _sheet?.SetDragging(false);
        if (state is null || _y is null)
        {
            return;
        }
        if (!state.Moved)
        {
            if (!_aimClosed && IsOpen)
            {
                _ = Go(_aim, closed: false);
            }
            return;
        }
        _suppressGrabberClick = true;
        var current = _y.Value;
        // A stretched sheet springs back without the pointer's speed, so it never launches past the stretch.
        if (current < 0)
        {
            velocity = 0;
        }
        Span<double> points = stackalloc double[_stops.Length + 1];
        for (var i = 0; i < _stops.Length; i++)
        {
            points[i] = OffsetOf(i);
        }
        points[_stops.Length] = ClosedOffset;
        var target = MomentumProjector.SelectSnapPoint(points, current, velocity,
            SnapProjection.Linear(Projection, state.From, FlickVelocity));
        if (target == _stops.Length)
        {
            // The exit starts in this frame with the pointer's velocity; closing only waits for it to land.
            if (!Reduced)
            {
                _ = Go(0, closed: true, velocity);
            }
            if (!RequestClose(DismissReason.Gesture, null, velocity))
            {
                Rest(state.From);
            }
            return;
        }
        Rest(target, velocity);
    }

    private void OnGrabberClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_suppressGrabberClick)
        {
            _suppressGrabberClick = false;
            return;
        }
        Rest(DetentIndex == Top ? 0 : Top);
    }

    private void OnGrabberKeyDown(object? sender, KeyEventArgs e)
    {
        _suppressGrabberClick = false;
        int? next = e.Key switch
        {
            Key.Up => Math.Min(DetentIndex + 1, Top),
            Key.Down => Math.Max(DetentIndex - 1, 0),
            Key.Home => Top,
            Key.End => 0,
            _ => null
        };
        if (next is { } stop)
        {
            e.Handled = true;
            Rest(stop);
        }
    }

    private void OnSheetKeyDown(object? sender, KeyEventArgs e)
    {
        // Reading keys on the sheet itself (or its content area) open it first, then scroll the content.
        var body = _sheet?.Body;
        if (e.Handled || body is null || !(ReferenceEquals(e.Source, _sheet) || ReferenceEquals(e.Source, body)))
        {
            return;
        }
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var down = e.Key is Key.Down or Key.PageDown || (e.Key == Key.Space && !shift);
        var up = e.Key is Key.Up or Key.PageUp || (e.Key == Key.Space && shift);
        if (!down && !up)
        {
            return;
        }
        e.Handled = true;
        if (down && DetentIndex != Top)
        {
            Rest(Top);
            return;
        }
        var step = e.Key is Key.Up or Key.Down ? 48 : body.Viewport.Height * 0.85;
        var max = Math.Max(0, body.Extent.Height - body.Viewport.Height);
        body.Offset = body.Offset.WithY(Math.Clamp(body.Offset.Y + (down ? step : -step), 0, max));
    }

    private void OnBodyWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DetentIndex == Top)
        {
            return;
        }
        // Below the tallest detent the content does not scroll; scrolling down opens the sheet the rest of the way.
        e.Handled = true;
        if (e.Delta.Y < 0 && _drag is null)
        {
            Rest(Top);
        }
    }

    private void OnBodyGotFocus(object? sender, FocusChangedEventArgs e)
    {
        // Tabbing into content still below the fold opens the sheet the rest of the way.
        if (DetentIndex == Top || e.Source is not Visual target || Presence is not { } presence)
        {
            return;
        }
        var root = TopLevel.GetTopLevel(target);
        if (root is null || target.TranslatePoint(new Point(0, target.Bounds.Height), root) is not { } bottom)
        {
            return;
        }
        if (bottom.Y > presence.LayerSize.Height - 8)
        {
            Rest(Top);
        }
    }

    #endregion
}
