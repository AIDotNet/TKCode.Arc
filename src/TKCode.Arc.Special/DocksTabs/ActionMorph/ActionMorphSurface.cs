using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>How a face of an <see cref="ActionMorph"/> comes in and how the one it replaces leaves.</summary>
internal enum ActionMorphFaceEntry
{
    /// <summary>No animation (first show, motion off).</summary>
    Instant,

    /// <summary>Between the button and a panel: the new face grows from 0.9, the old one shrinks to 0.92.</summary>
    Scale,

    /// <summary>One level deeper: the new face comes from the right, the old one is pushed left.</summary>
    Forward,

    /// <summary>One level back: the new face comes from the left, the old one leaves to the right.</summary>
    Back
}

/// <summary>A face that replays its own entrance (title fade, staggered rows) when it is shown.</summary>
internal interface IActionMorphFace
{
    /// <summary>Called when the face starts to come in; <paramref name="animate"/> is false without full motion.</summary>
    void OnEntering(bool animate);
}

/// <summary>
/// The one surface of an <see cref="ActionMorph"/>: a rounded rectangle anchored at the button's corner whose size and
/// corners follow the current face on a spring, with the face (and the one leaving) clipped inside it. It is arranged on
/// the button's square; the rectangle and the faces extend past it, over what is around the morph.
/// </summary>
/// <remarks>
/// Code-driven motion: the target size is the measured size of whichever face is current, the spring depends on whether
/// the move goes deeper (grow) or back (fold), and each face's entrance and exit depend on where it comes from — none of
/// which a ControlTheme transition can express. Colors and the shadow are styled properties with theme transitions.
/// </remarks>
internal sealed class ActionMorphSurface : Control
{
    /// <summary>The surface fill.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ActionMorphSurface, IBrush?>(nameof(Background));

    /// <summary>The 1 px edge drawn inside the surface.</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<ActionMorphSurface, IBrush?>(nameof(BorderBrush));

    /// <summary>The surface shadow.</summary>
    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<ActionMorphSurface, BoxShadows>(nameof(BoxShadow));

    /// <summary>Corner radius while the button is the face.</summary>
    public static readonly StyledProperty<CornerRadius> ButtonRadiusProperty =
        AvaloniaProperty.Register<ActionMorphSurface, CornerRadius>(nameof(ButtonRadius), new CornerRadius(28));

    /// <summary>Corner radius while a panel is the face.</summary>
    public static readonly StyledProperty<CornerRadius> PanelRadiusProperty =
        AvaloniaProperty.Register<ActionMorphSurface, CornerRadius>(nameof(PanelRadius), new CornerRadius(24));

    /// <summary>Whether the button sits in the bottom-left corner (the surface grows to the right).</summary>
    public static readonly StyledProperty<bool> IsStartProperty =
        AvaloniaProperty.Register<ActionMorphSurface, bool>(nameof(IsStart));

    // Arc Action morph (design reference): measured on the public preview, 2026-11-11.
    internal static readonly SpringSpec GrowSpring = SpringSpec.FromVisual(0.46, 0);
    internal static readonly SpringSpec FoldSpring = SpringSpec.FromVisual(0.38, 0);
    internal static readonly SpringSpec FaceScaleSpring = SpringSpec.FromVisual(0.46, 0);
    internal static readonly SpringSpec SlideSpring = SpringSpec.FromVisual(0.40, 0);
    internal static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);
    private const double EnterScale = 0.9;
    private const double ExitScale = 0.92;
    private const double EnterOffset = 28;
    private const double ExitOffset = 20;
    private const double Blur = 4;
    private static readonly TimeSpan EnterDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan EnterFade = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan EnterBlur = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan ExitFade = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan ReducedExit = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ReducedEnter = TimeSpan.FromMilliseconds(160);

    private readonly Stage _stage;
    private readonly Dictionary<Control, FaceMotion> _motions = new();
    private readonly RectangleGeometry[] _clips = [new RectangleGeometry(), new RectangleGeometry()];
    private int _clipIndex;
    private Control? _current;
    private SpringSpec _spring = GrowSpring;
    private MotionValue<Size>? _size;
    private MotionValue<double>? _radius;
    private bool _hasSize;
    private Pen? _edge;
    private IBrush? _edgeBrush;

    static ActionMorphSurface()
    {
        AffectsRender<ActionMorphSurface>(BackgroundProperty, BorderBrushProperty, BoxShadowProperty);
        AffectsArrange<ActionMorphSurface>(IsStartProperty);
        ClipToBoundsProperty.OverrideDefaultValue<ActionMorphSurface>(false);
    }

    public ActionMorphSurface()
    {
        _stage = new Stage(this);
        VisualChildren.Add(_stage);
        LogicalChildren.Add(_stage);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    public CornerRadius ButtonRadius { get => GetValue(ButtonRadiusProperty); set => SetValue(ButtonRadiusProperty, value); }

    public CornerRadius PanelRadius { get => GetValue(PanelRadiusProperty); set => SetValue(PanelRadiusProperty, value); }

    public bool IsStart { get => GetValue(IsStartProperty); set => SetValue(IsStartProperty, value); }

    /// <summary>The panel that holds the faces (tests).</summary>
    internal Panel StagePanel => _stage;

    /// <summary>The face being shown (null before the first).</summary>
    public Control? Current => _current;

    /// <summary>The faces on the stage: the current one and any still leaving.</summary>
    public IReadOnlyList<Control> Faces => _stage.Children;

    /// <summary>The surface's current (animated) size.</summary>
    public Size CurrentSize => _size?.Value ?? default;

    /// <summary>The size the surface is moving to.</summary>
    public Size TargetSize => _size?.Target ?? default;

    /// <summary>The surface's current (animated) corner radius.</summary>
    public double CurrentRadius => _radius?.Value ?? ButtonRadius.TopLeft;

    /// <summary>Whether the button face is the current one (the radius follows the button's).</summary>
    public bool IsButton { get; private set; } = true;

    /// <summary>
    /// Makes <paramref name="face"/> the current face: it comes in as <paramref name="entry"/> says, the old face leaves the
    /// matching way, and the surface springs to the new face's size on <paramref name="spring"/>.
    /// </summary>
    public void Show(Control face, ActionMorphFaceEntry entry, SpringSpec spring, bool isButton)
    {
        var old = _current;
        _spring = spring;
        IsButton = isButton;
        if (ReferenceEquals(old, face))
        {
            InvalidateArrange();
            return;
        }
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            entry = ActionMorphFaceEntry.Instant;
        }
        _current = face;
        if (old is not null)
        {
            Exit(old, entry);
        }
        Enter(face, entry);
        _stage.InvalidateMeasure();
        InvalidateMeasure();
    }

    /// <summary>Removes every face that is not current at once (end of a test, detach).</summary>
    public void FinishExits()
    {
        foreach (var face in _stage.Children.ToArray())
        {
            if (!ReferenceEquals(face, _current))
            {
                Remove(face);
            }
        }
    }

    /// <summary>Takes <paramref name="face"/> off the stage (an action that was removed).</summary>
    public void Forget(Control face)
    {
        if (ReferenceEquals(face, _current))
        {
            return;
        }
        Remove(face);
        _motions.Remove(face);
    }

    private void Enter(Control face, ActionMorphFaceEntry entry)
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
        if (entry == ActionMorphFaceEntry.Instant)
        {
            motion.Rest();
        }
        else if (level != ArcMotionLevel.Full)
        {
            // Reduced motion: a plain crossfade, no blur, scale or slide.
            motion.Scale.Jump(1);
            motion.Offset.Jump(0);
            motion.Blur.Jump(0);
            if (fresh)
            {
                motion.Opacity.Jump(0);
            }
            _ = motion.Opacity.AnimateTo(1, MotionSpec.Tween(ReducedEnter, EaseOut));
        }
        else
        {
            if (fresh)
            {
                motion.Opacity.Jump(0);
                motion.Blur.Jump(Blur);
                motion.Scale.Jump(entry == ActionMorphFaceEntry.Scale ? EnterScale : 1);
                motion.Offset.Jump(entry switch
                {
                    ActionMorphFaceEntry.Forward => EnterOffset,
                    ActionMorphFaceEntry.Back => -EnterOffset,
                    _ => 0
                });
            }
            _ = motion.Opacity.AnimateTo(1, MotionSpec.Tween(EnterFade, ArcEasings.Enter, EnterDelay));
            _ = motion.Blur.AnimateTo(0, MotionSpec.Tween(EnterBlur, ArcEasings.Enter, EnterDelay));
            _ = motion.Scale.AnimateTo(1, MotionSpec.Spring(FaceScaleSpring));
            _ = motion.Offset.AnimateTo(0, MotionSpec.Spring(SlideSpring));
        }
        (face as IActionMorphFace)?.OnEntering(entry != ActionMorphFaceEntry.Instant && level == ArcMotionLevel.Full);
    }

    private void Exit(Control face, ActionMorphFaceEntry entry)
    {
        var motion = MotionOf(face);
        motion.Exiting = true;
        var token = ++motion.Token;
        face.IsHitTestVisible = false;
        if (entry == ActionMorphFaceEntry.Instant)
        {
            Remove(face);
            return;
        }
        Task<bool> faded;
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            faded = motion.Opacity.AnimateTo(0, MotionSpec.Tween(ReducedExit, EaseOut));
        }
        else
        {
            faded = motion.Opacity.AnimateTo(0, MotionSpec.Tween(ExitFade, ArcEasings.Standard));
            _ = motion.Blur.AnimateTo(Blur, MotionSpec.Tween(ExitFade, ArcEasings.Standard));
            if (entry == ActionMorphFaceEntry.Scale)
            {
                _ = motion.Scale.AnimateTo(ExitScale, MotionSpec.Tween(ExitFade, ArcEasings.Standard));
            }
            else
            {
                _ = motion.Offset.AnimateTo(entry == ActionMorphFaceEntry.Forward ? -ExitOffset : ExitOffset,
                    MotionSpec.Spring(SlideSpring));
            }
        }
        _ = RemoveWhenFaded(face, motion, token, faded);
    }

    private async Task RemoveWhenFaded(Control face, FaceMotion motion, int token, Task<bool> faded)
    {
        // The face is gone from sight once it has faded; take it off the stage then (it costs layout and render).
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
        motion.SetOrigin(IsStart);
        return motion;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsStartProperty)
        {
            foreach (var motion in _motions.Values)
            {
                motion.SetOrigin(IsStart);
            }
        }
        else if (change.Property == ButtonRadiusProperty || change.Property == PanelRadiusProperty)
        {
            if (_radius is { IsAnimating: false })
            {
                _radius.Jump(TargetRadius());
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The surface takes the button's square (the morph's own size); the faces extend past it.
        _stage.Measure(Size.Infinity);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The stage shares the surface's origin and places the faces past its own bounds. With no offset between them
        // the stage's clip means the same in the stage's and the surface's coordinates, for drawing and hit testing alike.
        _stage.Arrange(new Rect(finalSize));
        OnStageArranged();
        return finalSize;
    }

    /// <summary>Follows the current face's size (it can change without the surface being laid out again).</summary>
    private void OnStageArranged()
    {
        if (_current is { } current)
        {
            Follow(current.DesiredSize);
        }
        UpdateClip();
    }

    /// <summary>Springs to the current face's size (and the matching corners); jumps the first time.</summary>
    private void Follow(Size target)
    {
        if (target.Width <= 0 || target.Height <= 0)
        {
            return;
        }
        _size ??= CreateSize(target);
        _radius ??= CreateRadius(TargetRadius());
        var radius = TargetRadius();
        if (!_hasSize || !IsLoaded)
        {
            _hasSize = true;
            _size.Jump(target);
            _radius.Jump(radius);
            return;
        }
        if (_size.Target != target)
        {
            UnclipWhenSettled(_size.AnimateTo(target, MotionSpec.Spring(_spring)));
        }
        if (!_radius.Target.Equals(radius))
        {
            UnclipWhenSettled(_radius.AnimateTo(radius, MotionSpec.Spring(_spring)));
        }
    }

    private double TargetRadius() => IsButton ? ButtonRadius.TopLeft : PanelRadius.TopLeft;

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

    private MotionValue<double> CreateRadius(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            UpdateClip();
            InvalidateVisual();
        };
        return value;
    }

    /// <summary>The surface rectangle in this control's coordinates (anchored at the button's bottom corner).</summary>
    internal Rect SurfaceRect()
    {
        var size = _size?.Value ?? new Size(Bounds.Width, Bounds.Height);
        var bounds = Bounds.Size;
        var x = IsStart ? 0 : bounds.Width - size.Width;
        return new Rect(x, bounds.Height - size.Height, size.Width, size.Height);
    }

    private async void UnclipWhenSettled(Task<bool> motion)
    {
        await motion;
        UpdateClip();
    }

    /// <summary>
    /// Clips the faces to the surface while it moves or a face is still leaving. At rest the current face fills the
    /// surface and draws nothing in its corners, so the clip goes (nothing to update, and hit testing stays plain).
    /// </summary>
    private void UpdateClip()
    {
        if (_size is null)
        {
            return;
        }
        var moving = _size.IsAnimating || _radius?.IsAnimating == true || _stage.Children.Count > 1;
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
        var radius = Math.Min(CurrentRadius, Math.Min(rect.Width, rect.Height) / 2);
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
        var radius = Math.Min(CurrentRadius, Math.Min(rect.Width, rect.Height) / 2);
        context.DrawRectangle(Background, null, new RoundedRect(rect, radius), BoxShadow);
        if (BorderBrush is { } edge)
        {
            if (_edge is null || !ReferenceEquals(_edgeBrush, edge))
            {
                _edgeBrush = edge;
                _edge = new Pen(edge, 1);
            }
            var inner = rect.Deflate(0.5);
            context.DrawRectangle(null, _edge, new RoundedRect(inner, Math.Max(0, radius - 0.5)));
        }
    }

    /// <summary>Holds the faces, each at its own size, anchored at the surface's corner.</summary>
    private sealed class Stage : Panel
    {
        private readonly ActionMorphSurface _owner;

        public Stage(ActionMorphSurface owner)
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
                var size = child.DesiredSize;
                var x = _owner.IsStart ? 0 : finalSize.Width - size.Width;
                child.Arrange(new Rect(x, finalSize.Height - size.Height, size.Width, size.Height));
            }
            _owner.OnStageArranged();
            return finalSize;
        }
    }

    /// <summary>The entrance and exit values of one face, written straight to its opacity, effect and transform.</summary>
    private sealed class FaceMotion
    {
        private readonly Control _face;
        private readonly ScaleTransform _scale = new();
        private readonly TranslateTransform _translate = new();

        public FaceMotion(Visual owner, Control face)
        {
            _face = face;
            face.RenderTransform = new TransformGroup { Children = { _scale, _translate } };
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
            Offset = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Offset.Changed += v => _translate.X = v;
        }

        public MotionValue<double> Opacity { get; }

        public MotionValue<double> Blur { get; }

        public MotionValue<double> Scale { get; }

        public MotionValue<double> Offset { get; }

        public bool Exiting { get; set; }

        public int Token { get; set; }

        public void SetOrigin(bool start) =>
            _face.RenderTransformOrigin = new RelativePoint(start ? 0 : 1, 1, RelativeUnit.Relative);

        public void Rest()
        {
            Opacity.Jump(1);
            Blur.Jump(0);
            Scale.Jump(1);
            Offset.Jump(0);
        }
    }
}
