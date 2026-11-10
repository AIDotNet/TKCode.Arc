using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A hidden live region of <see cref="LeadScoring"/>: screen readers say its name when it changes, repeats included.</summary>
internal sealed class LeadScoringAnnouncer : Control
{
    private string? _last;
    private bool _toggle;

    public LeadScoringAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last announced text.</summary>
    public string? Announcement => _last;

    public void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        _toggle = text == _last && !_toggle;
        _last = text;
        AutomationProperties.SetName(this, _toggle ? text + "​" : text);
    }

    protected override Size MeasureOverride(Size availableSize) => default;
}

/// <summary>The bar of actions over several selected rows; it fades in while rows are selected.</summary>
internal sealed class LeadScoringBulkBar : ContentControl
{
    public bool IsShown
    {
        get => Classes.Contains(":shown");
        set
        {
            PseudoClasses.Set(":shown", value);
            IsHitTestVisible = value;
            IsVisible = value;
        }
    }
}

/// <summary>
/// A horizontal score bar: a muted track and a fill to <see cref="Fraction"/> (0–1) in the band's color; for factors,
/// <see cref="IsSigned"/> draws from a center line, right for positive points (success) and left for negative (danger).
/// </summary>
internal sealed class LeadScoringBar : Control
{
    private readonly AnalyticsPalette _palette = new();

    public LeadScoringBar()
    {
        Height = 4;
        VerticalAlignment = VerticalAlignment.Center;
        ActualThemeVariantChanged += (_, _) => Repaint();
        ResourcesChanged += (_, _) => Repaint();
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The filled share: 0–1, or −1–1 when signed.</summary>
    public double Fraction { get; set; }

    public bool IsSigned { get; set; }

    /// <summary>0 hot (success), 1 warm (warning), 2 cold (muted); ignored when signed.</summary>
    public int Band { get; set; }

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
        var p = _palette;
        var w = Bounds.Width;
        var h = Bounds.Height;
        var r = h / 2;
        context.DrawRectangle(p.Brush(p.SurfaceMuted), null, new Rect(0, 0, w, h), r, r);
        if (IsSigned)
        {
            var mid = w / 2;
            var len = Math.Abs(Math.Clamp(Fraction, -1, 1)) * mid;
            var color = Fraction >= 0 ? p.Success : p.Danger;
            var x = Fraction >= 0 ? mid : mid - len;
            context.DrawRectangle(p.Brush(color), null, new Rect(x, 0, Math.Max(2, len), h), r, r);
            context.FillRectangle(p.Brush(p.BorderStrong), new Rect(mid - 0.5, -2, 1, h + 4));
            return;
        }
        var fill = Band switch { 0 => p.Success, 1 => p.Warning, _ => p.TextMuted };
        context.DrawRectangle(p.Brush(fill), null, new Rect(0, 0, Math.Max(h, w * Math.Clamp(Fraction, 0, 1)), h), r, r);
    }
}

/// <summary>Builders for the code-built parts of <see cref="LeadScoring"/>.</summary>
internal static class LeadScoringUi
{
    public static TextBlock Text(string? text, ArcDesignTokenKind color = ArcDesignTokenKind.Foreground,
        ArcDesignTokenKind size = ArcDesignTokenKind.TypeSM, FontWeight weight = FontWeight.Normal, double lineHeight = 19.6, bool wrap = false)
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
        var block = Text(text, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.Medium, 16.8);
        AutomationProperties.SetHeadingLevel(block, 3);
        return block;
    }

    public static TextBlock Tabular(TextBlock block)
    {
        block.FontFeatures = new FontFeatureCollection { FontFeature.Parse("tnum") };
        return block;
    }
}
