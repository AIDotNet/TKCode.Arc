using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The Mobile size of a docs page's Preview (design/engineering/gallery-mobile-preview.md, section 3.3): the demo runs
/// in a 390 × 844 viewport (an <see cref="ArcViewportHost"/>, so modals, popups and viewport-sized controls treat it as
/// the window), scaled into a fixed 334 × 700 phone outline. Only an outline: no status bar or notch; the device
/// simulation is <see cref="PhoneFrame"/>'s job.
/// </summary>
/// <remarks>
/// The outline can morph from another size (the Fit stage's content box) to its own: <see cref="MorphAsync"/>. That is
/// code-driven motion because it starts from a measured auto size, which a ControlTheme transition cannot express.
/// </remarks>
public sealed class GalleryPreviewPhone : Border
{
    /// <summary>The viewport's size, in device-independent pixels (an iPhone 15's, as <see cref="PhoneDevice.IPhone15"/>).</summary>
    public static readonly Size ViewportSize = new(PhoneDevice.IPhone15.Width, PhoneDevice.IPhone15.Height);

    /// <summary>The outline's size on the stage.</summary>
    public static readonly Size OutlineSize = new(334, 700);

    /// <summary>The bezel: the room between the outline and the screen.</summary>
    public const double Bezel = 10;

    // The device's shape, not a theme radius: outer and screen corners of the outline.
    private const double OuterRadius = 46;
    private const double ScreenRadius = 36;
    private const double ContentPadding = 16;

    private readonly Grid _shell;
    private readonly Border _body;
    private readonly ArcViewportHost _viewport;
    private readonly Border _content;
    private readonly Border _center;
    private readonly MotionValue<Size> _size;
    private readonly MotionValue<double> _bodyOpacity;
    private readonly MotionValue<double> _shellOpacity;
    private readonly ScaleTransform _scale;
    private readonly LayoutTransformControl _scaled;

    public GalleryPreviewPhone()
    {
        var scale = (OutlineSize.Height - 2 * Bezel) / ViewportSize.Height;
        Scale = scale;
        _scale = new ScaleTransform(scale, scale);

        // The demo keeps the stage's alignment rules (rows center, the rest stretches) and sits in the middle of the
        // screen; a taller demo scrolls.
        _center = new Border { VerticalAlignment = VerticalAlignment.Center };
        _content = new Border { MinHeight = ViewportSize.Height, Padding = new Thickness(ContentPadding), Child = _center };
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _content
        };
        _viewport = new ArcViewportHost { Width = ViewportSize.Width, Height = ViewportSize.Height };
        _viewport.Children.Add(scroll);

        var scaled = _scaled = new LayoutTransformControl
        {
            LayoutTransform = _scale,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _viewport
        };
        // Popups draw in the window's overlay layer, outside this transform: ArcZoom gives them the same scale.
        ArcZoom.SetScale(scaled, scale);

        var screen = new Border
        {
            Margin = new Thickness(Bezel),
            CornerRadius = new CornerRadius(ScreenRadius),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = scaled
        };
        ArcTokens.Bind(screen, BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(screen, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        _body = new Border { CornerRadius = new CornerRadius(OuterRadius), BorderThickness = new Thickness(1), IsHitTestVisible = false };
        ArcTokens.Bind(_body, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(_body, BorderBrushProperty, ArcDesignTokenKind.BorderStrong);
        ArcTokens.Bind(_body, BoxShadowProperty, ArcDesignTokenKind.ShadowRaised);

        _shell = new Grid
        {
            Width = OutlineSize.Width,
            Height = OutlineSize.Height,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _shell.Children.Add(_body);
        _shell.Children.Add(screen);

        _size = new MotionValue<Size>(_shell, OutlineSize, MotionVectors.Size);
        _size.Changed += size =>
        {
            _shell.Width = size.Width;
            _shell.Height = size.Height;
        };
        _bodyOpacity = new MotionValue<double>(_body, 1, MotionVectors.Double);
        _bodyOpacity.Changed += value => _body.Opacity = value;
        _shellOpacity = new MotionValue<double>(_shell, 1, MotionVectors.Double);
        _shellOpacity.Changed += value => _shell.Opacity = value;

        // A page narrower than the phone shrinks the whole phone instead of cutting it off.
        Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = _shell };
    }

    /// <summary>The scale of the viewport on the screen (680 / 844 on a docs page; <see cref="SetScale"/> changes it).</summary>
    public double Scale { get; private set; }

    /// <summary>The outline's size at the current <see cref="Scale"/>.</summary>
    public Size Outline => Scale == (OutlineSize.Height - 2 * Bezel) / ViewportSize.Height
        ? OutlineSize
        : new Size(ViewportSize.Width * Scale + 2 * Bezel, ViewportSize.Height * Scale + 2 * Bezel);

    /// <summary>
    /// Draws the viewport at <paramref name="scale"/> (the fullscreen preview fits the phone to the window's height) and
    /// resizes the outline to match, without motion. Popups follow through <see cref="ArcZoom"/>.
    /// </summary>
    public void SetScale(double scale)
    {
        if (scale <= 0 || Math.Abs(scale - Scale) < 0.0005)
        {
            return;
        }
        Scale = scale;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        ArcZoom.SetScale(_scaled, scale);
        _scaled.InvalidateMeasure();
        Settle();
    }

    /// <summary>The demo on the phone's screen.</summary>
    public Control? Screen
    {
        get => _center.Child;
        set => _center.Child = value;
    }

    /// <summary>The viewport the screen holds (the demo's "window").</summary>
    public ArcViewportHost Viewport => _viewport;

    /// <summary>The outline's current size (it differs from <see cref="OutlineSize"/> only while morphing).</summary>
    public Size CurrentSize => new(_shell.Width, _shell.Height);

    /// <summary>Whether the outline is morphing.</summary>
    public bool IsMorphing => _size.IsAnimating;

    /// <summary>
    /// Morphs the outline from <paramref name="from"/> to <paramref name="to"/> on a smooth spring (jumps under reduced or no
    /// motion). Toward the phone (<paramref name="toPhone"/>) the body fades in over 150ms; away from it the whole phone
    /// (body and screen) fades out over 180ms while it keeps growing, as uiarc.dev's preview does. Completes with false when
    /// another morph took over.
    /// </summary>
    public Task<bool> MorphAsync(Size from, Size to, bool toPhone)
    {
        _size.Jump(from);
        var fade = MotionSpec.Tween(TimeSpan.FromMilliseconds(toPhone ? 150 : 180), ArcEasings.Standard);
        if (toPhone)
        {
            if (_shellOpacity.IsAnimating || _shell.Opacity < 0.999)
            {
                // A phone that was fading out (Fit chosen, then Mobile again) comes back from where it was.
                _ = _shellOpacity.AnimateTo(1, fade);
            }
            else
            {
                _bodyOpacity.Jump(0);
                _ = _bodyOpacity.AnimateTo(1, fade);
            }
        }
        else
        {
            _ = _shellOpacity.AnimateTo(0, fade);
        }
        return _size.AnimateTo(to, MotionSpec.Spring(ArcSpring.Smooth));
    }

    /// <summary>Ends any morph at the full phone outline.</summary>
    public void Settle()
    {
        _size.Jump(Outline);
        _bodyOpacity.Jump(1);
        _shellOpacity.Jump(1);
    }
}
