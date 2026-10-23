using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The ink of the paper (spec 3.3): fixed grays that never follow the theme, because the paper is white in the dark theme
/// too. Lightness in OKLCH: ink 21%, secondary 40%, quiet 55%, thin rules 90%, strong rules 28%.
/// </summary>
internal static class DocumentInk
{
    /// <summary>Body text (L 21%).</summary>
    public static readonly Color Ink = Oklch.ToColor(0.21, 0, 0);

    /// <summary>The paper.</summary>
    public static readonly IImmutableSolidColorBrush PaperBrush = new ImmutableSolidColorBrush(Colors.White);

    /// <summary>Body text (L 21%).</summary>
    public static readonly IImmutableSolidColorBrush InkBrush = new ImmutableSolidColorBrush(Ink);

    /// <summary>Labels of the totals (L 40%).</summary>
    public static readonly IImmutableSolidColorBrush SecondaryBrush = new ImmutableSolidColorBrush(Oklch.ToColor(0.40, 0, 0));

    /// <summary>Labels, details, notes and the footer (L 55%).</summary>
    public static readonly IImmutableSolidColorBrush QuietBrush = new ImmutableSolidColorBrush(Oklch.ToColor(0.55, 0, 0));

    /// <summary>Thin rules between rows and around the meta strip (L 90%).</summary>
    public static readonly IImmutableSolidColorBrush RuleBrush = new ImmutableSolidColorBrush(Oklch.ToColor(0.90, 0, 0));

    /// <summary>Strong rules under the table head and around the total (L 28%).</summary>
    public static readonly IImmutableSolidColorBrush StrongRuleBrush = new ImmutableSolidColorBrush(Oklch.ToColor(0.28, 0, 0));

    /// <summary>The paper's typeface family: Inter whatever the theme's body font is.</summary>
    public static readonly FontFamily Font = ArcFonts.Body;

    /// <summary>Tabular figures, for every text on the paper.</summary>
    public static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };
}

/// <summary>A letterhead mark: a geometry in a 24 × 24 box, filled in the brand color at the mark's size.</summary>
internal sealed class DocumentPageMark : Control
{
    private readonly Geometry _geometry;
    private readonly IBrush _brush;

    public DocumentPageMark(Geometry geometry, IBrush brush, double size)
    {
        _geometry = geometry;
        _brush = brush;
        Width = size;
        Height = size;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var scale = Bounds.Width / 24;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            context.DrawGeometry(_brush, null, _geometry);
        }
    }
}
