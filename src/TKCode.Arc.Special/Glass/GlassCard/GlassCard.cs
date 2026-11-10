using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A card of frosted glass: the content sits on a blurred, tinted copy of what lies behind the card (see
/// <see cref="GlassBackdrop"/>), framed by a 1px edge light that is bright along the top left and fades to a faint shade at
/// the bottom right. A soft sheen follows the pointer across the card and brightens the edge near it; with
/// <see cref="MaxTilt"/> the card also leans toward the pointer in perspective. <see cref="Tilt"/> drives the same light
/// (and lean) from another source, such as a device's orientation, while the pointer is elsewhere.
/// </summary>
/// <remarks>
/// Under the ArcDesign <c>ReduceTransparency</c> token the card is an opaque raised surface with a plain border. Under
/// reduced motion the card does not lean and shows no sheen.
/// </remarks>
[TemplatePart(PanePart, typeof(GlassPane))]
public class GlassCard : ContentControl
{
    public const string PanePart = "PART_Pane";

    /// <summary>
    /// The largest lean toward the pointer in degrees (0..20); 0 (the default) keeps the card flat and only moves the
    /// light.
    /// </summary>
    public static readonly StyledProperty<double> MaxTiltProperty =
        AvaloniaProperty.Register<GlassCard, double>(nameof(MaxTilt), 0,
            coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0, 20) : 0);

    /// <summary>Strength of the pointer sheen and the edge glow, 0..1; 0 leaves the glass still.</summary>
    public static readonly StyledProperty<double> SheenIntensityProperty =
        AvaloniaProperty.Register<GlassCard, double>(nameof(SheenIntensity), 1,
            coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0, 1) : 1);

    /// <summary>
    /// Where the light comes from while the pointer is not over the card, -1..1 on each axis ((0, 0) is at rest: no sheen
    /// and no lean). Feed it a device's orientation or any other signal; the pointer takes over while it is on the card.
    /// </summary>
    public static readonly StyledProperty<Vector> TiltProperty =
        AvaloniaProperty.Register<GlassCard, Vector>(nameof(Tilt),
            coerce: static (_, v) => new Vector(Clamp1(v.X), Clamp1(v.Y)));

    internal static readonly StyledProperty<double> TiltDepthProperty =
        AvaloniaProperty.Register<GlassCard, double>(nameof(TiltDepth), 1200);

    /// <summary>The lean follows on a smooth spring without overshoot.</summary>
    internal static readonly SpringSpec TiltSpring = SpringSpec.FromVisual(0.45, 0);

    private readonly Rotate3DTransform _rotate = new();
    private readonly MotionValue<Vector> _lean;
    private GlassPane? _pane;
    private bool _pointerOver;
    private IPointer? _touch;

    static GlassCard()
    {
        FocusableProperty.OverrideDefaultValue<GlassCard>(false);
    }

    public GlassCard()
    {
        _lean = new MotionValue<Vector>(this, default, MotionVectors.Vector);
        _lean.Changed += ApplyLean;
    }

    /// <inheritdoc cref="MaxTiltProperty"/>
    public double MaxTilt
    {
        get => GetValue(MaxTiltProperty);
        set => SetValue(MaxTiltProperty, value);
    }

    /// <inheritdoc cref="SheenIntensityProperty"/>
    public double SheenIntensity
    {
        get => GetValue(SheenIntensityProperty);
        set => SetValue(SheenIntensityProperty, value);
    }

    /// <inheritdoc cref="TiltProperty"/>
    public Vector Tilt
    {
        get => GetValue(TiltProperty);
        set => SetValue(TiltProperty, value);
    }

    internal double TiltDepth
    {
        get => GetValue(TiltDepthProperty);
        set => SetValue(TiltDepthProperty, value);
    }

    /// <summary>True while the blurred backdrop shows through the card.</summary>
    public bool IsFrosted => _pane?.IsFrosted == true;

    /// <summary>The lean now, in degrees (X: about the vertical axis, Y: about the horizontal axis).</summary>
    internal Vector CurrentLean => _lean.Value;

    internal GlassPane? Pane => _pane;

    private static double Clamp1(double value) => double.IsFinite(value) ? Math.Clamp(value, -1, 1) : 0;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _pane = e.NameScope.Find<GlassPane>(PanePart);
        if (_pane is not null)
        {
            _pane.SheenIntensity = SheenIntensity;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SheenIntensityProperty && _pane is not null)
        {
            _pane.SheenIntensity = SheenIntensity;
        }
        else if (change.Property == TiltProperty || change.Property == MaxTiltProperty)
        {
            if (!_pointerOver)
            {
                FollowTilt();
            }
        }
        else if (change.Property == TiltDepthProperty)
        {
            _rotate.Depth = TiltDepth;
        }
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FollowTilt();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _pointerOver = true;
            Follow(e.GetPosition(this));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if ((e.Pointer.Type != PointerType.Touch && _pointerOver) || ReferenceEquals(e.Pointer, _touch))
        {
            Follow(e.GetPosition(this));
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _pointerOver = false;
            FollowTilt();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // A finger lights the card while it is down; it does not take the press from the content.
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touch = e.Pointer;
            _pointerOver = true;
            Follow(e.GetPosition(this));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndTouch(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndTouch(e.Pointer);
    }

    private void EndTouch(IPointer pointer)
    {
        if (ReferenceEquals(pointer, _touch))
        {
            _touch = null;
            _pointerOver = false;
            FollowTilt();
        }
    }

    /// <summary>Light and lean toward a point on the card (card coordinates).</summary>
    private void Follow(Point position)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        _pane?.PointAt(this.TranslatePoint(position, _pane) ?? position);
        var normalized = new Vector(
            Math.Clamp(position.X / size.Width * 2 - 1, -1, 1),
            Math.Clamp(position.Y / size.Height * 2 - 1, -1, 1));
        LeanTo(normalized);
    }

    /// <summary>Back to the <see cref="Tilt"/> signal: rest when it is (0, 0).</summary>
    private void FollowTilt()
    {
        var tilt = Tilt;
        if (tilt == default)
        {
            _pane?.PointAt(null);
            LeanTo(default);
            return;
        }
        var size = Bounds.Size;
        if (_pane is not null && size.Width > 0 && size.Height > 0)
        {
            _pane.PointAt(new Point((tilt.X + 1) / 2 * size.Width, (tilt.Y + 1) / 2 * size.Height));
        }
        LeanTo(tilt);
    }

    /// <summary>Leans toward a normalized point (-1..1): the edge nearest the light tips away from the viewer.</summary>
    private void LeanTo(Vector normalized)
    {
        var max = MaxTilt;
        var full = ArcMotion.IsFull(this);
        // Toward the pointer: the edge under it tips away from the viewer (rotateY with x, rotateX against y).
        var target = max > 0 && full ? new Vector(normalized.X * max, -normalized.Y * max) : default;
        if (!full || !IsLoaded)
        {
            _lean.Jump(target);
            return;
        }
        _ = _lean.AnimateTo(target, MotionSpec.Spring(TiltSpring));
    }

    private void ApplyLean(Vector lean)
    {
        if (Math.Abs(lean.X) < 0.001 && Math.Abs(lean.Y) < 0.001)
        {
            if (RenderTransform is not null)
            {
                RenderTransform = null;
            }
            return;
        }
        _rotate.AngleY = lean.X;
        _rotate.AngleX = lean.Y;
        _rotate.Depth = TiltDepth;
        if (!ReferenceEquals(RenderTransform, _rotate))
        {
            RenderTransform = _rotate;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GlassCardAutomationPeer(this);

    private sealed class GlassCardAutomationPeer : ControlAutomationPeer
    {
        public GlassCardAutomationPeer(GlassCard owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
