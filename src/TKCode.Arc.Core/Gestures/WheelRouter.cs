using Avalonia;
using Avalonia.Input;

namespace TKCode.Arc.Gestures;

/// <summary>What the mouse wheel and trackpad gestures do over a pan-and-zoom surface (<see cref="WheelRouter"/>).</summary>
public enum WheelMode
{
    /// <summary>
    /// Wheel and two-finger scrolling pan the surface (Shift turns a vertical wheel sideways); Ctrl / ⌘ + wheel and a
    /// trackpad or touch pinch zoom. The surface owns the wheel, as in design tools and node editors (Workflow).
    /// </summary>
    Pan,

    /// <summary>Every wheel event zooms; the page needs room to scroll around the surface (Semantic zoom).</summary>
    Zoom,

    /// <summary>Notched mouse wheels, Ctrl / ⌘ + wheel and pinches zoom; trackpad scrolling passes to the page (Customer galaxy).</summary>
    NotchedZoom,

    /// <summary>Only Ctrl / ⌘ + wheel and pinches zoom; plain scrolling passes to the page (Activity terrain).</summary>
    ModifierZoom,

    /// <summary>The surface ignores the wheel and pinches; they pass to the page (Revenue globe).</summary>
    PassThrough
}

/// <summary>What kind of device produced a wheel event (a guess; Avalonia does not say).</summary>
public enum WheelDevice
{
    /// <summary>A mouse wheel: whole notches, vertical only.</summary>
    Notched,

    /// <summary>A trackpad or precise wheel: fractional or two-axis deltas.</summary>
    Precise
}

/// <summary>What a routed wheel or pinch event asks the surface to do.</summary>
public enum WheelActionKind
{
    /// <summary>Nothing: leave the event unhandled so the page scrolls.</summary>
    None,

    /// <summary>Move the content by <see cref="WheelAction.Pan"/> screen pixels.</summary>
    Pan,

    /// <summary>Zoom by <see cref="WheelAction.ZoomFactor"/> about the pointer.</summary>
    Zoom
}

/// <summary>The outcome of <see cref="WheelRouter.Route"/> and the pinch methods.</summary>
/// <param name="Kind">What to do.</param>
/// <param name="Pan">For <see cref="WheelActionKind.Pan"/>: how far the content moves on screen, in pixels.</param>
/// <param name="ZoomFactor">For <see cref="WheelActionKind.Zoom"/>: the scale change (above 1 zooms in).</param>
public readonly record struct WheelAction(WheelActionKind Kind, Vector Pan, double ZoomFactor)
{
    /// <summary>Leave the event to the page.</summary>
    public static WheelAction None => new(WheelActionKind.None, default, 1);

    /// <summary>A pan by <paramref name="delta"/> screen pixels.</summary>
    public static WheelAction PanBy(Vector delta) => new(WheelActionKind.Pan, delta, 1);

    /// <summary>A zoom by <paramref name="factor"/>.</summary>
    public static WheelAction ZoomBy(double factor) => new(WheelActionKind.Zoom, default, factor);

    /// <summary>True when the surface should mark the event handled.</summary>
    public bool IsHandled => Kind != WheelActionKind.None;
}

/// <summary>
/// Routes wheel, trackpad magnify and touch pinch input over a pan-and-zoom surface per <see cref="Mode"/> (S10).
/// Pure state and math, so it is unit-testable; the control forwards its events and applies the returned
/// <see cref="WheelAction"/> to its view (usually a <see cref="PanZoomTransform"/>).
/// <para>
/// Zoom follows Arc's web formula on the browser's <c>deltaY</c> in px, where one mouse notch is 100px and positive
/// scrolls down: <c>zoom × exp(−deltaY·k)</c>. Avalonia reports one notch as <c>Delta.Y = ±1</c>, positive meaning
/// up, so the factor is <c>exp(Delta.Y·100·k)</c>: one notch up at k = 0.0022 zooms in ×1.246.
/// </para>
/// </summary>
public sealed class WheelRouter
{
    /// <summary>Web pixels per Avalonia wheel unit (one notch = 100px in Chromium).</summary>
    public const double WebPixelsPerNotch = 100;

    /// <summary>Plain wheel zoom: <c>exp(−deltaY·0.0022)</c>, ×1.246 per notch (Customer galaxy, Semantic zoom).</summary>
    public const double DefaultWheelSensitivity = 0.0022;

    /// <summary>Trackpad magnify: <c>(1 + m)^(100·0.01)</c>, which follows the fingers exactly.</summary>
    public const double DefaultMagnifySensitivity = 0.01;

    /// <summary>Screen pixels a pan moves per Avalonia wheel unit (Avalonia's <c>ScrollViewer</c> uses 50).</summary>
    public const double DefaultPixelsPerWheelUnit = 50;

    private TimeSpan _lastWheel = TimeSpan.MinValue;
    private bool _precise;
    private double _lastPinchScale = 1;

    /// <summary>What the wheel does (default <see cref="WheelMode.Pan"/>).</summary>
    public WheelMode Mode { get; set; } = WheelMode.Pan;

    /// <summary>Zoom sensitivity of a plain wheel per web px (default <see cref="DefaultWheelSensitivity"/>).</summary>
    public double WheelSensitivity { get; set; } = DefaultWheelSensitivity;

    /// <summary>Zoom sensitivity of Ctrl / ⌘ + wheel per web px (default <see cref="DefaultWheelSensitivity"/>).</summary>
    public double ModifierSensitivity { get; set; } = DefaultWheelSensitivity;

    /// <summary>Sensitivity of a trackpad magnify gesture (default <see cref="DefaultMagnifySensitivity"/>).</summary>
    public double MagnifySensitivity { get; set; } = DefaultMagnifySensitivity;

    /// <summary>Pan distance per wheel unit in <see cref="WheelMode.Pan"/> (default <see cref="DefaultPixelsPerWheelUnit"/>).</summary>
    public double PixelsPerWheelUnit { get; set; } = DefaultPixelsPerWheelUnit;

    /// <summary>A wheel pause longer than this starts a new burst for device detection (default 300ms).</summary>
    public TimeSpan BurstGap { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Routes a wheel event: its <paramref name="delta"/> (<c>PointerWheelEventArgs.Delta</c>), modifiers and time on
    /// any monotonic clock (the event timestamp).
    /// </summary>
    public WheelAction Route(Vector delta, KeyModifiers modifiers, TimeSpan time)
    {
        var device = ClassifyBurst(delta, time);
        var modifier = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        var zoom = Mode switch
        {
            WheelMode.Pan => modifier,
            WheelMode.Zoom => true,
            WheelMode.NotchedZoom => modifier || device == WheelDevice.Notched,
            WheelMode.ModifierZoom => modifier,
            _ => false
        };
        if (zoom)
        {
            if (delta.Y == 0)
            {
                return WheelAction.None;
            }
            return WheelAction.ZoomBy(ZoomFactor(delta.Y, modifier ? ModifierSensitivity : WheelSensitivity));
        }
        if (Mode != WheelMode.Pan || delta == default)
        {
            return WheelAction.None;
        }
        // Shift turns a vertical mouse wheel sideways; trackpads already report both axes.
        if (modifiers.HasFlag(KeyModifiers.Shift) && delta.X == 0)
        {
            delta = new Vector(delta.Y, 0);
        }
        return WheelAction.PanBy(delta * PixelsPerWheelUnit);
    }

    /// <summary>Routes a trackpad magnify gesture (<c>PointerTouchPadGestureMagnify</c>; its <c>Delta.X</c> is the relative scale change).</summary>
    public WheelAction RouteMagnify(double magnification)
    {
        if (Mode == WheelMode.PassThrough)
        {
            return WheelAction.None;
        }
        return WheelAction.ZoomBy(MagnifyFactor(magnification, MagnifySensitivity));
    }

    /// <summary>
    /// Routes a touch pinch update (<c>PinchEventArgs.Scale</c>, the scale since the gesture began) as the change since
    /// the previous update; call <see cref="EndPinch"/> when the gesture ends.
    /// </summary>
    public WheelAction RoutePinch(double scaleSinceStart)
    {
        if (Mode == WheelMode.PassThrough || !(scaleSinceStart > 0))
        {
            return WheelAction.None;
        }
        var factor = scaleSinceStart / _lastPinchScale;
        _lastPinchScale = scaleSinceStart;
        return WheelAction.ZoomBy(factor);
    }

    /// <summary>Ends a touch pinch.</summary>
    public void EndPinch() => _lastPinchScale = 1;

    /// <summary>Forgets the current wheel burst and pinch.</summary>
    public void Reset()
    {
        _lastWheel = TimeSpan.MinValue;
        _precise = false;
        _lastPinchScale = 1;
    }

    /// <summary>
    /// Tells a mouse wheel from a trackpad across a burst: once an event of a burst looks precise (see
    /// <see cref="Classify"/>), the rest of the burst counts as precise until the wheel rests for <see cref="BurstGap"/>.
    /// </summary>
    public WheelDevice ClassifyBurst(Vector delta, TimeSpan time)
    {
        if (_lastWheel == TimeSpan.MinValue || time - _lastWheel > BurstGap || time < _lastWheel)
        {
            _precise = false;
        }
        _lastWheel = time;
        if (Classify(delta) == WheelDevice.Precise)
        {
            _precise = true;
        }
        return _precise ? WheelDevice.Precise : WheelDevice.Notched;
    }

    /// <summary>
    /// A stateless guess at the device: whole vertical notches are a mouse wheel; anything fractional or with a
    /// horizontal part is a trackpad.
    /// </summary>
    public static WheelDevice Classify(Vector delta)
    {
        var y = Math.Abs(delta.Y);
        if (Math.Abs(delta.X) > 1e-6 || y < 0.99 || Math.Abs(y - Math.Round(y)) > 0.02)
        {
            return WheelDevice.Precise;
        }
        return WheelDevice.Notched;
    }

    /// <summary>The zoom factor of an Avalonia wheel delta (positive Y zooms in): <c>exp(Delta.Y·100·k)</c>.</summary>
    public static double ZoomFactor(double avaloniaDeltaY, double sensitivity)
        => Math.Exp(avaloniaDeltaY * WebPixelsPerNotch * sensitivity);

    /// <summary>
    /// The zoom factor of a trackpad magnify gesture with relative scale change <paramref name="magnification"/>.
    /// Chromium turns a pinch into Ctrl + wheel with <c>deltaY = −100·ln(1 + m)</c>, so the factor is
    /// <c>(1 + m)^(100·k)</c> (exactly the pinch at k = 0.01).
    /// </summary>
    public static double MagnifyFactor(double magnification, double sensitivity)
    {
        var scale = 1 + magnification;
        return scale <= 0 ? 1 : Math.Pow(scale, WebPixelsPerNotch * sensitivity);
    }
}
