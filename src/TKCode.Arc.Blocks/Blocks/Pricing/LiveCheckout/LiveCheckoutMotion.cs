using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The curves measured on the reference checkout (Arc LiveCheckout, design reference).</summary>
internal static class LiveCheckoutMotion
{
    /// <summary>
    /// Rolling digits, label swaps, the valid check pop and the add-on tick: a critically damped spring fitted at ω 20.0,
    /// ζ 1.00 (k 400, c 40). Arc calls it "snappy", but our Snappy carries bounce, so it is written out.
    /// </summary>
    public static readonly SpringSpec Roll = SpringSpec.FromPhysical(400, 40);

    /// <summary>The summary glide, the plan ring, the billing pill, row heights and the receipt rise (Smooth, fitted ω 12.5–13.2).</summary>
    public static readonly SpringSpec Smooth = ArcMotionPresets.Smooth;

    /// <summary>The narrow summary's unfold: rows open over 480 ms on the standard curve.</summary>
    public static readonly TimeSpan UnfoldHeight = ArcMotionPresets.Considered;

    /// <summary>The narrow summary's unfold: content fades in over 240 ms on the standard curve.</summary>
    public static readonly TimeSpan UnfoldFade = ArcMotionPresets.Standard;

    /// <summary>The form leaves over 240 ms on the exit curve.</summary>
    public static readonly TimeSpan FormExit = ArcMotionPresets.Standard;

    /// <summary>The old summary heading fades out in about 150 ms.</summary>
    public static readonly TimeSpan HeadOut = TimeSpan.FromMilliseconds(150);

    /// <summary>A quick fade (the coupon button leaving, ~170 ms).</summary>
    public static readonly TimeSpan QuickFade = TimeSpan.FromMilliseconds(170);

    /// <summary>A short fade back in (the coupon button returning, ~120 ms).</summary>
    public static readonly TimeSpan ReturnFade = ArcMotionPresets.Instant;

    public static Easing Standard => ArcEasings.Standard;

    public static Easing Exit => ArcEasings.Exit;

    public static Easing Enter => ArcEasings.Enter;

    /// <summary>True when <paramref name="visual"/> is shown and motion is full.</summary>
    public static bool CanAnimate(Visual visual) => visual is Control { IsLoaded: true } && ArcMotion.IsFull(visual);
}

/// <summary>
/// Shows and hides one part with its own opacity curve and its own shape curve (offset, scale, blur), so that, as measured,
/// a heading can fade in over 240 ms while it rises on a delayed spring. Hidden parts are taken out of view
/// (<see cref="Visual.IsVisible"/> false) once both curves are done.
/// </summary>
/// <remarks>
/// Code-driven because the fade and the movement run on different curves with different delays, and the part must be
/// hidden only when both are done, which a ControlTheme transition cannot express.
/// </remarks>
internal sealed class LiveCheckoutPresence
{
    private readonly Control _target;
    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<double> _shape;
    private readonly TranslateTransform _translate = new();
    private readonly ScaleTransform _scale = new();
    private int _generation;

    public LiveCheckoutPresence(Control target, Vector offset = default, double scale = 1, double blur = 0, bool visible = true)
    {
        _target = target;
        Offset = offset;
        Scale = scale;
        Blur = blur;
        var group = new TransformGroup();
        group.Children.Add(_scale);
        group.Children.Add(_translate);
        target.RenderTransform = group;
        target.RenderTransformOrigin = RelativePoint.Center;
        _opacity = new MotionValue<double>(target, visible ? 1 : 0, MotionVectors.Double);
        _opacity.Changed += v => _target.Opacity = Math.Clamp(v, 0, 1);
        _shape = new MotionValue<double>(target, visible ? 1 : 0, MotionVectors.Double);
        _shape.Changed += _ => ApplyShape();
        IsShown = visible;
        target.IsVisible = visible;
        target.Opacity = visible ? 1 : 0;
        ApplyShape();
    }

    /// <summary>Offset of the hidden state (the part moves from here to rest).</summary>
    public Vector Offset { get; set; }

    /// <summary>Scale of the hidden state.</summary>
    public double Scale { get; set; }

    /// <summary>Blur radius of the hidden state.</summary>
    public double Blur { get; set; }

    /// <summary>Raised when a hide has finished and the part is out of view.</summary>
    public event EventHandler? Hidden;

    /// <summary>True from <see cref="Show"/> until the next <see cref="Hide"/>.</summary>
    public bool IsShown { get; private set; }

    /// <summary>Current opacity value (tests).</summary>
    public double OpacityValue => _opacity.Value;

    /// <summary>Current shape progress, 0 hidden pose to 1 rest (tests).</summary>
    public double ShapeValue => _shape.Value;

    /// <summary>Brings the part in; without <paramref name="animate"/> (or below full motion) it appears at once.</summary>
    public void Show(bool animate, MotionSpec opacity, MotionSpec? shape = null)
    {
        _generation++;
        IsShown = true;
        _target.IsVisible = true;
        if (!animate)
        {
            _opacity.Jump(1);
            _shape.Jump(1);
            return;
        }
        if (_opacity.Value <= 0.001 && !_opacity.IsAnimating)
        {
            _shape.Jump(0);
        }
        _ = _opacity.AnimateTo(1, opacity);
        _ = _shape.AnimateTo(1, shape ?? opacity);
    }

    /// <summary>Takes the part out; it is hidden once both curves are done.</summary>
    public async void Hide(bool animate, MotionSpec opacity, MotionSpec? shape = null)
    {
        var generation = ++_generation;
        IsShown = false;
        if (!animate || !_target.IsVisible)
        {
            _opacity.Jump(0);
            _shape.Jump(0);
            _target.IsVisible = false;
            Hidden?.Invoke(this, EventArgs.Empty);
            return;
        }
        var a = _opacity.AnimateTo(0, opacity);
        var b = _shape.AnimateTo(0, shape ?? opacity);
        await Task.WhenAll(a, b);
        if (generation == _generation && !IsShown)
        {
            _target.IsVisible = false;
            Hidden?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sets the final state at once.</summary>
    public void Jump(bool shown)
    {
        _generation++;
        IsShown = shown;
        _opacity.Jump(shown ? 1 : 0);
        _shape.Jump(shown ? 1 : 0);
        _target.IsVisible = shown;
        if (!shown)
        {
            Hidden?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyShape()
    {
        var hidden = 1 - Math.Clamp(_shape.Value, 0, 1.2);
        _translate.X = Offset.X * hidden;
        _translate.Y = Offset.Y * hidden;
        var s = 1 + (Scale - 1) * hidden;
        _scale.ScaleX = s;
        _scale.ScaleY = s;
        _target.Effect = Blur > 0 ? ArcBlur.Get(Math.Max(0, Blur * hidden)) : null;
    }
}
