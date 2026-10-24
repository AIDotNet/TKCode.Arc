// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): the ring backdrop and useDrift of login-centered.tsx.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The quiet line backdrop behind the card: two axes and fourteen concentric rings around the block's center (radius 224,
/// then every 60), every third ring a shade stronger. The outer rings fade into the canvas through a radial mask
/// (an ellipse of 72% × 78% of the block, opaque to 38%). Nothing is filled. A fine pointer moving over the block drifts
/// the set by up to a pixel per unit of depth, outer rings further, on a .9s spring; never under reduced motion.
/// </summary>
/// <remarks>
/// Self-drawn: fifteen strokes and one mask are cheaper than fifteen shapes, and the drift is a per-frame offset that a
/// ControlTheme cannot express (it follows the pointer through a spring).
/// </remarks>
internal sealed class LoginCenteredBackdrop : Control
{
    /// <summary>The axes and ordinary rings (Arc's <c>--border-subtle</c>).</summary>
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<LoginCenteredBackdrop, IBrush?>(nameof(LineBrush));

    /// <summary>Every third ring (Arc's <c>--border</c>).</summary>
    public static readonly StyledProperty<IBrush?> MajorLineBrushProperty =
        AvaloniaProperty.Register<LoginCenteredBackdrop, IBrush?>(nameof(MajorLineBrush));

    internal const int RingCount = 14;
    internal const double FirstRadius = 224;
    internal const double RingStep = 60;
    internal const double DepthStep = 0.34;
    private const double AxisReach = 2400;

    private static readonly IBrush s_mask = new ImmutableRadialGradientBrush(new RadialGradientBrush
    {
        RadiusX = new RelativeScalar(0.72, RelativeUnit.Relative),
        RadiusY = new RelativeScalar(0.78, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Colors.Black, 0.38), new GradientStop(Colors.Transparent, 1) }
    });

    private MotionValue<Point>? _drift;
    private Point _offset;
    private Pen? _pen;
    private Pen? _majorPen;

    static LoginCenteredBackdrop()
    {
        AffectsRender<LoginCenteredBackdrop>(LineBrushProperty, MajorLineBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginCenteredBackdrop>(false);
        FocusableProperty.OverrideDefaultValue<LoginCenteredBackdrop>(false);
    }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public IBrush? MajorLineBrush { get => GetValue(MajorLineBrushProperty); set => SetValue(MajorLineBrushProperty, value); }

    /// <summary>The drift right now in pixels per unit of depth, −1 to 1 on each axis (tests read it).</summary>
    internal Point Offset => _offset;

    /// <summary>The ring radii and depth factors, inner first (Arc's <c>rings</c>).</summary>
    internal static (double Radius, double Depth, bool IsMajor) Ring(int index)
        => (FirstRadius + index * RingStep, 1 + index * DepthStep, index % 3 == 2);

    /// <summary>
    /// Moves the drift target: <paramref name="x"/> and <paramref name="y"/> are −1 to 1 (Arc's
    /// <c>((clientX − left) / width − .5) × −2</c>). Under anything but full motion the set stays still.
    /// </summary>
    public void DriftTo(double x, double y)
    {
        if (!ArcMotion.IsFull(this))
        {
            ResetDrift();
            return;
        }
        if (_drift is null)
        {
            _drift = new MotionValue<Point>(this, _offset, MotionVectors.Point);
            _drift.Changed += p =>
            {
                _offset = p;
                InvalidateVisual();
            };
        }
        _ = _drift.AnimateTo(new Point(x, y), MotionSpec.Spring(LoginCenteredMotion.Drift));
    }

    /// <summary>Springs back to center (Arc's <c>onPointerLeave</c>).</summary>
    public void Release()
    {
        if (_drift is null || !ArcMotion.IsFull(this))
        {
            ResetDrift();
            return;
        }
        _ = _drift.AnimateTo(default, MotionSpec.Spring(LoginCenteredMotion.Drift));
    }

    private void ResetDrift()
    {
        _drift?.Jump(default);
        if (_offset != default)
        {
            _offset = default;
            InvalidateVisual();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineBrushProperty || change.Property == MajorLineBrushProperty)
        {
            _pen = null;
            _majorPen = null;
        }
        else if (change.Property == ArcMotion.LevelProperty && !ArcMotion.IsFull(this))
        {
            ResetDrift();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _drift?.Stop();
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        _pen ??= LineBrush is { } line ? new Pen(line, 1) : null;
        _majorPen ??= MajorLineBrush is { } major ? new Pen(major, 1) : null;
        if (_pen is null)
        {
            return;
        }
        var center = bounds.Center;
        using (context.PushClip(bounds))
        using (context.PushOpacityMask(s_mask, bounds))
        {
            // The axes move with the drift itself (depth 1); the rings by their depth.
            var axis = center + _offset;
            context.DrawLine(_pen, new Point(axis.X - AxisReach, axis.Y), new Point(axis.X + AxisReach, axis.Y));
            context.DrawLine(_pen, new Point(axis.X, axis.Y - AxisReach), new Point(axis.X, axis.Y + AxisReach));
            for (var i = 0; i < RingCount; i++)
            {
                var (radius, depth, isMajor) = Ring(i);
                var ring = center + _offset * depth;
                context.DrawEllipse(null, isMajor ? _majorPen ?? _pen : _pen, ring, radius, radius);
            }
        }
    }
}
