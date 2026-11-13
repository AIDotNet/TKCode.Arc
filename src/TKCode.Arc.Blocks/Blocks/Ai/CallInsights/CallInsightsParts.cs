using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A pressable row of the block (a transcript line, a key moment, a topic chip): a button with no chrome of its own that
/// tints on hover, shows <c>:current</c> (the line under the playhead, the chosen topic) and draws a lazily built focus ring.
/// </summary>
internal sealed class CallInsightsPressable : AvaloniaButton
{
    public const string RingHostPart = "PART_RingHost";
    public const string CurrentPseudoClass = ":current";
    public const string ChipClass = "chip";

    private LazyFocusRing? _focusRing;

    protected override Type StyleKeyOverride => typeof(CallInsightsPressable);

    public bool IsCurrent
    {
        get => Classes.Contains(CurrentPseudoClass);
        set => PseudoClasses.Set(CurrentPseudoClass, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var radius = Classes.Contains(ChipClass) ? ArcDesignTokenKind.RadiusPill : ArcDesignTokenKind.RadiusStep12;
        _focusRing ??= new LazyFocusRing(this, CallInsightsTokens.Key(CallInsightsTokenKind.FocusRingMargin), ArcDesignTokens.Key(radius));
        _focusRing.Attach(e.NameScope.Find<Panel>(RingHostPart), below: true);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}

/// <summary>The talk-time split: one bar cut into each speaker's share, in the speakers' lane colors, 2px apart.</summary>
internal sealed class CallInsightsTalkBar : Control
{
    private readonly AnalyticsPalette _palette = new();
    private IReadOnlyList<CallInsightsTalkShare> _shares = Array.Empty<CallInsightsTalkShare>();

    public CallInsightsTalkBar()
    {
        ActualThemeVariantChanged += (_, _) => Repaint();
        ResourcesChanged += (_, _) => Repaint();
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public double BarHeight { get; set; } = 10;

    public IReadOnlyList<CallInsightsTalkShare> Shares
    {
        get => _shares;
        set
        {
            _shares = value;
            InvalidateVisual();
        }
    }

    private void Repaint()
    {
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(double.IsFinite(availableSize.Width) ? availableSize.Width : 200, BarHeight);

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var width = Bounds.Width;
        var visible = _shares.Count(s => s.Share > 0);
        var gaps = Math.Max(0, visible - 1) * 2;
        var x = 0.0;
        var radius = BarHeight / 2;
        context.DrawRectangle(_palette.Brush(_palette.SurfaceMuted), null, new Rect(0, 0, width, BarHeight), radius, radius);
        for (var i = 0; i < _shares.Count; i++)
        {
            var share = _shares[i].Share;
            if (share <= 0)
            {
                continue;
            }
            var w = Math.Max(2, (width - gaps) * share);
            context.DrawRectangle(_palette.Brush(_palette.Series(i)), null, new Rect(x, 0, w, BarHeight), radius, radius);
            x += w + 2;
        }
    }
}

/// <summary>A small filled dot in a palette color (a speaker's lane color, a moment kind's color).</summary>
internal sealed class CallInsightsDot : Control
{
    private readonly AnalyticsPalette _palette = new();

    public CallInsightsDot()
    {
        Width = 8;
        Height = 8;
        VerticalAlignment = VerticalAlignment.Center;
        ActualThemeVariantChanged += (_, _) => Repaint();
        ResourcesChanged += (_, _) => Repaint();
    }

    /// <summary>The speaker lane index whose color the dot takes; −1 uses <see cref="Moment"/>.</summary>
    public int SeriesIndex { get; set; } = -1;

    public CallInsightsMomentKind Moment { get; set; }

    /// <summary>Draws a ring instead of a disc (moment marks, as on the timeline).</summary>
    public bool IsRing { get; set; }

    private void Repaint()
    {
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var color = SeriesIndex >= 0 ? _palette.Series(SeriesIndex) : CallInsightsTimeline.MomentColor(_palette, Moment);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var r = Math.Min(Bounds.Width, Bounds.Height) / 2;
        if (IsRing)
        {
            context.DrawEllipse(_palette.Brush(_palette.Surface), _palette.Pen(color, 2), center, r - 1, r - 1);
        }
        else
        {
            context.DrawEllipse(_palette.Brush(color), null, center, r, r);
        }
    }
}

/// <summary>Builders for the code-built parts of <see cref="CallInsights"/>: every color and type size is bound to a token.</summary>
internal static class CallInsightsUi
{
    public static TextBlock Text(string? text, ArcDesignTokenKind color = ArcDesignTokenKind.Foreground,
        ArcDesignTokenKind size = ArcDesignTokenKind.TypeSM, FontWeight weight = FontWeight.Normal, double lineHeight = 19.6)
    {
        var block = new TextBlock { Text = text, FontWeight = weight, LineHeight = lineHeight, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        return block;
    }

    public static TextBlock Heading(string text)
    {
        var block = Text(text, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.Medium, 16.8);
        block.TextWrapping = TextWrapping.NoWrap;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        AutomationProperties.SetHeadingLevel(block, 3);
        return block;
    }

    public static TextBlock Tabular(TextBlock block)
    {
        block.FontFeatures = new FontFeatureCollection { FontFeature.Parse("tnum") };
        return block;
    }

    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind? color = null)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, VerticalAlignment = VerticalAlignment.Center };
        if (color is { } kind)
        {
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, kind);
        }
        return glyph;
    }
}
