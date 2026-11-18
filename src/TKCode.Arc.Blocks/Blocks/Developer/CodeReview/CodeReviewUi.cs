using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;

namespace TKCode.Arc;

/// <summary>
/// Motion measured on Arc Code review (design reference, ADR 0053; frame captures in the spec): nearly every change —
/// bands opening and folding, the tab highlight, the file panel's height and slide, the progress fill, the approve
/// button's width — runs on one critically damped spring (k ≈ 300, c ≈ 34.6, settles in about 420ms).
/// </summary>
internal static class CodeReviewMotion
{
    /// <summary>The block's spring (Arc's smooth spring as measured here: ω 17.3, ζ 1).</summary>
    public static readonly SpringSpec Spring = SpringSpec.FromPhysical(300, 34.6);

    /// <summary>Band contents and swapped panels fade in over 160ms ...</summary>
    public static readonly TimeSpan FadeIn = ArcMotionPresets.Fast;

    /// <summary>... and out over 150ms.</summary>
    public static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(150);

    /// <summary>Hover colors (150ms) and the active-line tint (200ms).</summary>
    public static readonly TimeSpan Hover = TimeSpan.FromMilliseconds(150);

    public static readonly TimeSpan Tint = TimeSpan.FromMilliseconds(200);

    /// <summary>The add-comment mark waits 70ms after the pointer arrives, so a passing pointer lights nothing.</summary>
    public static readonly TimeSpan MarkDelay = TimeSpan.FromMilliseconds(70);

    /// <summary>The mark grows from 0.6 to 1 in 440ms on Arc's spring curve.</summary>
    public static readonly TimeSpan MarkGrow = TimeSpan.FromMilliseconds(440);

    /// <summary>A just-applied line keeps its strong tint for 35% of 1400ms, then eases to the added tint.</summary>
    public static readonly TimeSpan FreshHold = TimeSpan.FromMilliseconds(490);

    public static readonly TimeSpan FreshFade = TimeSpan.FromMilliseconds(910);

    /// <summary>The thread of an applied suggestion folds this long after Apply.</summary>
    public static readonly TimeSpan FoldAfterApply = TimeSpan.FromMilliseconds(650);

    /// <summary>How far a file panel slides when another tab is chosen (16).</summary>
    public const double PanelSlide = 16;

    /// <summary>The simulated submit when no <see cref="CodeReview.SubmitReviewAction"/> is set (700ms).</summary>
    public static readonly TimeSpan SimulatedSubmit = TimeSpan.FromMilliseconds(700);
}

/// <summary>Small builders for the parts of <see cref="CodeReview"/> made in code.</summary>
internal static class CodeReviewUi
{
    public static readonly FontFeatureCollection TabularNumbers = FontFeatureCollection.Parse("+tnum");

    /// <summary>A text block with a token-bound color, the body font and a fixed size.</summary>
    public static TextBlock Text(string? text, double size, ArcDesignTokenKind color, FontWeight weight = FontWeight.Normal,
        double lineHeight = double.NaN)
    {
        var block = new TextBlock
        {
            Text = text, FontSize = size, FontWeight = weight, LineHeight = lineHeight, VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        return block;
    }

    /// <summary>A monospaced text block (12px).</summary>
    public static TextBlock Mono(string? text, ArcDesignTokenKind color, FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock { Text = text, FontSize = 12, FontWeight = weight, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        return block;
    }

    /// <summary>A Lucide glyph in a token color.</summary>
    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind color, double stroke = 1.75)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, StrokeThickness = stroke, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    /// <summary>A pill ("New", "Pending"): 12px secondary text on the muted surface, padding 1 7.</summary>
    public static Border Pill(string text)
    {
        var pill = new Border
        {
            Padding = new Thickness(7, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Text(text, 12, ArcDesignTokenKind.TextSecondary, FontWeight.Medium, 16.8)
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(pill, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        return pill;
    }

    /// <summary>Binds a property to a CodeReview token.</summary>
    public static void Bind(StyledElement target, AvaloniaProperty property, CodeReviewTokenKind kind) =>
        target.Bind(property, target.GetResourceObservable(CodeReviewTokens.Key(kind)));

    /// <summary>Reads a CodeReview color token (published as a brush or a color).</summary>
    public static Color Color(Control owner, CodeReviewTokenKind kind)
    {
        if (owner.TryFindResource(CodeReviewTokens.Key(kind), owner.ActualThemeVariant, out var value))
        {
            return value switch
            {
                ISolidColorBrush brush => brush.Color,
                Color color => color,
                _ => Colors.Transparent
            };
        }
        return Colors.Transparent;
    }

    /// <summary>Reads a CodeReview number token, or <paramref name="fallback"/> outside a themed tree.</summary>
    public static double Number(Control owner, CodeReviewTokenKind kind, double fallback) =>
        owner.TryFindResource(CodeReviewTokens.Key(kind), owner.ActualThemeVariant, out var value) && value is double d ? d : fallback;

    /// <summary>Reads a CodeReview thickness token, or <paramref name="fallback"/> outside a themed tree.</summary>
    public static Thickness Thickness(Control owner, CodeReviewTokenKind kind, Thickness fallback) =>
        owner.TryFindResource(CodeReviewTokens.Key(kind), owner.ActualThemeVariant, out var value) && value is Thickness t ? t : fallback;

    /// <summary>Whether <paramref name="owner"/> is shown in the dark theme.</summary>
    public static bool IsDark(Control owner) => owner.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
}
