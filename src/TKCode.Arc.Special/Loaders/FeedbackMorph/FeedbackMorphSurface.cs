using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>A face of a <see cref="FeedbackMorph"/> that plays its own entrance (staggered rows, drawn check) when shown.</summary>
internal interface IFeedbackMorphFace
{
    /// <summary>Called as the face starts to come in; <paramref name="animate"/> is false without full motion.</summary>
    void OnEntering(bool animate);
}

/// <summary>
/// The one shape of a <see cref="FeedbackMorph"/>: a rounded rectangle anchored on the button that springs between the
/// button's size, the panel's and the thank-you pill's, with the faces clipped inside it. It is laid out on the button's
/// size; the rectangle and the faces extend past it, over what is around the control.
/// </summary>
/// <remarks>
/// Code-driven motion: the target size is the measured size of whichever face is current, and each face's entrance and
/// exit depend on the face it replaces (Arc Feedback morph, design reference: one critically damped spring of 0.36 s for
/// the shape and every offset, 170 ms fades on Arc's standard curve) — none of which a ControlTheme transition can express.
/// The fill and the shadow are styled properties with theme transitions.
/// </remarks>
internal sealed class FeedbackMorphSurface : Control
{
    /// <summary>The surface fill.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<FeedbackMorphSurface, IBrush?>(nameof(Background));

    /// <summary>The 1 px edge drawn inside the surface.</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<FeedbackMorphSurface, IBrush?>(nameof(BorderBrush));

    /// <summary>The surface shadow.</summary>
    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<FeedbackMorphSurface, BoxShadows>(nameof(BoxShadow));

    /// <summary>Corner radius in every state (a 40 px pill is fully round with it).</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<FeedbackMorphSurface, CornerRadius>(nameof(CornerRadius), new CornerRadius(20));

    /// <summary>Which way the surface grows.</summary>
    public static readonly StyledProperty<FeedbackMorphSide> SideProperty =
        AvaloniaProperty.Register<FeedbackMorphSurface, FeedbackMorphSide>(nameof(Side));

    /// <summary>Which horizontal point of the button the surface stays anchored to.</summary>
    public static readonly StyledProperty<FeedbackMorphAlign> AlignProperty =
        AvaloniaProperty.Register<FeedbackMorphSurface, FeedbackMorphAlign>(nameof(Align), FeedbackMorphAlign.Center);

    // Arc Feedback morph (design reference), sampled on the public preview 2026-11-18: the shape, the panel's scale and
    // every row offset follow one spring of k ≈ 300, c ≈ 35 (ζ ≈ 1); fades run 170 ms on (0.22, 1, 0.36, 1).
    internal static readonly SpringSpec ShapeSpring = SpringSpec.FromVisual(0.36, 0);
    internal static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(170);
    internal static readonly TimeSpan QuickFade = TimeSpan.FromMilliseconds(100);
    internal static readonly TimeSpan ReducedFade = TimeSpan.FromMilliseconds(150);
    private const double Blur = 4;
    private const double FaceScale = 0.92;

    private readonly Stage _stage;
    private readonly Dictionary<Control, FaceMotion> _motions = new();
    private readonly RectangleGeometry[] _clips = [new RectangleGeometry(), new RectangleGeometry()];
    private int _clipIndex;
    private Control? _current;
    private Control? _anchor;
    private Size _anchorSize;
    private MotionValue<Size>? _size;
    private bool _hasSize;
    private Pen? _edge;
    private IBrush? _edgeBrush;

    static FeedbackMorphSurface()
    {
        AffectsRender<FeedbackMorphSurface>(BackgroundProperty, BorderBrushProperty, BoxShadowProperty, CornerRadiusProperty);
        AffectsArrange<FeedbackMorphSurface>(SideProperty, AlignProperty);
        ClipToBoundsProperty.OverrideDefaultValue<FeedbackMorphSurface>(false);
    }

    public FeedbackMorphSurface()
    {
        _stage = new Stage(this);
        VisualChildren.Add(_stage);
        LogicalChildren.Add(_stage);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public FeedbackMorphSide Side { get => GetValue(SideProperty); set => SetValue(SideProperty, value); }

    public FeedbackMorphAlign Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>The face being shown (null before the first).</summary>
    public Control? Current => _current;

    /// <summary>The faces on the stage: the current one and any still leaving.</summary>
    public IReadOnlyList<Control> Faces => _stage.Children;

    /// <summary>The surface's current (animated) size.</summary>
    public Size CurrentSize => _size?.Value ?? default;

    /// <summary>The size the surface is moving to.</summary>
    public Size TargetSize => _size?.Target ?? default;

    /// <summary>Whether the shape is still springing.</summary>
    public bool IsMoving => _size?.IsAnimating == true;

    /// <summary>
    /// The face whose size is the surface's own layout size (the button): the control takes this room in the layout
    /// whichever face shows.
    /// </summary>
    public Control? Anchor
    {
        get => _anchor;
        set
        {
            _anchor = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Makes <paramref name="face"/> the current face: the old one leaves (fades, blurs and shrinks a little), the new one
    /// comes in after <paramref name="enterDelay"/> and the surface springs to its size.
    /// </summary>
    public void Show(Control face, TimeSpan exitDuration, TimeSpan enterDelay, bool scaleIn)
    {
        var old = _current;
        if (ReferenceEquals(old, face))
        {
            return;
        }
        var instant = !IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off;
        _current = face;
        if (old is not null)
        {
            Exit(old, instant, exitDuration);
        }
        Enter(face, instant, enterDelay, scaleIn);
        _stage.InvalidateMeasure();
        InvalidateMeasure();
    }

    /// <summary>Removes every face that is not current at once (detach, tests).</summary>
    public void FinishExits()
    {
        foreach (var face in _stage.Children.ToArray())
        {
            if (!ReferenceEquals(face, _current))
            {
                Remove(face);
            }
        }
        if (_size is { } size && _current is { } current && current.DesiredSize is { Width: > 0, Height: > 0 } target)
        {
            size.Jump(target);
        }
        UpdateClip();
    }

    private void Enter(Control face, bool instant, TimeSpan delay, bool scaleIn)
    {
        var motion = MotionOf(face);
        var fresh = !_stage.Children.Contains(face);
        motion.Exiting = false;
        motion.Token++;
        face.IsHitTestVisible = true;
        if (fresh)
        {
            _stage.Children.Add(face);
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (instant)
        {
            motion.Rest();
        }
        else if (level != ArcMotionLevel.Full)
        {
            // Reduced motion: a short crossfade, no blur or scale (Arc lands transforms at once).
            motion.Scale.Jump(1);
            motion.Blur.Jump(0);
            if (fresh)
            {
                motion.Opacity.Jump(0);
            }
            _ = motion.Opacity.AnimateTo(1, MotionSpec.Tween(ReducedFade, ArcEasings.Standard));
        }
        else
        {
            if (fresh)
            {
                motion.Opacity.Jump(0);
                motion.Blur.Jump(Blur);
                motion.Scale.Jump(scaleIn ? FaceScale : 1);
            }
            _ = motion.Opacity.AnimateTo(1, MotionSpec.Tween(Fade, ArcEasings.Standard, delay));
            _ = motion.Blur.AnimateTo(0, MotionSpec.Tween(Fade, ArcEasings.Standard, delay));
            _ = motion.Scale.AnimateTo(1, MotionSpec.Spring(ShapeSpring, delay));
        }
        (face as IFeedbackMorphFace)?.OnEntering(!instant && level == ArcMotionLevel.Full);
    }

    private void Exit(Control face, bool instant, TimeSpan duration)
    {
        var motion = MotionOf(face);
        motion.Exiting = true;
        var token = ++motion.Token;
        face.IsHitTestVisible = false;
        if (instant)
        {
            Remove(face);
            return;
        }
        Task<bool> faded;
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            faded = motion.Opacity.AnimateTo(0, MotionSpec.Tween(QuickFade, ArcEasings.Standard));
        }
        else
        {
            faded = motion.Opacity.AnimateTo(0, MotionSpec.Tween(duration, ArcEasings.Standard));
            _ = motion.Blur.AnimateTo(Blur, MotionSpec.Tween(duration, ArcEasings.Standard));
            _ = motion.Scale.AnimateTo(FaceScale, MotionSpec.Tween(duration, ArcEasings.Standard));
        }
        _ = RemoveWhenFaded(face, motion, token, faded);
    }

    private async Task RemoveWhenFaded(Control face, FaceMotion motion, int token, Task<bool> faded)
    {
        // Out of sight once it has faded: take it off the stage (it costs layout and render; the panel mounts only while open).
        await faded;
        if (motion.Exiting && motion.Token == token && !ReferenceEquals(face, _current))
        {
            Remove(face);
        }
    }

    private void Remove(Control face)
    {
        if (_motions.TryGetValue(face, out var motion))
        {
            motion.Exiting = false;
            motion.Token++;
            motion.Rest();
        }
        _stage.Children.Remove(face);
        UpdateClip();
    }

    private FaceMotion MotionOf(Control face)
    {
        if (!_motions.TryGetValue(face, out var motion))
        {
            motion = new FaceMotion(this, face);
            _motions.Add(face, motion);
        }
        motion.SetOrigin(Side, Align);
        return motion;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SideProperty || change.Property == AlignProperty)
        {
            foreach (var motion in _motions.Values)
            {
                motion.SetOrigin(Side, Align);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _stage.Measure(Size.Infinity);
        // The control keeps the button's size while another face shows (the button is off the stage then, unstyled):
        // remember it from the last time the button was measured on the stage.
        if (_anchor is { } anchor && _stage.Children.Contains(anchor) && anchor.DesiredSize is { Width: > 0 } size)
        {
            _anchorSize = size;
        }
        return _anchorSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The stage shares the surface's origin and places the faces past its own bounds; with no offset between them the
        // stage's clip means the same in both coordinate spaces, for drawing and hit testing alike.
        _stage.Arrange(new Rect(finalSize));
        OnStageArranged();
        return finalSize;
    }

    private void OnStageArranged()
    {
        if (_current is { } current)
        {
            Follow(current.DesiredSize);
        }
        UpdateClip();
    }

    /// <summary>Springs to the current face's size; jumps the first time and without full motion.</summary>
    private void Follow(Size target)
    {
        if (target.Width <= 0 || target.Height <= 0)
        {
            return;
        }
        _size ??= CreateSize(target);
        if (!_hasSize || !IsLoaded || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            _hasSize = true;
            if (_size.Target != target || _size.IsAnimating)
            {
                _size.Jump(target);
            }
            return;
        }
        if (_size.Target != target)
        {
            UnclipWhenSettled(_size.AnimateTo(target, MotionSpec.Spring(ShapeSpring)));
        }
    }

    private MotionValue<Size> CreateSize(Size initial)
    {
        var value = new MotionValue<Size>(this, initial, MotionVectors.Size);
        value.Changed += _ =>
        {
            UpdateClip();
            InvalidateVisual();
        };
        return value;
    }

    /// <summary>The rectangle a face or the surface of <paramref name="size"/> takes, anchored on the button.</summary>
    internal Rect Place(Size size, Size bounds)
    {
        var x = Align switch
        {
            FeedbackMorphAlign.Start => 0,
            FeedbackMorphAlign.End => bounds.Width - size.Width,
            _ => (bounds.Width - size.Width) / 2
        };
        var y = Side == FeedbackMorphSide.Top ? bounds.Height - size.Height : 0;
        return new Rect(x, y, size.Width, size.Height);
    }

    /// <summary>The surface rectangle in this control's coordinates.</summary>
    internal Rect SurfaceRect() => Place(_size?.Value ?? Bounds.Size, Bounds.Size);

    private async void UnclipWhenSettled(Task<bool> motion)
    {
        await motion;
        UpdateClip();
    }

    /// <summary>
    /// Clips the faces to the surface while it moves or a face is still leaving; at rest the current face fills the surface
    /// and draws nothing in its corners, so the clip goes (nothing to update, and hit testing stays plain).
    /// </summary>
    private void UpdateClip()
    {
        if (_size is null)
        {
            return;
        }
        var moving = _size.IsAnimating || _stage.Children.Count > 1;
        if (!moving)
        {
            if (_stage.Clip is not null)
            {
                _stage.Clip = null;
                _stage.InvalidateVisual();
            }
            return;
        }
        var rect = SurfaceRect();
        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(rect.Width, rect.Height) / 2);
        if (_stage.Clip is RectangleGeometry current && current.Rect == rect && current.RadiusX.Equals(radius))
        {
            return;
        }
        // Alternate two geometries: the stage then sees a new clip each frame and passes it on to the compositor.
        _clipIndex ^= 1;
        var next = _clips[_clipIndex];
        next.Rect = rect;
        next.RadiusX = radius;
        next.RadiusY = radius;
        _stage.Clip = next;
        _stage.InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_size is null)
        {
            return;
        }
        var rect = SurfaceRect();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(rect.Width, rect.Height) / 2);
        context.DrawRectangle(Background, null, new RoundedRect(rect, radius), BoxShadow);
        if (BorderBrush is { } edge)
        {
            if (_edge is null || !ReferenceEquals(_edgeBrush, edge))
            {
                _edgeBrush = edge;
                _edge = new Pen(edge, 1);
            }
            context.DrawRectangle(null, _edge, new RoundedRect(rect.Deflate(0.5), Math.Max(0, radius - 0.5)));
        }
    }

    /// <summary>Holds the faces, each at its own size, anchored on the button like the surface.</summary>
    private sealed class Stage : Panel
    {
        private readonly FeedbackMorphSurface _owner;

        public Stage(FeedbackMorphSurface owner)
        {
            _owner = owner;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children)
            {
                child.Measure(Size.Infinity);
            }
            return default;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var child in Children)
            {
                child.Arrange(_owner.Place(child.DesiredSize, finalSize));
            }
            _owner.OnStageArranged();
            return finalSize;
        }
    }

    /// <summary>The entrance and exit values of one face, written straight to its opacity, effect and scale.</summary>
    private sealed class FaceMotion
    {
        private readonly Control _face;
        private readonly ScaleTransform _scale = new();

        public FaceMotion(Visual owner, Control face)
        {
            _face = face;
            face.RenderTransform = _scale;
            // Owned by the surface: it decides the motion level and stays attached while faces come and go.
            Opacity = new MotionValue<double>(owner, 1, MotionVectors.Double);
            Opacity.Changed += v => _face.Opacity = v;
            Blur = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Blur.Changed += v => _face.Effect = ArcBlur.Get(v);
            Scale = new MotionValue<double>(owner, 1, MotionVectors.Double);
            Scale.Changed += v =>
            {
                _scale.ScaleX = v;
                _scale.ScaleY = v;
            };
        }

        public MotionValue<double> Opacity { get; }

        public MotionValue<double> Blur { get; }

        public MotionValue<double> Scale { get; }

        public bool Exiting { get; set; }

        public int Token { get; set; }

        public void SetOrigin(FeedbackMorphSide side, FeedbackMorphAlign align) =>
            _face.RenderTransformOrigin = new RelativePoint(
                align switch { FeedbackMorphAlign.Start => 0, FeedbackMorphAlign.End => 1, _ => 0.5 },
                side == FeedbackMorphSide.Top ? 1 : 0,
                RelativeUnit.Relative);

        public void Rest()
        {
            Opacity.Jump(1);
            Blur.Jump(0);
            Scale.Jump(1);
        }
    }
}
