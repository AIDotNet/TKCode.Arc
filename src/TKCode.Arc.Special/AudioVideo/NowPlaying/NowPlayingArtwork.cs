using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The artwork: a rounded box on the muted surface whose image is always a square as wide as the box, centred vertically
/// and cropped (so the 44px thumbnail grows into the 310 × 192 banner without distortion). A track change slides the old
/// cover 24% of the width toward the side it leaves by, shrinking it to 0.96 and blurring it to 4px, while the new one
/// comes in from the other side (smooth spring) and the two crossfade over 240ms ease-out. Reduced motion only crossfades.
/// <para>Why code: the slide and blur ride a spring that a quick second skip retargets.</para>
/// </summary>
internal sealed class NowPlayingArtwork : Border
{
    private readonly Panel _host;
    private readonly NowPlayingArtLayer _a;
    private readonly NowPlayingArtLayer _b;
    private NowPlayingArtLayer _current;
    private MotionValue<double>? _slide;
    private MotionValue<double>? _fade;
    private double _sign;
    private bool _travel;
    private string? _alt;

    public NowPlayingArtwork()
    {
        ClipToBounds = true;
        _a = new NowPlayingArtLayer();
        _b = new NowPlayingArtLayer { IsVisible = false };
        _current = _a;
        _host = new Panel { Children = { _a, _b } };
        Child = _host;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The image showing now (the incoming one during a swap).</summary>
    public IImage? Image => _current.Image;

    /// <summary>True while a swap is moving.</summary>
    public bool IsSwapping => _slide?.IsAnimating == true || _fade?.IsAnimating == true;

    internal NowPlayingArtLayer CurrentLayer => _current;

    internal NowPlayingArtLayer OtherLayer => _current == _a ? _b : _a;

    /// <summary>The description for screen readers; null makes the artwork decorative.</summary>
    public string? Alt
    {
        get => _alt;
        set
        {
            _alt = value;
            AutomationProperties.SetName(this, value ?? string.Empty);
            AutomationProperties.SetAccessibilityView(this, string.IsNullOrEmpty(value) ? AccessibilityView.Raw : AccessibilityView.Content);
        }
    }

    public void Show(IImage? image, NowPlayingSkipDirection direction, bool animate)
    {
        if (!animate)
        {
            _slide?.Jump(1);
            _fade?.Jump(1);
            _current.Image = image;
            _current.IsVisible = true;
            _current.Pose(0, 1, 0, 1);
            var other = OtherLayer;
            other.Image = null;
            other.IsVisible = false;
            return;
        }
        // A skip during a skip: settle the running one first (the newest cover always slides in from its edge).
        _slide?.Jump(1);
        _fade?.Jump(1);
        var incoming = OtherLayer;
        _current = incoming;
        incoming.Image = image;
        incoming.IsVisible = true;
        _sign = direction == NowPlayingSkipDirection.Forward ? 1 : -1;
        _travel = ArcMotion.IsFull(this);
        _slide ??= Create(ApplySlide);
        _fade ??= Create(ApplyFade);
        _slide.Jump(0);
        _fade.Jump(0);
        _ = _fade.AnimateTo(1, MotionSpec.Tween(NowPlayingMotion.Fade, NowPlayingMotion.EaseOut));
        if (_travel)
        {
            _ = _slide.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _slide.Jump(1);
        }
    }

    private MotionValue<double> Create(Action<double> apply)
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += apply;
        return value;
    }

    private void ApplySlide(double q) => Pose();

    private void ApplyFade(double f) => Pose();

    private void Pose()
    {
        var q = _slide?.Value ?? 1;
        var f = Math.Clamp(_fade?.Value ?? 1, 0, 1);
        var incoming = _current;
        var outgoing = OtherLayer;
        var w = Bounds.Width;
        if (_travel)
        {
            incoming.Pose(_sign * 0.24 * w * (1 - q), 0.96 + 0.04 * q, 4 * (1 - q), f);
            outgoing.Pose(-_sign * 0.24 * w * q, 1 - 0.04 * q, 4 * q, 1 - f);
        }
        else
        {
            incoming.Pose(0, 1, 0, f);
            outgoing.Pose(0, 1, 0, 1 - f);
        }
        var done = q >= 1 && f >= 1;
        if (done)
        {
            outgoing.IsVisible = false;
            outgoing.Image = null;
            incoming.Pose(0, 1, 0, 1);
        }
    }
}

/// <summary>One cover of the artwork: the image as a square as wide as the box, centred vertically, cropped by the box.</summary>
internal sealed class NowPlayingArtLayer : Control
{
    private readonly MatrixTransform _transform = new();
    private IImage? _image;

    public NowPlayingArtLayer()
    {
        IsHitTestVisible = false;
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.Center;
    }

    public IImage? Image
    {
        get => _image;
        set
        {
            if (!ReferenceEquals(_image, value))
            {
                _image = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>Places the cover: x offset, scale, blur radius and opacity.</summary>
    public void Pose(double x, double scale, double blur, double opacity)
    {
        _transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, 0);
        Opacity = Math.Clamp(opacity, 0, 1);
        Effect = ArcBlur.Get(blur);
    }

    public override void Render(DrawingContext context)
    {
        if (_image is not { } image)
        {
            return;
        }
        var w = Bounds.Width;
        var h = Bounds.Height;
        var side = Math.Max(w, h);
        // A bitmap's source rectangle is in pixels (its Size is in DIPs).
        var source = image is Bitmap bitmap ? new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height) : image.Size;
        if (side <= 0 || source.Width <= 0 || source.Height <= 0)
        {
            return;
        }
        // Cover a square: crop the source to its centred square, then draw it side × side, centred in the box.
        var crop = Math.Min(source.Width, source.Height);
        var src = new Rect((source.Width - crop) / 2, (source.Height - crop) / 2, crop, crop);
        context.DrawImage(image, src, new Rect((w - side) / 2, (h - side) / 2, side, side));
    }
}
