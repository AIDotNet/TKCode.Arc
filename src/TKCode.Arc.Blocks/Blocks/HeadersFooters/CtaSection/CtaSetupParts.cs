// Ported from Arc's free cta-section block (MIT, Copyright (c) 2026 Elia Kuratli): SetupVisual's marks, progress line and
// faces (cta-section.tsx, .stepMark / .progress / .stepFaces in cta-section.module.css).

using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The 20 px circle of a setup step: a dashed ring while the step is ahead, a solid ring when it is the current one, a solid
/// ring with a fill and a check once it is done. The theme sets the brushes and the check's scale and opacity per state and
/// animates them (colors 240 ms, check scale 580 ms spring curve, check opacity 160 ms). Decorative.
/// </summary>
internal sealed class CtaStepMark : Control
{
    private static readonly DashStyle s_dashes = new(new double[] { 1, 1.3 }, 0);

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<CtaStepMark, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<CtaStepMark, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> CheckBrushProperty =
        AvaloniaProperty.Register<CtaStepMark, IBrush?>(nameof(CheckBrush));

    public static readonly StyledProperty<bool> IsDashedProperty =
        AvaloniaProperty.Register<CtaStepMark, bool>(nameof(IsDashed), true);

    public static readonly StyledProperty<double> CheckScaleProperty =
        AvaloniaProperty.Register<CtaStepMark, double>(nameof(CheckScale), 0.5);

    public static readonly StyledProperty<double> CheckOpacityProperty =
        AvaloniaProperty.Register<CtaStepMark, double>(nameof(CheckOpacity));

    private Pen? _ringPen;
    private IBrush? _ringPenBrush;
    private bool _ringPenDashed;
    private Pen? _checkPen;
    private IBrush? _checkPenBrush;

    static CtaStepMark()
    {
        AffectsRender<CtaStepMark>(RingBrushProperty, FillBrushProperty, CheckBrushProperty, IsDashedProperty, CheckScaleProperty, CheckOpacityProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<CtaStepMark>(false);
        FocusableProperty.OverrideDefaultValue<CtaStepMark>(false);
    }

    public CtaStepMark()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        Width = 20;
        Height = 20;
    }

    /// <summary>The ring color.</summary>
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    /// <summary>The disc behind the check (transparent until done).</summary>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <summary>The check's color.</summary>
    public IBrush? CheckBrush { get => GetValue(CheckBrushProperty); set => SetValue(CheckBrushProperty, value); }

    /// <summary>The ring is dotted (the step is still ahead).</summary>
    public bool IsDashed { get => GetValue(IsDashedProperty); set => SetValue(IsDashedProperty, value); }

    /// <summary>The check's scale (.5 hidden, 1 shown).</summary>
    public double CheckScale { get => GetValue(CheckScaleProperty); set => SetValue(CheckScaleProperty, value); }

    /// <summary>The check's opacity.</summary>
    public double CheckOpacity { get => GetValue(CheckOpacityProperty); set => SetValue(CheckOpacityProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(20, 20);

    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var outer = Math.Min(Bounds.Width, Bounds.Height) / 2;
        if (FillBrush is { } fill)
        {
            context.DrawEllipse(fill, null, center, outer, outer);
        }
        if (RingBrush is { } ring)
        {
            var dashed = IsDashed;
            if (_ringPen is null || !ReferenceEquals(_ringPenBrush, ring) || _ringPenDashed != dashed)
            {
                _ringPen = new Pen(ring, 1.5, dashed ? s_dashes : null, PenLineCap.Flat);
                _ringPenBrush = ring;
                _ringPenDashed = dashed;
            }
            context.DrawEllipse(null, _ringPen, center, outer - 0.75, outer - 0.75);
        }
        var opacity = Math.Clamp(CheckOpacity, 0, 1);
        if (CheckBrush is { } check && opacity > 0.001)
        {
            if (_checkPen is null || !ReferenceEquals(_checkPenBrush, check))
            {
                _checkPen = new Pen(check, 2.75, null, PenLineCap.Round, PenLineJoin.Round);
                _checkPenBrush = check;
            }
            // The 24-unit glyph drawn 12 px wide, scaled about the mark's centre; the stroke scales with it like lucide's.
            var scale = CheckScale * 12.0 / 24.0;
            using (context.PushOpacity(opacity))
            using (context.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y)))
            {
                context.DrawGeometry(null, _checkPen, ArcIcons.Check);
            }
        }
    }
}

/// <summary>
/// The setup card's 2 px progress line: a <c>Border</c> track and a <c>Success</c> fill that grows from the left to
/// <see cref="Value"/> (0 to 1). The theme animates <see cref="Value"/> (480 ms in-out), so under Reduced and Off it jumps.
/// </summary>
internal sealed class CtaProgressLine : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<CtaProgressLine, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CtaProgressLine, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<CtaProgressLine, IBrush?>(nameof(FillBrush));

    static CtaProgressLine()
    {
        AffectsRender<CtaProgressLine>(ValueProperty, TrackBrushProperty, FillBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<CtaProgressLine>(false);
        FocusableProperty.OverrideDefaultValue<CtaProgressLine>(false);
    }

    public CtaProgressLine()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The share done, 0 to 1.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>The track color.</summary>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    /// <summary>The fill color.</summary>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, 2);

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }
        var track = new RoundedRect(new Rect(0, 0, w, h), 1);
        if (TrackBrush is { } trackBrush)
        {
            context.DrawRectangle(trackBrush, null, track);
        }
        var fraction = Math.Clamp(Value, 0, 1);
        if (FillBrush is { } fill && fraction > 0)
        {
            using (context.PushClip(track))
            {
                context.DrawRectangle(fill, null, new Rect(0, 0, w * fraction, h));
            }
        }
    }
}
