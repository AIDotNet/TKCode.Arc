using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The three faces of a <see cref="ShareSheet"/>'s shape.</summary>
internal enum ShareSheetFace
{
    Button,
    Panel,
    Sent
}

/// <summary>How one face replaces another.</summary>
internal enum ShareSheetFaceSwap
{
    /// <summary>Between the button and a card: the old face shrinks to 0.96 and blurs out, the new one grows from 0.94.</summary>
    Scale,

    /// <summary>Between the panel and the confirmation: the faces slide 32 px sideways while they blur across.</summary>
    Slide
}

/// <summary>
/// The faces of the shape and its size. Every face keeps its own natural size and is anchored inside the stage by the
/// sheet's alignment; the stage's own size springs to the current face's size (and follows that face when it grows or
/// shrinks, such as the access list opening), so the shape that contains it reveals or covers the faces as it moves.
/// </summary>
/// <remarks>
/// Code-driven motion: the stage lives in an overlay layer outside any ControlTheme, its size must spring from wherever
/// it is when the target changes mid-flight (velocity kept), and an outgoing face is removed only once its exit ends.
/// </remarks>
internal sealed class ShareSheetStage : Panel
{
    private readonly Dictionary<Control, FaceMotion> _faces = new();
    private MotionValue<Size>? _size;
    private MotionValue<double>? _radius;
    private Size _target;
    private double _radiusTarget;
    private SpringSpec _spring = ShareSheetMotion.Grow;
    private bool _jumpNext = true;
    private Task<bool>? _sizeRun;
    private Task<bool>? _radiusRun;
    private readonly List<Task> _exits = new();

    public ShareSheetStage()
    {
        ClipToBounds = false;
    }

    /// <summary>Where faces sit inside the stage while it is not their size.</summary>
    public ShareSheetAlign Align { get; set; } = ShareSheetAlign.End;

    /// <summary>In the bottom sheet the stage takes the width it is given and only its height springs.</summary>
    public bool IsSheet { get; set; }

    /// <summary>The face the stage is sized to.</summary>
    public Control? Current { get; private set; }

    /// <summary>Which face <see cref="Current"/> is.</summary>
    public ShareSheetFace CurrentKind { get; private set; }

    /// <summary>The current (animated) size.</summary>
    public Size ShapeSize => _size?.Value ?? _target;

    /// <summary>The current (animated) corner radius.</summary>
    public double ShapeRadius => _radius?.Value ?? _radiusTarget;

    /// <summary>Whether the size or the radius is still moving.</summary>
    public bool IsMoving => (_size?.IsAnimating ?? false) || (_radius?.IsAnimating ?? false);

    /// <summary>Faces on their way out.</summary>
    public int LeavingCount
    {
        get
        {
            var count = 0;
            foreach (var motion in _faces.Values)
            {
                if (motion.Leaving)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>Raised when the size or the radius changed (every frame while they move).</summary>
    public event Action? ShapeChanged;

    /// <summary>Raised when a face's exit finished and it left the stage.</summary>
    public event Action<Control>? FaceRemoved;

    /// <summary>
    /// Shows <paramref name="face"/>. The first face (or <paramref name="animate"/> false) appears at once and the size
    /// jumps; otherwise the old face leaves, the new one enters and the size springs (<see cref="ShareSheetMotion.Grow"/>
    /// toward the panel, <see cref="ShareSheetMotion.Fold"/> otherwise). <paramref name="direction"/> is +1 forward (the
    /// new face comes from the end) or -1 back.
    /// </summary>
    public void Show(Control face, ShareSheetFace kind, double radius, ShareSheetFaceSwap swap, int direction, bool animate)
    {
        var old = Current;
        if (ReferenceEquals(old, face) && kind == CurrentKind)
        {
            _radiusTarget = radius;
            RetargetRadius(animate);
            return;
        }
        Current = face;
        CurrentKind = kind;
        _spring = kind == ShareSheetFace.Panel ? ShareSheetMotion.Grow : ShareSheetMotion.Fold;
        _radiusTarget = radius;
        var level = ArcMotion.GetEffectiveLevel(this);
        var moving = animate && old is not null && level != ArcMotionLevel.Off;
        var full = level == ArcMotionLevel.Full;
        if (!moving)
        {
            _jumpNext = true;
        }

        var enter = Ensure(face);
        var wasLeaving = enter.Leaving;
        enter.Leaving = false;
        enter.Generation++;
        face.IsHitTestVisible = true;
        if (old is not null && !ReferenceEquals(old, face) && _faces.TryGetValue(old, out var exit))
        {
            Leave(old, exit, swap, direction, moving, full);
        }
        if (moving)
        {
            Enter(enter, swap, direction, full, wasLeaving);
        }
        else
        {
            enter.Jump(1, 0, 1, 0);
        }
        RetargetSize(face, moving);
        RetargetRadius(moving);
        InvalidateMeasure();
    }

    /// <summary>
    /// Completes once the shape has stopped and every outgoing face has left (true), or false when
    /// <paramref name="stillWanted"/> stops holding while waiting.
    /// </summary>
    public async Task<bool> WhenSettledAsync(Func<bool> stillWanted)
    {
        while (stillWanted())
        {
            if (!IsMoving && LeavingCount == 0)
            {
                return true;
            }
            var pending = new List<Task>(4);
            if (_sizeRun is { IsCompleted: false } size)
            {
                pending.Add(size);
            }
            if (_radiusRun is { IsCompleted: false } radius)
            {
                pending.Add(radius);
            }
            pending.AddRange(_exits.Where(t => !t.IsCompleted));
            if (pending.Count == 0)
            {
                // Nothing to wait on yet the values report motion: give the frame that ends it a chance.
                await Task.Delay(16);
                continue;
            }
            await Task.WhenAny(pending);
        }
        return false;
    }

    private void RetargetSize(Control face, bool animate)
    {
        // Measure the new face now so the shape starts toward it this frame (the stage is attached: templates apply).
        if (TopLevel.GetTopLevel(face) is null)
        {
            _jumpNext = !animate || _jumpNext;
            return;
        }
        var constraint = IsSheet ? new Size(Bounds.Width > 0 ? Bounds.Width : double.PositiveInfinity, double.PositiveInfinity) : Size.Infinity;
        face.Measure(constraint);
        var desired = face.DesiredSize;
        var target = IsSheet && Bounds.Width > 0 ? new Size(Bounds.Width, desired.Height) : desired;
        _target = target;
        _size ??= CreateSize();
        if (!animate || _jumpNext)
        {
            _jumpNext = false;
            _size.Jump(target);
            return;
        }
        _sizeRun = _size.AnimateTo(target, MotionSpec.Spring(_spring));
    }

    /// <summary>Takes every face off and forgets the size (the shape was unmounted).</summary>
    public void Reset()
    {
        foreach (var (face, motion) in _faces)
        {
            motion.Dispose();
            Children.Remove(face);
        }
        _faces.Clear();
        _exits.Clear();
        Current = null;
        _jumpNext = true;
    }

    private FaceMotion Ensure(Control face)
    {
        if (_faces.TryGetValue(face, out var motion))
        {
            return motion;
        }
        motion = new FaceMotion(this, face);
        _faces[face] = motion;
        if (!Children.Contains(face))
        {
            Children.Add(face);
        }
        return motion;
    }

    private void Enter(FaceMotion motion, ShareSheetFaceSwap swap, int direction, bool full, bool fromCurrent)
    {
        // A face caught on its way out turns around from where it is; a new one starts from the far end.
        var fade = MotionSpec.Tween(ShareSheetMotion.FaceEnter, ShareSheetMotion.FaceEnterEasing,
            fromCurrent ? TimeSpan.Zero : ShareSheetMotion.FaceEnterDelay);
        if (!fromCurrent)
        {
            motion.Opacity.Jump(0);
        }
        _ = motion.Opacity.AnimateTo(1, fade);
        if (!full)
        {
            // Reduced: a plain fade, nothing moves or blurs.
            motion.Blur.Jump(0);
            motion.Scale.Jump(1);
            motion.Shift.Jump(0);
            return;
        }
        if (!fromCurrent)
        {
            motion.Blur.Jump(ShareSheetMotion.FaceBlur);
        }
        _ = motion.Blur.AnimateTo(0, fade);
        if (swap == ShareSheetFaceSwap.Scale)
        {
            if (!fromCurrent)
            {
                motion.Shift.Jump(0);
                motion.Scale.Jump(ShareSheetMotion.FaceEnterScale);
            }
            _ = motion.Scale.AnimateTo(1, MotionSpec.Spring(ShareSheetMotion.FaceScale));
            _ = motion.Shift.AnimateTo(0, MotionSpec.Spring(ShareSheetMotion.FaceSlide));
        }
        else
        {
            if (!fromCurrent)
            {
                motion.Scale.Jump(1);
                motion.Shift.Jump(ShareSheetMotion.FaceSlideDistance * direction);
            }
            _ = motion.Scale.AnimateTo(1, MotionSpec.Spring(ShareSheetMotion.FaceScale));
            _ = motion.Shift.AnimateTo(0, MotionSpec.Spring(ShareSheetMotion.FaceSlide));
        }
    }

    private void Leave(Control face, FaceMotion motion, ShareSheetFaceSwap swap, int direction, bool moving, bool full)
    {
        face.IsHitTestVisible = false;
        if (!moving)
        {
            Remove(face, motion);
            return;
        }
        motion.Leaving = true;
        var generation = ++motion.Generation;
        var fade = MotionSpec.Tween(ShareSheetMotion.FaceExit, ArcEasings.Enter);
        if (full)
        {
            _ = motion.Blur.AnimateTo(ShareSheetMotion.FaceBlur, fade);
            if (swap == ShareSheetFaceSwap.Scale)
            {
                _ = motion.Scale.AnimateTo(ShareSheetMotion.FaceExitScaleTo, MotionSpec.Tween(ShareSheetMotion.FaceExitScale, ArcEasings.Standard));
            }
            else
            {
                _ = motion.Shift.AnimateTo(-ShareSheetMotion.FaceSlideDistance * direction, MotionSpec.Spring(ShareSheetMotion.FaceSlide));
            }
        }
        _exits.RemoveAll(t => t.IsCompleted);
        _exits.Add(FinishLeave(face, motion, motion.Opacity.AnimateTo(0, fade), generation));
    }

    private async Task FinishLeave(Control face, FaceMotion motion, Task<bool> fade, int generation)
    {
        await fade;
        if (motion.Leaving && motion.Generation == generation)
        {
            Remove(face, motion);
        }
    }

    private void Remove(Control face, FaceMotion motion)
    {
        motion.Dispose();
        _faces.Remove(face);
        Children.Remove(face);
        FaceRemoved?.Invoke(face);
    }

    private void RetargetRadius(bool animate)
    {
        _radius ??= CreateRadius();
        if (!animate || _jumpNext)
        {
            _radius.Jump(_radiusTarget);
            return;
        }
        _radiusRun = _radius.AnimateTo(_radiusTarget, MotionSpec.Spring(_spring));
    }

    private MotionValue<double> CreateRadius()
    {
        var value = new MotionValue<double>(this, _radiusTarget, MotionVectors.Double);
        value.Changed += _ => ShapeChanged?.Invoke();
        return value;
    }

    private MotionValue<Size> CreateSize()
    {
        var value = new MotionValue<Size>(this, _target, MotionVectors.Size);
        value.Changed += _ =>
        {
            InvalidateMeasure();
            ShapeChanged?.Invoke();
        };
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var faceConstraint = IsSheet ? new Size(availableSize.Width, double.PositiveInfinity) : Size.Infinity;
        foreach (var child in Children)
        {
            child.Measure(faceConstraint);
        }
        if (Current is { } current)
        {
            var desired = current.DesiredSize;
            var target = IsSheet && !double.IsInfinity(availableSize.Width) ? new Size(availableSize.Width, desired.Height) : desired;
            if (target != _target || _size is null)
            {
                _target = target;
                _size ??= CreateSize();
                if (_jumpNext)
                {
                    _jumpNext = false;
                    _size.Jump(target);
                }
                else
                {
                    _sizeRun = _size.AnimateTo(target, MotionSpec.Spring(_spring));
                }
            }
            else if (_jumpNext)
            {
                _jumpNext = false;
            }
        }
        var size = ShapeSize;
        return IsSheet && !double.IsInfinity(availableSize.Width) ? new Size(availableSize.Width, size.Height) : size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var desired = child.DesiredSize;
            var width = IsSheet ? finalSize.Width : desired.Width;
            var height = desired.Height;
            double x;
            double y;
            if (IsSheet)
            {
                x = 0;
                y = 0;
            }
            else
            {
                switch (Align)
                {
                    case ShareSheetAlign.Center:
                        x = (finalSize.Width - width) / 2;
                        y = (finalSize.Height - height) / 2;
                        break;
                    case ShareSheetAlign.Start:
                        x = 0;
                        y = 0;
                        break;
                    default:
                        x = finalSize.Width - width;
                        y = 0;
                        break;
                }
            }
            child.Arrange(new Rect(x, y, width, height));
        }
        return finalSize;
    }

    /// <summary>A face's opacity, blur, scale and sideways shift.</summary>
    private sealed class FaceMotion : IDisposable
    {
        private readonly Control _face;
        private readonly ScaleTransform _scale = new();
        private readonly TranslateTransform _shift = new();

        public FaceMotion(Visual owner, Control face)
        {
            _face = face;
            Opacity = new MotionValue<double>(owner, 1, MotionVectors.Double);
            Blur = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Scale = new MotionValue<double>(owner, 1, MotionVectors.Double);
            Shift = new MotionValue<double>(owner, 0, MotionVectors.Double);
            face.RenderTransformOrigin = RelativePoint.Center;
            face.RenderTransform = new TransformGroup { Children = { _scale, _shift } };
            Opacity.Changed += v => _face.Opacity = Math.Clamp(v, 0, 1);
            Blur.Changed += v => _face.Effect = ArcBlur.Get(Math.Max(0, v));
            Scale.Changed += v =>
            {
                _scale.ScaleX = v;
                _scale.ScaleY = v;
            };
            Shift.Changed += v => _shift.X = v;
        }

        public MotionValue<double> Opacity { get; }

        public MotionValue<double> Blur { get; }

        public MotionValue<double> Scale { get; }

        public MotionValue<double> Shift { get; }

        public bool Leaving { get; set; }

        public int Generation { get; set; }

        public void Jump(double opacity, double blur, double scale, double shift)
        {
            Opacity.Jump(opacity);
            Blur.Jump(blur);
            Scale.Jump(scale);
            Shift.Jump(shift);
        }

        public void Dispose()
        {
            Opacity.Dispose();
            Blur.Dispose();
            Scale.Dispose();
            Shift.Dispose();
            _face.Opacity = 1;
            _face.Effect = null;
            _face.RenderTransform = null;
        }
    }
}

/// <summary>
/// The floating shape: a rounded surface that holds the stage and clips it. Its size and corner radius follow the stage;
/// its fill crosses from the button's fill to the panel's in a short tween and its shadow from resting to floating.
/// </summary>
internal sealed class ShareSheetSurface : Border
{
    private MotionValue<double>? _fill;
    private IDisposable? _fillBinding;
    private IDisposable? _shadowBinding;
    private Color _from;
    private Color _to;
    private bool _toPanel;

    public ShareSheetSurface(ShareSheetStage stage)
    {
        Stage = stage;
        Child = stage;
        ClipToBounds = true;
        stage.ShapeChanged += () => CornerRadius = new CornerRadius(stage.ShapeRadius);
    }

    public ShareSheetStage Stage { get; }

    /// <summary>Whether the surface is (moving to) the panel's look rather than the button's.</summary>
    public bool IsPanelLook => _toPanel;

    /// <summary>Sets the look at once: the button's fill and resting shadow, or the panel's fill and floating shadow.</summary>
    public void JumpLook(bool panel)
    {
        _toPanel = panel;
        _fill?.Jump(panel ? 1 : 0);
        BindLook(panel);
    }

    /// <summary>Crosses the fill to the button's or the panel's color (a 240 ms standard tween; Off jumps).</summary>
    public void AnimateLook(bool panel)
    {
        if (panel == _toPanel && _fill is { IsAnimating: false })
        {
            return;
        }
        _toPanel = panel;
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            JumpLook(panel);
            return;
        }
        _from = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        _to = ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceRaised);
        if (_fill is null)
        {
            // First run: start from the other look (the one the surface shows now).
            _fill = CreateFill();
            _fill.Jump(panel ? 0 : 1);
        }
        ShadowFor(panel);
        _ = Settle(_fill.AnimateTo(panel ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard)), panel);
    }

    private async Task Settle(Task<bool> run, bool panel)
    {
        if (await run && panel == _toPanel)
        {
            BindLook(panel);
        }
    }

    private MotionValue<double> CreateFill()
    {
        var value = new MotionValue<double>(this, _toPanel ? 1 : 0, MotionVectors.Double);
        value.Changed += p => Background = new ImmutableSolidColorBrush(Oklch.MixOklab(_from, _to, Math.Clamp(p, 0, 1)));
        return value;
    }

    private void BindLook(bool panel)
    {
        // At rest the fill follows the token (a theme switch repaints it); the tween's local value goes.
        ClearValue(BackgroundProperty);
        _fillBinding?.Dispose();
        _fillBinding = ArcTokens.Bind(this, BackgroundProperty, panel ? ArcDesignTokenKind.SurfaceRaised : ArcDesignTokenKind.Foreground);
        ShadowFor(panel);
    }

    private void ShadowFor(bool panel)
    {
        _shadowBinding?.Dispose();
        _shadowBinding = ArcTokens.Bind(this, BoxShadowProperty, panel ? ArcDesignTokenKind.ShadowFloating : ArcDesignTokenKind.ShadowResting);
    }
}

/// <summary>
/// The bottom sheet used at or below the breakpoint: a rounded surface inset from the viewport's sides and bottom, a
/// grabber row on top and the stage under it. The owner moves it up and down and handles the drag.
/// </summary>
[TemplatePart(GrabberPart, typeof(Control))]
[TemplatePart(HostPart, typeof(Decorator))]
internal sealed class ShareSheetSheetFrame : TemplatedControl
{
    internal const string GrabberPart = "PART_Grabber";
    internal const string HostPart = "PART_Host";

    private readonly TranslateTransform _shift = new();

    public ShareSheetSheetFrame(ShareSheetStage stage)
    {
        Stage = stage;
        RenderTransform = _shift;
    }

    public ShareSheetStage Stage { get; }

    /// <summary>The grabber row (drag handle) once the template is applied.</summary>
    public Control? Grabber { get; private set; }

    /// <summary>Raised when the template is applied.</summary>
    public event EventHandler? PartsReady;

    /// <summary>How far the sheet is pushed down from its resting place.</summary>
    public double Offset
    {
        get => _shift.Y;
        set => _shift.Y = value;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Decorator>(HostPart) is { } host)
        {
            if (Stage.Parent is Decorator old && !ReferenceEquals(old, host))
            {
                old.Child = null;
            }
            host.Child = Stage;
        }
        Grabber = e.NameScope.Find<Control>(GrabberPart);
        PartsReady?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Mounts the floating shape on an overlay layer as a visual child only, so the shape keeps the share sheet as its
/// logical parent (styles, tokens, theme variant and motion level flow from it). Sized to the shape and placed by the
/// owner with <c>Canvas.Left</c> / <c>Canvas.Top</c>.
/// </summary>
internal sealed class ShareSheetOverlayRoot : Control
{
    private readonly Control _child;

    public ShareSheetOverlayRoot(Control child)
    {
        _child = child;
        VisualChildren.Add(child);
        ClipToBounds = false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _child.Measure(availableSize);
        return _child.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _child.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
