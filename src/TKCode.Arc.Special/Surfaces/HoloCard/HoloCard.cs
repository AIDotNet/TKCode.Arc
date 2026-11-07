using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A holographic card, such as a member card, an event badge or a reward reveal (Arc Holo card, design reference). It
/// leans toward the pointer in perspective while a rainbow foil, a sprinkle of glitter and a glare follow the light
/// across its face, and a floating shadow leans the other way. With a <see cref="Back"/> it is a button: a click or tap
/// spins it half a turn (to the right on the right half, back to the left on the left half), and Enter or Space spins it
/// to the right. The card is always 5:7, at most 300 wide unless a width is set.
/// </summary>
/// <remarks>
/// <para>The faces are pictures: their content is drawn under the card's projection and does not take input. Keep a
/// card's text short and its own design dark or saturated enough for the light layers (on a light card, lower
/// <see cref="Intensity"/>).</para>
/// <para>Without a back the card only tilts: it is not focusable and not a button. Under reduced motion the card does
/// not tilt, scale or spin and shows no glitter; the faces cross-fade instead. With motion off they swap at once.</para>
/// </remarks>
[TemplatePart(ShadowPart, typeof(HoloCardShadow))]
[TemplatePart(FrontFacePart, typeof(Border))]
[TemplatePart(BackFacePart, typeof(Border))]
[TemplatePart(FrontSheenPart, typeof(HoloCardSheen))]
[TemplatePart(BackSheenPart, typeof(HoloCardSheen))]
[TemplatePart(FocusRingPart, typeof(HoloCardFocusRing))]
[PseudoClasses(FlippedPseudoClass, FlippingPseudoClass, TiltOnlyPseudoClass)]
public class HoloCard : TemplatedControl
{
    public const string ShadowPart = "PART_Shadow";
    public const string FrontFacePart = "PART_FrontFace";
    public const string BackFacePart = "PART_BackFace";
    public const string FrontSheenPart = "PART_FrontSheen";
    public const string BackSheenPart = "PART_BackSheen";
    public const string FocusRingPart = "PART_FocusRing";
    public const string FlippedPseudoClass = ":flipped";
    public const string FlippingPseudoClass = ":flipping";
    public const string TiltOnlyPseudoClass = ":tilt-only";

    /// <summary>The front of the card: any content, stretched over the whole card and clipped to its corners.</summary>
    public static readonly StyledProperty<object?> FrontProperty =
        AvaloniaProperty.Register<HoloCard, object?>(nameof(Front));

    /// <summary>The template for <see cref="Front"/> when it is data rather than a control.</summary>
    public static readonly StyledProperty<IDataTemplate?> FrontTemplateProperty =
        AvaloniaProperty.Register<HoloCard, IDataTemplate?>(nameof(FrontTemplate));

    /// <summary>
    /// The back of the card. Without one the card only tilts: it does not flip, is not focusable and is not a button.
    /// </summary>
    public static readonly StyledProperty<object?> BackProperty =
        AvaloniaProperty.Register<HoloCard, object?>(nameof(Back));

    /// <summary>The template for <see cref="Back"/> when it is data rather than a control.</summary>
    public static readonly StyledProperty<IDataTemplate?> BackTemplateProperty =
        AvaloniaProperty.Register<HoloCard, IDataTemplate?>(nameof(BackTemplate));

    /// <summary>
    /// Whether the back faces the viewer. Two-way: a click, a tap or Enter / Space turns it, and setting it turns the
    /// card (to the right). Setting it does not raise <see cref="FlippedChanged"/>.
    /// </summary>
    public static readonly StyledProperty<bool> IsFlippedProperty =
        AvaloniaProperty.Register<HoloCard, bool>(nameof(IsFlipped), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The largest tilt in degrees, reached with the pointer at the card's edge; 0 turns tilting off.</summary>
    public static readonly StyledProperty<double> MaxTiltProperty =
        AvaloniaProperty.Register<HoloCard, double>(nameof(MaxTilt), 14,
            coerce: (_, value) => double.IsFinite(value) ? Math.Clamp(value, 0, 60) : 14);

    /// <summary>Strength of the foil, glitter and glare, 0..1; 0 leaves the content plain.</summary>
    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<HoloCard, double>(nameof(Intensity), 1,
            coerce: (_, value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1);

    /// <summary>
    /// The card's accessible name, such as "Mira Okafor, Lumen Society patron pass". When empty, the text of the
    /// visible face is used.
    /// </summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<HoloCard, string?>(nameof(Label));

    /// <summary>What assistive technology calls the card when it can flip ("flip card").</summary>
    public static readonly StyledProperty<string> RoleDescriptionProperty =
        AvaloniaProperty.Register<HoloCard, string>(nameof(RoleDescription), "flip card");

    /// <summary>Raised when a click, a tap, the keyboard or <see cref="Flip"/> turns the card over.</summary>
    public static readonly RoutedEvent<HoloCardFlippedEventArgs> FlippedChangedEvent =
        RoutedEvent.Register<HoloCard, HoloCardFlippedEventArgs>(nameof(FlippedChanged), RoutingStrategies.Bubble);

    // Fitted to the measured step responses (notes section 6): tilt ω 15.25, ζ 1.04; light and hover ω 11.1, ζ 0.92;
    // flip ω 8.75, ζ 0.90.
    private static readonly MotionSpec s_tiltSpring = MotionSpec.Spring(SpringSpec.FromPhysical(230, 31));
    private static readonly MotionSpec s_lightSpring = MotionSpec.Spring(SpringSpec.FromPhysical(122, 20));
    private static readonly MotionSpec s_flipSpring = MotionSpec.Spring(SpringSpec.FromPhysical(77, 15.8));
    private static readonly MotionSpec s_fade = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard);
    private static readonly Vector s_restLight = new(0.5, 0.5);

    /// <summary>A tap that travels less than this (px) flips the card; a longer touch drag only tilts it.</summary>
    internal const double TapSlop = 10;

    // Pointer-following springs, the flip and the reduced-motion cross-fade. Code-driven because the tilt, the light
    // and the flip feed a perspective projection and baked light layers every frame, and a re-aimed spring (the pointer
    // moving on, a second click mid-spin) must keep its velocity, which ControlTheme transitions cannot do.
    private readonly MotionValue<Vector> _tilt;
    private readonly MotionValue<Vector> _light;
    private readonly MotionValue<double> _hover;
    private readonly MotionValue<double> _flip;
    private readonly MotionValue<double> _fade;
    private readonly MatrixTransform _frontTransform = new();
    private readonly MatrixTransform _backTransform = new();
    private readonly MatrixTransform _tiltTransform = new();
    private readonly MatrixTransform _ringTransform = new();

    private HoloCardShadow? _shadow;
    private Border? _frontFace;
    private Border? _backFace;
    private HoloCardSheen? _frontSheen;
    private HoloCardSheen? _backSheen;
    private HoloCardFocusRing? _focusRing;
    private Size _cardSize;
    private double _flipTarget;
    private bool _syncingFlip;
    private bool _pressed;
    private IPointer? _touch;
    private Point _touchStart;
    private bool _touchMoved;

    static HoloCard()
    {
        FocusableProperty.OverrideDefaultValue<HoloCard>(false);
        AffectsMeasure<HoloCard>(BackProperty);
    }

    public HoloCard()
    {
        _tilt = new MotionValue<Vector>(this, default, MotionVectors.Vector);
        _light = new MotionValue<Vector>(this, s_restLight, MotionVectors.Vector);
        _hover = new MotionValue<double>(this, 0, MotionVectors.Double);
        _flip = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fade = new MotionValue<double>(this, 0, MotionVectors.Double);
        _tilt.Changed += _ => ApplyMotion();
        _light.Changed += _ => ApplyMotion();
        _hover.Changed += _ => ApplyMotion();
        _flip.Changed += _ => OnFlipMoved();
        _fade.Changed += _ => ApplyMotion();
        PseudoClasses.Set(TiltOnlyPseudoClass, true);
    }

    /// <inheritdoc cref="FrontProperty"/>
    public object? Front
    {
        get => GetValue(FrontProperty);
        set => SetValue(FrontProperty, value);
    }

    /// <inheritdoc cref="FrontTemplateProperty"/>
    public IDataTemplate? FrontTemplate
    {
        get => GetValue(FrontTemplateProperty);
        set => SetValue(FrontTemplateProperty, value);
    }

    /// <inheritdoc cref="BackProperty"/>
    public object? Back
    {
        get => GetValue(BackProperty);
        set => SetValue(BackProperty, value);
    }

    /// <inheritdoc cref="BackTemplateProperty"/>
    public IDataTemplate? BackTemplate
    {
        get => GetValue(BackTemplateProperty);
        set => SetValue(BackTemplateProperty, value);
    }

    /// <inheritdoc cref="IsFlippedProperty"/>
    public bool IsFlipped
    {
        get => GetValue(IsFlippedProperty);
        set => SetValue(IsFlippedProperty, value);
    }

    /// <inheritdoc cref="MaxTiltProperty"/>
    public double MaxTilt
    {
        get => GetValue(MaxTiltProperty);
        set => SetValue(MaxTiltProperty, value);
    }

    /// <inheritdoc cref="IntensityProperty"/>
    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="RoleDescriptionProperty"/>
    public string RoleDescription
    {
        get => GetValue(RoleDescriptionProperty);
        set => SetValue(RoleDescriptionProperty, value);
    }

    /// <inheritdoc cref="FlippedChangedEvent"/>
    public event EventHandler<HoloCardFlippedEventArgs>? FlippedChanged
    {
        add => AddHandler(FlippedChangedEvent, value);
        remove => RemoveHandler(FlippedChangedEvent, value);
    }

    /// <summary>Whether the card has a back and so flips (otherwise it only tilts).</summary>
    public bool CanFlip => Back is not null;

    /// <summary>The flip angle in degrees, accumulated over every turn (0, 180, 360, 180 ...).</summary>
    internal double FlipAngle => _flip.Value;

    /// <summary>The angle the flip is heading to.</summary>
    internal double FlipTarget => _flipTarget;

    /// <summary>The current tilt in degrees (X = rotateX, Y = rotateY).</summary>
    internal Vector TiltValue => _tilt.Value;

    /// <summary>The current light position, 0..1.</summary>
    internal Vector LightValue => _light.Value;

    /// <summary>The current hover progress, 0..1.</summary>
    internal double HoverValue => _hover.Value;

    /// <summary>The reduced-motion cross-fade, 0 = front, 1 = back.</summary>
    internal double FadeValue => _fade.Value;

    /// <summary>The card's size (5:7) inside the control.</summary>
    internal Size CardSize => _cardSize;

    internal Border? FrontFace => _frontFace;

    internal Border? BackFace => _backFace;

    internal HoloCardSheen? FrontSheen => _frontSheen;

    internal HoloCardShadow? Shadow => _shadow;

    internal Matrix FrontMatrix => _frontTransform.Matrix;

    internal Matrix BackMatrix => _backTransform.Matrix;

    /// <summary>
    /// Turns the card over: half a turn to the right, or back to the left for <see cref="HoloCardFlipSide.Left"/>.
    /// Raises <see cref="FlippedChanged"/>. Does nothing without a back.
    /// </summary>
    public void Flip(HoloCardFlipSide side = HoloCardFlipSide.Programmatic)
    {
        if (!CanFlip)
        {
            return;
        }
        _flipTarget = HoloCardMath.NextFlipTarget(_flipTarget, side);
        var flipped = HoloCardMath.IsFlippedAt(_flipTarget);
        _syncingFlip = true;
        try
        {
            SetCurrentValue(IsFlippedProperty, flipped);
        }
        finally
        {
            _syncingFlip = false;
        }
        StartFlip();
        RaiseEvent(new HoloCardFlippedEventArgs(FlippedChangedEvent, flipped, side));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _shadow = e.NameScope.Find<HoloCardShadow>(ShadowPart);
        _frontFace = e.NameScope.Find<Border>(FrontFacePart);
        _backFace = e.NameScope.Find<Border>(BackFacePart);
        _frontSheen = e.NameScope.Find<HoloCardSheen>(FrontSheenPart);
        _backSheen = e.NameScope.Find<HoloCardSheen>(BackSheenPart);
        _focusRing = e.NameScope.Find<HoloCardFocusRing>(FocusRingPart);
        Prepare(_frontFace, _frontTransform);
        Prepare(_backFace, _backTransform);
        Prepare(_shadow, _tiltTransform);
        Prepare(_focusRing, _ringTransform);
        ApplyCorners();
        ApplyMotion();
    }

    private static void Prepare(Visual? visual, MatrixTransform transform)
    {
        if (visual is null)
        {
            return;
        }
        visual.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        visual.RenderTransform = transform;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var card = HoloCardMath.CardSize(availableSize.Width, availableSize.Height, double.PositiveInfinity);
        Root?.Measure(card);
        return card;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var card = HoloCardMath.CardSize(finalSize.Width, finalSize.Height, double.PositiveInfinity);
        var changed = card != _cardSize;
        _cardSize = card;
        Root?.Arrange(new Rect((finalSize.Width - card.Width) / 2, (finalSize.Height - card.Height) / 2, card.Width, card.Height));
        if (changed)
        {
            ApplyCorners();
            ApplyMotion();
        }
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == IsFlippedProperty)
        {
            PseudoClasses.Set(FlippedPseudoClass, IsFlipped);
            if (!_syncingFlip && CanFlip && HoloCardMath.IsFlippedAt(_flipTarget) != IsFlipped)
            {
                _flipTarget = HoloCardMath.NextFlipTarget(_flipTarget, HoloCardFlipSide.Programmatic);
                StartFlip();
            }
        }
        else if (property == BackProperty)
        {
            var canFlip = CanFlip;
            PseudoClasses.Set(TiltOnlyPseudoClass, !canFlip);
            SetCurrentValue(FocusableProperty, canFlip);
            if (!canFlip)
            {
                _flipTarget = 0;
                _flip.Jump(0);
                _fade.Jump(0);
            }
            else if (HoloCardMath.IsFlippedAt(_flipTarget) != IsFlipped)
            {
                _flipTarget = HoloCardMath.NextFlipTarget(_flipTarget, HoloCardFlipSide.Programmatic);
                StartFlip();
            }
            ApplyMotion();
        }
        else if (property == IntensityProperty || property == ArcMotion.LevelProperty)
        {
            if (property == ArcMotion.LevelProperty && !ArcMotion.IsFull(this))
            {
                Rest(jump: true);
            }
            ApplyMotion();
        }
        else if (property == CornerRadiusProperty)
        {
            ApplyCorners();
        }
        else if (property == MaxTiltProperty && _hover.Target > 0)
        {
            Aim(_light.Target);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _pressed = false;
        _touch = null;
        Rest(jump: true);
        _flip.Jump(_flipTarget);
        _fade.Jump(HoloCardMath.IsFlippedAt(_flipTarget) ? 1 : 0);
    }

    // ---- Input ----

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_touch is not null && e.Pointer == _touch)
        {
            var at = e.GetPosition(this);
            if (Distance(at, _touchStart) > TapSlop)
            {
                _touchMoved = true;
            }
            Follow(at, hover: false);
            return;
        }
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var position = e.GetPosition(this);
        if (CardRect.Contains(position))
        {
            Follow(position, hover: true);
        }
        else
        {
            Rest(jump: false);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_touch is null)
        {
            Rest(jump: false);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || !CardRect.Contains(point.Position))
        {
            return;
        }
        if (e.Pointer.Type == PointerType.Touch)
        {
            // A finger tilts the card while it is down (no hover lift for touch) and flips it on a tap.
            _touch = e.Pointer;
            _touchStart = point.Position;
            _touchMoved = false;
            e.Pointer.Capture(this);
            Follow(point.Position, hover: false);
        }
        else
        {
            _pressed = true;
        }
        if (CanFlip)
        {
            Focus(NavigationMethod.Pointer);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var position = e.GetPosition(this);
        if (_touch is not null && e.Pointer == _touch)
        {
            var tapped = !_touchMoved;
            _touch = null;
            e.Pointer.Capture(null);
            Rest(jump: false);
            if (tapped && CardRect.Contains(position))
            {
                FlipFrom(position);
                e.Handled = true;
            }
            return;
        }
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        if (e.InitialPressMouseButton == MouseButton.Left && CardRect.Contains(position))
        {
            FlipFrom(position);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_touch is not null && e.Pointer == _touch)
        {
            _touch = null;
            Rest(jump: false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !CanFlip || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            Flip(HoloCardFlipSide.Keyboard);
            e.Handled = true;
        }
    }

    private Layoutable? Root => VisualChildren.Count > 0 ? VisualChildren[0] as Layoutable : null;

    private Rect CardRect => new((Bounds.Width - _cardSize.Width) / 2, (Bounds.Height - _cardSize.Height) / 2,
        _cardSize.Width, _cardSize.Height);

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private void FlipFrom(Point position)
    {
        if (!CanFlip)
        {
            return;
        }
        var rect = CardRect;
        Flip(position.X < rect.X + rect.Width / 2 ? HoloCardFlipSide.Left : HoloCardFlipSide.Right);
    }

    /// <summary>Aims the tilt, the light and (for a mouse or pen) the hover lift at a point in the control.</summary>
    private void Follow(Point position, bool hover)
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        var rect = CardRect;
        var pointer = HoloCardMath.Normalize(new Point(position.X - rect.X, position.Y - rect.Y), rect.Size);
        Aim(pointer);
        if (hover)
        {
            _ = _hover.AnimateTo(1, s_lightSpring);
        }
    }

    private void Aim(Vector pointer)
    {
        _ = _tilt.AnimateTo(HoloCardMath.Tilt(pointer, MaxTilt), s_tiltSpring);
        _ = _light.AnimateTo(pointer, s_lightSpring);
    }

    /// <summary>The pointer left: tilt, light and hover settle back to rest together.</summary>
    private void Rest(bool jump)
    {
        if (jump)
        {
            _tilt.Jump(default);
            _light.Jump(s_restLight);
            _hover.Jump(0);
            return;
        }
        _ = _tilt.AnimateTo(default, s_tiltSpring);
        _ = _light.AnimateTo(s_restLight, s_lightSpring);
        _ = _hover.AnimateTo(0, s_lightSpring);
    }

    private void StartFlip()
    {
        var flipped = HoloCardMath.IsFlippedAt(_flipTarget);
        if (ArcMotion.IsFull(this))
        {
            _fade.Jump(flipped ? 1 : 0);
            PseudoClasses.Set(FlippingPseudoClass, true);
            _ = _flip.AnimateTo(_flipTarget, s_flipSpring);
        }
        else
        {
            // Reduced motion: no spin; the faces cross-fade (240 ms). Off: the fade jumps too.
            _flip.Jump(_flipTarget);
            _ = _fade.AnimateTo(flipped ? 1 : 0, s_fade);
        }
        ApplyMotion();
    }

    private void OnFlipMoved()
    {
        // A settled spring lands exactly on its target (Changed fires before the value reports it stopped).
        if (_flip.Value == _flipTarget)
        {
            PseudoClasses.Set(FlippingPseudoClass, false);
        }
        ApplyMotion();
    }

    private void ApplyCorners()
    {
        var radius = CornerRadius.TopLeft;
        var size = _cardSize;
        foreach (var face in new[] { _frontFace, _backFace })
        {
            if (face is null)
            {
                continue;
            }
            face.Clip = new RectangleGeometry(new Rect(size), radius, radius);
            face.InvalidateVisual();
        }
        if (_frontSheen is not null)
        {
            _frontSheen.CornerRadius = radius;
        }
        if (_backSheen is not null)
        {
            _backSheen.CornerRadius = radius;
        }
        if (_shadow is not null)
        {
            _shadow.CornerRadius = radius;
        }
        if (_focusRing is not null)
        {
            _focusRing.CornerRadius = radius;
        }
    }

    /// <summary>
    /// Writes the springs' current values to the projection, the faces, the shadow and the light layers. Called on every
    /// spring step; only matrices and a few fields change, the layers rebake on their next render.
    /// </summary>
    private void ApplyMotion()
    {
        if (_frontFace is null || _cardSize.Width <= 0)
        {
            return;
        }
        double w = _cardSize.Width, h = _cardSize.Height;
        var full = ArcMotion.IsFull(this);
        var canFlip = CanFlip;
        var tilt = full ? _tilt.Value : default;
        var light = full ? _light.Value : s_restLight;
        var hover = full ? _hover.Value : 0;
        var flip = full && canFlip ? _flip.Value : 0;
        var scale = HoloCardMath.Scale(hover);
        var shine = HoloCardMath.Shine(hover, flip);
        var intensity = Intensity;

        var tiltMatrix = HoloCardProjection.PlaneMatrix(w, h, HoloCardMath.Perspective, scale, tilt.X, tilt.Y);
        SetMatrix(_tiltTransform, tiltMatrix);
        SetMatrix(_ringTransform, tiltMatrix);
        _shadow?.Update(HoloCardMath.ShadowOffset(tilt), HoloCardMath.ShadowAlpha(hover));

        if (full)
        {
            var front = HoloCardProjection.PlaneMatrix(w, h, HoloCardMath.Perspective, scale, tilt.X, tilt.Y + flip);
            var back = HoloCardProjection.PlaneMatrix(w, h, HoloCardMath.Perspective, scale, tilt.X, tilt.Y + flip + 180);
            SetMatrix(_frontTransform, front);
            SetMatrix(_backTransform, back);
            var frontVisible = !canFlip || HoloCardProjection.FacesViewer(front, w, h);
            SetFace(_frontFace, frontVisible, 1);
            SetFace(_backFace, canFlip && !frontVisible, 1);
        }
        else
        {
            SetMatrix(_frontTransform, Matrix.Identity);
            SetMatrix(_backTransform, Matrix.Identity);
            var fade = canFlip ? Math.Clamp(_fade.Value, 0, 1) : 0;
            SetFace(_frontFace, fade < 1, 1 - fade);
            SetFace(_backFace, canFlip && fade > 0, fade);
        }
        _frontSheen?.Update(light, shine, flip, intensity, glitter: full);
        _backSheen?.Update(light, shine, flip, intensity, glitter: false);
    }

    private static void SetMatrix(MatrixTransform transform, in Matrix matrix)
    {
        if (transform.Matrix != matrix)
        {
            transform.Matrix = matrix;
        }
    }

    private static void SetFace(Border? face, bool visible, double opacity)
    {
        if (face is null)
        {
            return;
        }
        if (face.IsVisible != visible)
        {
            face.IsVisible = visible;
        }
        if (face.Opacity != opacity)
        {
            face.Opacity = opacity;
        }
    }

    /// <summary>The text of the visible face, for the accessible name when <see cref="Label"/> is empty.</summary>
    internal string FaceText()
    {
        var content = CanFlip && IsFlipped ? Back : Front;
        if (content is string text)
        {
            return text;
        }
        // A control is searched directly (it may not be laid out yet behind a hidden face); data is found through the
        // face that shows its template.
        Visual? root = content as Visual ?? (CanFlip && IsFlipped ? _backFace : _frontFace);
        if (root is null)
        {
            return string.Empty;
        }
        var parts = new List<string>();
        if (root is TextBlock { Text: { } own } && !string.IsNullOrWhiteSpace(own))
        {
            parts.Add(own.Trim());
        }
        foreach (var block in root.GetVisualDescendants().OfType<TextBlock>())
        {
            if (block.IsVisible && !string.IsNullOrWhiteSpace(block.Text))
            {
                parts.Add(block.Text.Trim());
            }
        }
        return string.Join(", ", parts);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HoloCardAutomationPeer(this);

    private sealed class HoloCardAutomationPeer(HoloCard owner) : ControlAutomationPeer(owner), IInvokeProvider, IToggleProvider
    {
        private HoloCard Card => (HoloCard)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore()
            => Card.CanFlip ? AutomationControlType.Button : AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore()
            => Card.CanFlip ? Card.RoleDescription : base.GetLocalizedControlTypeCore();

        protected override string? GetNameCore()
        {
            var name = AutomationProperties.GetName(Owner);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            return string.IsNullOrEmpty(Card.Label) ? Card.FaceText() : Card.Label;
        }

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;

        // The faces are pictures; the card is one element (its name carries the text).
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        public void Invoke()
        {
            EnsureEnabled();
            Card.Flip();
        }

        public ToggleState ToggleState => Card.IsFlipped ? ToggleState.On : ToggleState.Off;

        public void Toggle()
        {
            EnsureEnabled();
            Card.Flip();
        }
    }
}
