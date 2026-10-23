using Avalonia;

namespace TKCode.Arc;

/// <summary>What kind of device produced a wheel event (a guess; Avalonia does not say).</summary>
internal enum ShowpieceWheelDevice
{
    /// <summary>A mouse wheel: whole notches, vertical only.</summary>
    Notched,

    /// <summary>A trackpad or precise wheel: fractional or two-axis deltas.</summary>
    Precise
}

/// <summary>
/// Wheel and pinch normalization for the Showpieces. Arc measures zoom on the web's <c>deltaY</c> in px, where one
/// mouse notch is 100px and positive means scrolling down: <c>zoom × exp(−deltaY·k)</c>. Avalonia reports one notch as
/// <c>Delta.Y = ±1</c> with positive meaning up, so <c>deltaY = −Delta.Y·100</c> and the factor is
/// <c>exp(Delta.Y·100·k)</c>: one notch up at k = 0.0022 zooms in ×1.246.
/// </summary>
internal static class ShowpieceWheel
{
    /// <summary>Web pixels per Avalonia wheel unit (one notch = 100px in Chromium).</summary>
    public const double PixelsPerNotch = 100;

    /// <summary>Plain wheel zoom (Customer galaxy, Semantic zoom): <c>exp(−deltaY·0.0022)</c>, ×1.246 per notch.</summary>
    public const double WheelSensitivity = 0.0022;

    /// <summary>Ctrl + wheel / trackpad pinch in Semantic zoom: <c>exp(−deltaY·0.012)</c>, ×3.3 per notch.</summary>
    public const double PinchSensitivity = 0.012;

    /// <summary>Ctrl + wheel / pinch in Activity terrain: <c>exp(−deltaY·0.01)</c> (deltaY −20 → ×1.2214).</summary>
    public const double OrbitPinchSensitivity = 0.01;

    /// <summary>The web <c>deltaY</c> (px, positive down) of an Avalonia wheel delta.</summary>
    public static double WebDeltaY(double avaloniaDeltaY) => -avaloniaDeltaY * PixelsPerNotch;

    /// <summary>The zoom factor of a web <c>deltaY</c>: <c>exp(−deltaY·sensitivity)</c>.</summary>
    public static double ZoomFactorFromWebDelta(double webDeltaY, double sensitivity) => Math.Exp(-webDeltaY * sensitivity);

    /// <summary>The zoom factor of an Avalonia wheel delta (positive Y zooms in).</summary>
    public static double ZoomFactor(double avaloniaDeltaY, double sensitivity)
        => ZoomFactorFromWebDelta(WebDeltaY(avaloniaDeltaY), sensitivity);

    /// <summary>
    /// The zoom factor of a trackpad magnify gesture (macOS: <c>Gestures.PointerTouchPadGestureMagnify</c>, delta =
    /// relative scale change m). Chromium turns a pinch into Ctrl + wheel with <c>deltaY = −100·ln(1 + m)</c>, so the
    /// same sensitivities apply: the factor is <c>(1 + m)^(100·k)</c> (exactly the pinch at k = 0.01).
    /// </summary>
    public static double MagnifyFactor(double magnification, double sensitivity)
    {
        var scale = 1 + magnification;
        return scale <= 0 ? 1 : Math.Pow(scale, PixelsPerNotch * sensitivity);
    }

    /// <summary>
    /// A stateless guess at the device: whole vertical notches are a mouse wheel; anything fractional or with a
    /// horizontal part is a trackpad. <see cref="ShowpieceWheelClassifier"/> also uses the timing of a burst.
    /// </summary>
    public static ShowpieceWheelDevice Classify(Vector delta)
    {
        var y = Math.Abs(delta.Y);
        if (Math.Abs(delta.X) > 1e-6 || y < 0.99 || Math.Abs(y - Math.Round(y)) > 0.02)
        {
            return ShowpieceWheelDevice.Precise;
        }
        return ShowpieceWheelDevice.Notched;
    }
}

/// <summary>
/// Tells a mouse wheel from a trackpad across a burst of wheel events: once an event of a burst looks precise
/// (fractional or two-axis, see <see cref="ShowpieceWheel.Classify"/>), the rest of the burst counts as precise until
/// the wheel rests for <see cref="BurstGap"/>. Customer galaxy zooms on notched wheels and Ctrl only, so a trackpad
/// two-finger scroll keeps scrolling the page.
/// </summary>
internal sealed class ShowpieceWheelClassifier
{
    private TimeSpan _last = TimeSpan.MinValue;
    private bool _precise;

    /// <summary>A pause longer than this starts a new burst (default 300ms).</summary>
    public TimeSpan BurstGap { get; init; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Classifies an event at <paramref name="time"/> (any monotonic clock).</summary>
    public ShowpieceWheelDevice Classify(Vector delta, TimeSpan time)
    {
        if (_last == TimeSpan.MinValue || time - _last > BurstGap || time < _last)
        {
            _precise = false;
        }
        _last = time;
        if (ShowpieceWheel.Classify(delta) == ShowpieceWheelDevice.Precise)
        {
            _precise = true;
        }
        return _precise ? ShowpieceWheelDevice.Precise : ShowpieceWheelDevice.Notched;
    }

    /// <summary>Forgets the current burst.</summary>
    public void Reset()
    {
        _last = TimeSpan.MinValue;
        _precise = false;
    }
}
