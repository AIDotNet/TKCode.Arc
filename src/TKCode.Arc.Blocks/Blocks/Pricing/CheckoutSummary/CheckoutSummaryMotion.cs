using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Motion constants of <see cref="CheckoutSummary"/> as measured on the reference (Arc CheckoutSummary（设计参考）, notes
/// section 6): every spring fits Arc's smooth spring (visual duration 0.4 s, no bounce); fades are CSS ease-out or the
/// enter curve.
/// </summary>
internal static class CheckoutSummaryMotion
{
    /// <summary>The smooth spring (ω ≈ 13.1, critically damped) used by every height, scale and wheel.</summary>
    public static readonly SpringSpec Smooth = SpringSpec.FromVisual(0.4, 0);

    /// <summary>CSS <c>ease-out</c>.</summary>
    public static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    /// <summary>CSS <c>ease-in-out</c> (inside each shake segment).</summary>
    public static readonly Easing EaseInOut = new SplineEasing(0.42, 0, 0.58, 1);

    /// <summary>Exit fades: 160 ms.</summary>
    public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(160);

    /// <summary>Enter fades: 240 ms.</summary>
    public static readonly TimeSpan Standard = TimeSpan.FromMilliseconds(240);

    /// <summary>Length of the refusal shake.</summary>
    public static readonly TimeSpan ShakeDuration = TimeSpan.FromMilliseconds(360);

    private static readonly double[] s_shake = [0, -6, 6, -4, 3, 0];

    /// <summary>Horizontal offset of the refusal shake at <paramref name="t"/> (0–1): keyframes 0, −6, 6, −4, 3, 0 in equal steps.</summary>
    public static double ShakeAt(double t, double amplitude = 6)
    {
        t = Math.Clamp(t, 0, 1);
        var segments = s_shake.Length - 1;
        var position = t * segments;
        var index = Math.Min(segments - 1, (int)position);
        var local = EaseInOut.Ease(position - index);
        var from = s_shake[index];
        var to = s_shake[index + 1];
        return (from + (to - from) * local) * amplitude / 6;
    }
}

/// <summary>
/// Shows and hides one child by springing its height (padding included) and fading it, like the reference's breakdown
/// lines and error lines. Code-driven because the target height is measured content and an interrupted spring keeps its
/// velocity; a ControlTheme transition cannot animate to "auto".
/// </summary>
internal sealed class CheckoutSummaryCollapse : Decorator
{
    private MotionValue<double>? _height;
    private MotionValue<double>? _opacity;
    private bool _open = true;
    private bool _initialized;
    private int _version;
    private bool _deferred;

    static CheckoutSummaryCollapse()
    {
        ClipToBoundsProperty.OverrideDefaultValue<CheckoutSummaryCollapse>(true);
    }

    /// <summary>When true the opacity follows the same spring as the height (error lines); otherwise it fades on <see cref="FadeIn"/> / <see cref="FadeOut"/>.</summary>
    public bool FadeWithSpring { get; set; }

    /// <summary>Fade in when opening (default 240 ms ease-out).</summary>
    public TimeSpan FadeIn { get; set; } = CheckoutSummaryMotion.Standard;

    /// <summary>Fade out when closing (default 240 ms ease-out).</summary>
    public TimeSpan FadeOut { get; set; } = CheckoutSummaryMotion.Standard;

    /// <summary>True while shown or opening.</summary>
    public bool IsOpen => _open;

    /// <summary>Current height fraction (0 closed, 1 open).</summary>
    public double Fraction => _height?.Value ?? (_open ? 1 : 0);

    /// <summary>Raised when a close finished; the child is hidden by then.</summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Opens or closes; animated when <paramref name="animate"/> and the motion level is Full. A request made before the
    /// control is loaded (a line just inserted) waits for <see cref="Control.Loaded"/>.
    /// </summary>
    public void SetOpen(bool open, bool animate = true)
    {
        if (_initialized && open == _open && !_deferred)
        {
            return;
        }
        var wasOpen = _open;
        _open = open;
        var version = ++_version;
        if (open && Child is { } child)
        {
            child.IsVisible = true;
        }
        if (animate && _initialized && !IsLoaded)
        {
            // Hold the previous state until loaded, then animate from it.
            _height ??= CreateHeight(wasOpen ? 1 : 0);
            _opacity ??= CreateOpacity(wasOpen ? 1 : 0);
            Opacity = _opacity.Value;
            InvalidateMeasure();
            _deferred = true;
            return;
        }
        _deferred = false;
        if (!animate || !_initialized || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _initialized = true;
            _height?.Jump(open ? 1 : 0);
            _opacity?.Jump(open ? 1 : 0);
            Opacity = open ? 1 : 0;
            InvalidateMeasure();
            if (!open)
            {
                FinishClose(version);
            }
            return;
        }
        Animate(open, version);
    }

    private void Animate(bool open, int version)
    {
        _height ??= CreateHeight(open ? 0 : 1);
        _opacity ??= CreateOpacity(open ? 0 : 1);
        var heightTask = _height.AnimateTo(open ? 1 : 0, MotionSpec.Spring(CheckoutSummaryMotion.Smooth));
        var fade = FadeWithSpring
            ? MotionSpec.Spring(CheckoutSummaryMotion.Smooth)
            : MotionSpec.Tween(open ? FadeIn : FadeOut, CheckoutSummaryMotion.EaseOut);
        _ = _opacity.AnimateTo(open ? 1 : 0, fade);
        if (!open)
        {
            _ = CloseWhenDone(heightTask, version);
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_deferred)
        {
            _deferred = false;
            if (ArcMotion.IsFull(this))
            {
                Animate(_open, _version);
            }
            else
            {
                _height?.Jump(_open ? 1 : 0);
                _opacity?.Jump(_open ? 1 : 0);
                Opacity = _open ? 1 : 0;
                InvalidateMeasure();
                if (!_open)
                {
                    FinishClose(_version);
                }
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_initialized)
        {
            _initialized = true;
            Opacity = _open ? 1 : 0;
            if (Child is { } child)
            {
                child.IsVisible = _open;
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChildProperty && change.NewValue is Control child && !_open)
        {
            child.IsVisible = false;
        }
    }

    private async Task CloseWhenDone(Task<bool> task, int version)
    {
        await task;
        FinishClose(version);
    }

    private void FinishClose(int version)
    {
        if (version != _version || _open)
        {
            return;
        }
        if (Child is { } child)
        {
            child.IsVisible = false;
        }
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private MotionValue<double> CreateHeight(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    private MotionValue<double> CreateOpacity(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        var fraction = Child is { IsVisible: true } ? Math.Clamp(Fraction, 0, 1) : 0;
        return size.WithHeight(size.Height * fraction);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            // The child keeps its full height and is clipped, so text never squashes while the line opens.
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(child.DesiredSize.Height, finalSize.Height)));
        }
        return finalSize;
    }
}

/// <summary>
/// Swaps a label in place: the old content fades out over 160 ms (ease-out) while rising 8 px, the new one fades in
/// over 240 ms on the enter curve from 8 px below, both at once and always upward (notes 6.5). Reduced motion keeps a
/// 240 ms crossfade without travel. Code-driven because two contents are alive at once during the swap.
/// </summary>
internal sealed class CheckoutSummaryLabelSwap : Control
{
    private const double Travel = 8;
    private Control? _current;

    /// <summary>The content shown now.</summary>
    public Control? Current => _current;

    /// <summary>Shows <paramref name="next"/>; animates when <paramref name="animate"/> and the control is loaded.</summary>
    public void Swap(Control next, bool animate = true)
    {
        var old = _current;
        _current = next;
        AddChild(next);
        var level = ArcMotion.GetEffectiveLevel(this);
        if (old is null || !animate || !IsLoaded || level == ArcMotionLevel.Off)
        {
            if (old is not null)
            {
                RemoveChild(old);
            }
            next.Opacity = 1;
            next.RenderTransform = null;
            return;
        }
        var full = level == ArcMotionLevel.Full;
        Animate(old, entering: false, full);
        Animate(next, entering: true, full);
    }

    private void Animate(Control child, bool entering, bool full)
    {
        var move = new TranslateTransform();
        child.RenderTransform = full ? move : null;
        var progress = new MotionValue<double>(this, entering ? 0 : 1, MotionVectors.Double);
        progress.Changed += p =>
        {
            child.Opacity = Math.Clamp(p, 0, 1);
            move.Y = entering ? (1 - p) * Travel : -(1 - p) * Travel;
        };
        child.Opacity = entering ? 0 : 1;
        move.Y = entering ? Travel : 0;
        var spec = entering || !full
            ? MotionSpec.Tween(CheckoutSummaryMotion.Standard, ArcEasings.Enter)
            : MotionSpec.Tween(CheckoutSummaryMotion.Fast, CheckoutSummaryMotion.EaseOut);
        _ = Run(progress, entering ? 1 : 0, spec, entering ? null : child);
    }

    private async Task Run(MotionValue<double> progress, double target, MotionSpec spec, Control? removeWhenDone)
    {
        await progress.AnimateTo(target, spec);
        progress.Dispose();
        if (removeWhenDone is not null && !ReferenceEquals(removeWhenDone, _current))
        {
            RemoveChild(removeWhenDone);
        }
    }

    private void AddChild(Control child)
    {
        if (!VisualChildren.Contains(child))
        {
            LogicalChildren.Add(child);
            VisualChildren.Add(child);
        }
        InvalidateMeasure();
    }

    private void RemoveChild(Control child)
    {
        VisualChildren.Remove(child);
        LogicalChildren.Remove(child);
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        foreach (var visual in VisualChildren)
        {
            if (visual is Control child)
            {
                child.Measure(availableSize);
                if (ReferenceEquals(child, _current))
                {
                    size = child.DesiredSize;
                }
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var visual in VisualChildren)
        {
            if (visual is Control child)
            {
                var desired = child.DesiredSize;
                var x = (finalSize.Width - desired.Width) / 2;
                var y = (finalSize.Height - desired.Height) / 2;
                child.Arrange(new Rect(x, y, desired.Width, desired.Height));
            }
        }
        return finalSize;
    }
}

/// <summary>
/// The reference's spinner: Lucide loader-circle turning once per 700 ms (1.6 s under reduced motion, still under Off).
/// Driven by a <see cref="LiveLoop"/>, so it only runs while shown.
/// </summary>
internal sealed class CheckoutSummarySpinner : Decorator
{
    private readonly RotateTransform _rotate = new();
    private readonly LiveLoop _loop;
    private double _turn;

    public CheckoutSummarySpinner(double size, IBrush? foreground = null)
    {
        var glyph = new ArcGlyph { Data = ArcIcons.LoaderCircle, Size = size, StrokeThickness = 1.75 };
        if (foreground is not null)
        {
            glyph.Foreground = foreground;
        }
        Child = glyph;
        RenderTransform = _rotate;
        RenderTransformOrigin = RelativePoint.Center;
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        _loop.Start();
    }

    private bool Step(double seconds)
    {
        var period = ArcMotion.IsFull(this) ? 0.7 : 1.6;
        _turn = (_turn + seconds / period) % 1;
        _rotate.Angle = _turn * 360;
        return true;
    }
}
