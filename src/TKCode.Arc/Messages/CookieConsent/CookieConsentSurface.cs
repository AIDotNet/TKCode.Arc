using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Rendering;

namespace TKCode.Arc;

/// <summary>Which face a <see cref="CookieConsent"/> shows.</summary>
internal enum CookieConsentMode
{
    None,
    Bar,
    Panel,
    Chip
}

/// <summary>Horizontal anchor of a face inside the layer.</summary>
internal enum CookieConsentAlign
{
    Left,
    Center,
    Right
}

/// <summary>
/// The single surface of a <see cref="CookieConsent"/>: its position, width, height and corner spring between the
/// consent bar, the preferences panel and the 44px reopen button (larger shapes on a slightly bouncy 0.46s spring,
/// smaller ones on a flat 0.38s spring, so folding never overshoots). The surface is drawn in <see cref="Render"/>;
/// faces sit on the bottom edge at their final width and are clipped to the moving shape, cross-fading out of a
/// short blur. Nothing relayouts while the shape moves.
/// </summary>
internal sealed class CookieConsentSurface : Control, ICustomHitTest
{
    public const double BarMax = 660;
    public const double PanelMax = 420;
    public const double ChipSize = 44;

    public static readonly StyledProperty<IBrush?> SurfaceBackgroundProperty =
        AvaloniaProperty.Register<CookieConsentSurface, IBrush?>(nameof(SurfaceBackground));

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<CookieConsentSurface, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<BoxShadows> SurfaceShadowProperty =
        AvaloniaProperty.Register<CookieConsentSurface, BoxShadows>(nameof(SurfaceShadow));

    public IBrush? SurfaceBackground { get => GetValue(SurfaceBackgroundProperty); set => SetValue(SurfaceBackgroundProperty, value); }
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public BoxShadows SurfaceShadow { get => GetValue(SurfaceShadowProperty); set => SetValue(SurfaceShadowProperty, value); }

    private static readonly SpringSpec Grow = SpringSpec.FromVisual(0.46, 0.14);
    private static readonly SpringSpec Shrink = SpringSpec.FromVisual(0.38, 0);
    private static readonly SpringSpec Arrive = SpringSpec.FromVisual(0.5, 0.1);
    private static readonly SpringSpec FaceRise = SpringSpec.FromVisual(0.42, 0.08);

    private sealed class Face
    {
        public required Control Control { get; init; }
        public required CookieConsentMode Mode { get; init; }
        public required TranslateTransform Translate { get; init; }
        public required RectangleGeometry Clip { get; init; }
        public required MotionValue<double> Fade { get; init; }
        public required MotionValue<double> Lift { get; init; }
        public required MotionValue<double> Blur { get; init; }
        public BlurEffect? Effect { get; set; }
        public bool Present { get; set; }
        public int Generation { get; set; }
        public double Width { get; set; }
        public CookieConsentAlign Align { get; set; }
    }

    private readonly List<Face> _faces = new();
    private readonly MotionValue<double> _x;
    private readonly MotionValue<double> _w;
    private readonly MotionValue<double> _h;
    private readonly MotionValue<double> _r;
    private readonly MotionValue<double> _enterY;
    private readonly MotionValue<double> _enterScale;
    private readonly MotionValue<double> _enterOpacity;
    private readonly MatrixTransform _transform = new();
    private Pen? _ringPen;
    private CookieConsentMode _mode;
    private CookieConsentAlign _barAlign = CookieConsentAlign.Center;
    private CookieConsentAlign _panelAlign = CookieConsentAlign.Center;
    private CookieConsentAlign _chipAlign = CookieConsentAlign.Left;
    private bool _shapeReady;
    private double _shapeArea;
    private Rect _targetShape;
    private double _targetRadius;
    private int _exitGeneration;
    private double _layerWidth;

    /// <summary>The faces (bar, panel, reopen button), declared in the template.</summary>
    [Content]
    public Avalonia.Controls.Controls Children { get; } = new();

    public CookieConsentSurface()
    {
        Children.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (Control child in e.OldItems)
                {
                    LogicalChildren.Remove(child);
                    VisualChildren.Remove(child);
                }
            }
            if (e.NewItems is not null)
            {
                foreach (Control child in e.NewItems)
                {
                    LogicalChildren.Add(child);
                    VisualChildren.Add(child);
                }
            }
        };
        _x = CreateValue(0);
        _w = CreateValue(0);
        _h = CreateValue(0);
        _r = CreateValue(22);
        _enterY = CreateValue(0);
        _enterScale = CreateValue(1);
        _enterOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _enterOpacity.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.TopLeft;
        IsVisible = false;
    }

    static CookieConsentSurface()
    {
        AffectsRender<CookieConsentSurface>(SurfaceBackgroundProperty, RingBrushProperty, SurfaceShadowProperty);
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => OnShapeChanged();
        return value;
    }

    public CookieConsentMode Mode => _mode;

    /// <summary>The current (animated) shape in local coordinates.</summary>
    internal Rect Shape => new(_x.Value, _arranged.Height - _h.Value, Math.Max(0, _w.Value), Math.Max(0, _h.Value));

    /// <summary>The last arranged size (Bounds lags behind during ArrangeOverride).</summary>
    private Size _arranged;

    internal double ShapeRadius => _r.Value;

    internal Rect TargetShape => _targetShape;

    public void SetFaces(Control? bar, Control? panel, Control? chip)
    {
        _faces.Clear();
        AddFace(bar, CookieConsentMode.Bar);
        AddFace(panel, CookieConsentMode.Panel);
        AddFace(chip, CookieConsentMode.Chip);
    }

    private void AddFace(Control? control, CookieConsentMode mode)
    {
        if (control is null)
        {
            return;
        }
        var translate = new TranslateTransform();
        var face = new Face
        {
            Control = control,
            Mode = mode,
            Translate = translate,
            Clip = new RectangleGeometry(),
            Fade = new MotionValue<double>(control, 0, MotionVectors.Double),
            Lift = new MotionValue<double>(control, 0, MotionVectors.Double),
            Blur = new MotionValue<double>(control, 0, MotionVectors.Double)
        };
        control.RenderTransform = translate;
        control.IsVisible = false;
        face.Fade.Changed += v => control.Opacity = Math.Clamp(v, 0, 1);
        face.Lift.Changed += v =>
        {
            translate.Y = v;
            UpdateFaceClip(face);
        };
        face.Blur.Changed += v =>
        {
            if (v < 0.05)
            {
                control.Effect = null;
                return;
            }
            face.Effect ??= new BlurEffect();
            face.Effect.Radius = v;
            control.Effect = face.Effect;
        };
        _faces.Add(face);
    }

    /// <summary>Switches the face; the surface then springs to the new face's shape.</summary>
    public void SetMode(CookieConsentMode mode, CookieConsentAlign barAlign, CookieConsentAlign panelAlign, CookieConsentAlign chipAlign)
    {
        var changed = mode != _mode;
        _barAlign = barAlign;
        _panelAlign = panelAlign;
        _chipAlign = chipAlign;
        var previous = _mode;
        _mode = mode;
        if (changed)
        {
            var level = ArcMotion.GetEffectiveLevel(this);
            var animate = IsLoaded && level != ArcMotionLevel.Off && previous != CookieConsentMode.None && mode != CookieConsentMode.None;
            foreach (var face in _faces)
            {
                var present = face.Mode == mode;
                if (present == face.Present)
                {
                    continue;
                }
                face.Present = present;
                if (present)
                {
                    EnterFace(face, animate, level == ArcMotionLevel.Full);
                }
                else
                {
                    LeaveFace(face, animate, level == ArcMotionLevel.Full);
                }
            }
            RunPresence(previous, mode, level);
        }
        InvalidateMeasure();
    }

    private void EnterFace(Face face, bool animate, bool full)
    {
        face.Generation++;
        var control = face.Control;
        control.IsVisible = true;
        control.IsHitTestVisible = true;
        AutomationProperties.SetAccessibilityView(control, AccessibilityView.Default);
        if (!animate)
        {
            face.Fade.Jump(1);
            face.Lift.Jump(0);
            face.Blur.Jump(0);
            return;
        }
        if (full)
        {
            face.Fade.Jump(0);
            face.Lift.Jump(10);
            face.Blur.Jump(4);
            _ = face.Lift.AnimateTo(0, MotionSpec.Spring(FaceRise));
            _ = face.Fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(0.22), ArcEasings.Enter, TimeSpan.FromSeconds(0.04)));
            _ = face.Blur.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromSeconds(0.26), ArcEasings.Enter, TimeSpan.FromSeconds(0.04)));
        }
        else
        {
            face.Lift.Jump(0);
            face.Blur.Jump(0);
            face.Fade.Jump(0);
            _ = face.Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
    }

    private void LeaveFace(Face face, bool animate, bool full)
    {
        var generation = ++face.Generation;
        var control = face.Control;
        control.IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(control, AccessibilityView.Raw);
        if (!animate)
        {
            face.Fade.Jump(0);
            control.IsVisible = false;
            return;
        }
        Task<bool> done;
        if (full)
        {
            var spec = MotionSpec.Tween(TimeSpan.FromSeconds(0.12), ArcEasings.Standard);
            _ = face.Lift.AnimateTo(4, spec);
            _ = face.Blur.AnimateTo(4, spec);
            done = face.Fade.AnimateTo(0, spec);
        }
        else
        {
            done = face.Fade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromSeconds(0.1), ArcEasings.Standard));
        }
        HideWhenDone(face, generation, done);
    }

    private static void HideWhenDone(Face face, int generation, Task<bool> task)
    {
        task.ThenOnUiThread(() =>
        {
            if (face.Generation == generation && !face.Present)
            {
                face.Control.IsVisible = false;
                face.Control.Effect = null;
            }
        });
    }

    /// <summary>The whole surface rises in from the bottom edge, and fades out when nothing is left to show.</summary>
    private void RunPresence(CookieConsentMode previous, CookieConsentMode mode, ArcMotionLevel level)
    {
        var animate = IsLoaded && level != ArcMotionLevel.Off;
        if (previous == CookieConsentMode.None && mode != CookieConsentMode.None)
        {
            _exitGeneration++;
            IsVisible = true;
            _shapeReady = false;
            if (!animate)
            {
                _enterY.Jump(0);
                _enterScale.Jump(1);
                _enterOpacity.Jump(1);
            }
            else if (level == ArcMotionLevel.Full)
            {
                _enterY.Jump(24);
                _enterScale.Jump(0.98);
                _enterOpacity.Jump(0);
                _ = _enterY.AnimateTo(0, MotionSpec.Spring(Arrive));
                _ = _enterScale.AnimateTo(1, MotionSpec.Spring(Arrive));
                _ = _enterOpacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(0.24), ArcEasings.Enter));
            }
            else
            {
                _enterY.Jump(0);
                _enterScale.Jump(1);
                _enterOpacity.Jump(0);
                _ = _enterOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
        }
        else if (mode == CookieConsentMode.None && previous != CookieConsentMode.None)
        {
            var generation = ++_exitGeneration;
            if (!animate)
            {
                IsVisible = false;
                return;
            }
            Task<bool> done;
            if (level == ArcMotionLevel.Full)
            {
                var spec = MotionSpec.Tween(TimeSpan.FromSeconds(0.18), ArcEasings.Standard);
                _ = _enterY.AnimateTo(12, spec);
                _ = _enterScale.AnimateTo(0.96, spec);
                done = _enterOpacity.AnimateTo(0, spec);
            }
            else
            {
                done = _enterOpacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            HideSurfaceWhenDone(generation, done);
        }
    }

    private void HideSurfaceWhenDone(int generation, Task<bool> task)
    {
        task.ThenOnUiThread(() =>
        {
            if (generation == _exitGeneration && _mode == CookieConsentMode.None)
            {
                IsVisible = false;
                foreach (var face in _faces)
                {
                    face.Control.IsVisible = false;
                }
            }
        });
    }

    private static double Place(double layer, double width, CookieConsentAlign align) => align switch
    {
        CookieConsentAlign.Left => 0,
        CookieConsentAlign.Right => layer - width,
        _ => (layer - width) / 2
    };

    private double WidthOf(CookieConsentMode mode, double layer) => mode switch
    {
        CookieConsentMode.Bar => Math.Min(layer, BarMax),
        CookieConsentMode.Panel => Math.Min(layer, PanelMax),
        _ => ChipSize
    };

    private CookieConsentAlign AlignOf(CookieConsentMode mode) => mode switch
    {
        CookieConsentMode.Bar => _barAlign,
        CookieConsentMode.Panel => _panelAlign,
        _ => _chipAlign
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        var layer = double.IsInfinity(availableSize.Width) ? BarMax : availableSize.Width;
        _layerWidth = layer;
        foreach (var face in _faces)
        {
            face.Width = WidthOf(face.Mode, layer);
            face.Align = AlignOf(face.Mode);
            face.Control.Measure(new Size(face.Width, double.PositiveInfinity));
        }
        var current = FaceOf(_mode);
        if (current is null)
        {
            return new Size(layer, 0);
        }
        var height = current.Mode == CookieConsentMode.Chip ? ChipSize : current.Control.DesiredSize.Height;
        UpdateShapeTarget(layer, current, height);
        return new Size(layer, height);
    }

    private Face? FaceOf(CookieConsentMode mode)
    {
        foreach (var face in _faces)
        {
            if (face.Mode == mode)
            {
                return face;
            }
        }
        return null;
    }

    private void UpdateShapeTarget(double layer, Face face, double height)
    {
        if (height <= 0 || layer <= 0)
        {
            return;
        }
        var width = face.Width;
        var x = Place(layer, width, face.Align);
        var radius = face.Mode switch
        {
            CookieConsentMode.Chip => ChipSize / 2,
            CookieConsentMode.Panel => 26,
            _ => 22
        };
        var target = new Rect(x, 0, width, height);
        if (_shapeReady && target == _targetShape && Math.Abs(radius - _targetRadius) < 0.01)
        {
            return;
        }
        _targetShape = target;
        _targetRadius = radius;
        if (!_shapeReady || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _x.Jump(x);
            _w.Jump(width);
            _h.Jump(height);
            _r.Jump(radius);
            _shapeReady = true;
            _shapeArea = width * height;
            return;
        }
        var area = width * height;
        var spring = MotionSpec.Spring(area >= _shapeArea ? Grow : Shrink);
        _shapeArea = area;
        _ = _x.AnimateTo(x, spring);
        _ = _w.AnimateTo(width, spring);
        _ = _h.AnimateTo(height, spring);
        _ = _r.AnimateTo(radius, spring);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _arranged = finalSize;
        foreach (var face in _faces)
        {
            var height = face.Mode == CookieConsentMode.Chip ? ChipSize : face.Control.DesiredSize.Height;
            var x = Place(finalSize.Width, face.Width, face.Align);
            face.Control.Arrange(new Rect(x, finalSize.Height - height, face.Width, height));
        }
        OnShapeChanged();
        return finalSize;
    }

    private void OnShapeChanged()
    {
        var shape = Shape;
        var center = new Point(shape.X + shape.Width / 2, _arranged.Height);
        var s = _enterScale.Value;
        // transform-origin: bottom center; the arrival lifts from 24px below.
        _transform.Matrix = Matrix.CreateTranslation(-center.X, -center.Y) *
                            Matrix.CreateScale(s, s) *
                            Matrix.CreateTranslation(center.X, center.Y + _enterY.Value);
        foreach (var face in _faces)
        {
            UpdateFaceClip(face);
        }
        InvalidateVisual();
    }

    /// <summary>Clips a face to the surface, in the face's own coordinates (the clip moves with its lift).</summary>
    private void UpdateFaceClip(Face face)
    {
        if (!face.Control.IsVisible)
        {
            return;
        }
        var shape = Shape;
        var bounds = face.Control.Bounds;
        face.Clip.Rect = new Rect(shape.X - bounds.X, shape.Y - bounds.Y - face.Translate.Y, shape.Width, shape.Height);
        face.Clip.RadiusX = _r.Value;
        face.Clip.RadiusY = _r.Value;
        face.Control.Clip = face.Clip;
        face.Control.InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var shape = Shape;
        if (shape.Width <= 0 || shape.Height <= 0)
        {
            return;
        }
        var radius = Math.Min(_r.Value, Math.Min(shape.Width, shape.Height) / 2);
        var rounded = new RoundedRect(shape, radius);
        context.DrawRectangle(SurfaceBackground, null, rounded, SurfaceShadow);
        if (RingBrush is { } ring)
        {
            if (_ringPen is null || !ReferenceEquals(_ringPen.Brush, ring))
            {
                _ringPen = new Pen(ring, 1);
            }
            // box-shadow: 0 0 0 1px var(--border): a hairline just outside the surface.
            context.DrawRectangle(null, _ringPen, new RoundedRect(shape.Inflate(0.5), radius + 0.5));
        }
    }

    bool ICustomHitTest.HitTest(Point point) => _mode != CookieConsentMode.None && Shape.Contains(point);
}
