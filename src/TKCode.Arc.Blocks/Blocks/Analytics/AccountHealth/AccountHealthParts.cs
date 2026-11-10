using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The health trend: the weekly scores as a line on a 0–100 scale, with dashed guides at the healthy and watch thresholds
/// and a dot on this week, in the color of this week's band. The line draws in from the left the first time (Full motion).
/// </summary>
internal sealed class AccountHealthTrend : Control
{
    private readonly AnalyticsPalette _palette = new();
    private IReadOnlyList<double> _values = Array.Empty<double>();
    private StreamGeometry? _line;
    private Size _lineSize;
    private MotionValue<double>? _reveal;
    private double _revealValue = 1;
    private bool _revealed;

    public AccountHealthTrend()
    {
        ActualThemeVariantChanged += (_, _) => Repaint();
        ResourcesChanged += (_, _) => Repaint();
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public double HealthyFrom { get; set; } = 75;

    public double WatchFrom { get; set; } = 50;

    public IReadOnlyList<double> Values
    {
        get => _values;
        set
        {
            _values = value;
            _line = null;
            InvalidateVisual();
        }
    }

    internal double RevealValue => _revealValue;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_revealed || !ArcMotion.IsFull(this))
        {
            _revealed = true;
            return;
        }
        _revealed = true;
        _reveal = new MotionValue<double>(this, 0, MotionVectors.Double);
        _reveal.Changed += v =>
        {
            _revealValue = v;
            InvalidateVisual();
        };
        _revealValue = 0;
        _ = _reveal.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(700), ArcEasings.InOut));
    }

    private void Repaint()
    {
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    private double Y(double score) => 3 + (Bounds.Height - 6) * (1 - Math.Clamp(score, 0, 100) / 100);

    private double X(int index) => _values.Count <= 1 ? Bounds.Width / 2 : 4 + (Bounds.Width - 8) * index / (_values.Count - 1);

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var p = _palette;
        var guide = p.Pen(p.Border, 1, dash: 3, gap: 3);
        context.DrawLine(guide, new Point(0, Y(HealthyFrom)), new Point(Bounds.Width, Y(HealthyFrom)));
        context.DrawLine(guide, new Point(0, Y(WatchFrom)), new Point(Bounds.Width, Y(WatchFrom)));
        if (_values.Count == 0)
        {
            return;
        }
        if (_line is null || _lineSize != Bounds.Size)
        {
            _line = new StreamGeometry();
            using (var g = _line.Open())
            {
                g.BeginFigure(new Point(X(0), Y(_values[0])), false);
                for (var i = 1; i < _values.Count; i++)
                {
                    g.LineTo(new Point(X(i), Y(_values[i])));
                }
                g.EndFigure(false);
            }
            _lineSize = Bounds.Size;
        }
        var last = _values[^1];
        var color = last >= HealthyFrom ? p.Success : last >= WatchFrom ? p.Warning : p.Danger;
        using (context.PushClip(new Rect(0, 0, Bounds.Width * _revealValue + 1, Bounds.Height)))
        {
            context.DrawGeometry(null, p.Pen(color, 2), _line);
        }
        if (_revealValue >= 1)
        {
            context.DrawEllipse(p.Brush(color), p.Pen(p.Surface, 2), new Point(X(_values.Count - 1), Y(last)), 4, 4);
        }
    }
}

/// <summary>A small filled dot in a status color (ticket priority, risk level).</summary>
internal sealed class AccountHealthDot : Control
{
    public AccountHealthDot(ArcDesignTokenKind color)
    {
        Width = 8;
        Height = 8;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, 6, 0, 0);
        ArcTokens.Bind(this, FillProperty, color);
    }

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<AccountHealthDot, IBrush?>(nameof(Fill));

    static AccountHealthDot()
    {
        AffectsRender<AccountHealthDot>(FillProperty);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public override void Render(DrawingContext context)
        => context.DrawEllipse(Fill, null, new Point(Bounds.Width / 2, Bounds.Height / 2), Bounds.Width / 2, Bounds.Height / 2);
}

/// <summary>Builders for the code-built parts of <see cref="AccountHealth"/>.</summary>
internal static class AccountHealthUi
{
    public static TextBlock Text(string? text, ArcDesignTokenKind color = ArcDesignTokenKind.Foreground,
        ArcDesignTokenKind size = ArcDesignTokenKind.TypeSM, FontWeight weight = FontWeight.Normal, double lineHeight = 19.6, bool wrap = true)
    {
        var block = new TextBlock
        {
            Text = text,
            FontWeight = weight,
            LineHeight = lineHeight,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        return block;
    }

    public static TextBlock Heading(string text)
    {
        var block = Text(text, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.Medium, 16.8, wrap: false);
        AutomationProperties.SetHeadingLevel(block, 3);
        return block;
    }

    public static TextBlock Big(string? text)
    {
        var block = Text(text, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.Type2XL, FontWeight.Medium, 32, wrap: false);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        block.FontFeatures = new FontFeatureCollection { FontFeature.Parse("tnum") };
        return block;
    }

    public static Border Tile(Control child)
    {
        var tile = new Border { Child = child, Padding = new Thickness(16), BorderThickness = new Thickness(1) }.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        ArcTokens.Bind(tile, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return tile;
    }
}
