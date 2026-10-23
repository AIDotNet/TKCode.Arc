// Ported from Arc's free site-header / site-footer blocks (MIT, Copyright (c) 2026 Elia Kuratli): the arch mark geometry.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The arch mark of the Headers and footers blocks (Arc's <c>ArcMark</c>): three round-capped strokes on a 64×64 grid,
/// stroke 5.5, drawn in <see cref="Foreground"/> and scaled to the control's smaller side (22×22 by default). Decorative:
/// screen readers skip it (the brand name next to it carries the meaning).
/// </summary>
internal sealed class SiteArcMark : Control
{
    /// <summary>The three strokes of the arch, in the 64×64 viewBox.</summary>
    public const string PathData = "M9 48V29C9 15 19 7 32 7s23 8 23 22v19 M20 48V31c0-8 5-13 12-13s12 5 12 13v17 M32 38v10";

    /// <summary>Stroke width in viewBox units.</summary>
    public const double ViewBoxStroke = 5.5;

    /// <summary>Side of the square viewBox.</summary>
    public const double ViewBoxSize = 64;

    /// <summary>Stroke color (the brand text color).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<SiteArcMark, IBrush?>(nameof(Foreground));

    private static Geometry? s_geometry;
    private IPen? _pen;
    private double _penScale;

    static SiteArcMark()
    {
        AffectsRender<SiteArcMark>(ForegroundProperty);
        WidthProperty.OverrideDefaultValue<SiteArcMark>(22);
        HeightProperty.OverrideDefaultValue<SiteArcMark>(22);
        IsHitTestVisibleProperty.OverrideDefaultValue<SiteArcMark>(false);
        FocusableProperty.OverrideDefaultValue<SiteArcMark>(false);
    }

    public SiteArcMark()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The shared, parsed arch geometry (viewBox units).</summary>
    public static Geometry Geometry => s_geometry ??= Avalonia.Media.Geometry.Parse(PathData);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            _pen = null;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var side = Math.Min(Bounds.Width, Bounds.Height);
        if (side <= 0 || Foreground is not { } brush)
        {
            return;
        }
        var scale = side / ViewBoxSize;
        if (_pen is null || Math.Abs(_penScale - scale) > 1e-6)
        {
            // The pen is in viewBox units because the geometry is drawn under the scale transform below.
            _pen = new ImmutablePen(brush.ToImmutable(), ViewBoxStroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penScale = scale;
        }
        var offset = new Point((Bounds.Width - side) / 2, (Bounds.Height - side) / 2);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y)))
        {
            context.DrawGeometry(null, _pen, Geometry);
        }
    }
}
