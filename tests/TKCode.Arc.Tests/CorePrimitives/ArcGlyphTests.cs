using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>
/// ArcGlyph (and LucideIcon, which derives from it) strokes with its inherited Foreground. Where nothing above it sets one
/// it used to draw nothing at all (#192); it now takes the theme's text color and follows theme changes.
/// </summary>
public class ArcGlyphTests
{
    /// <summary>The pen brushes of what <paramref name="glyph"/> draws, recorded through a <see cref="DrawingGroup"/>.</summary>
    private static List<IBrush?> Strokes(ArcGlyph glyph)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            glyph.Render(context);
        }
        var strokes = new List<IBrush?>();
        Walk(group, strokes);
        return strokes;
    }

    private static void Walk(Drawing drawing, List<IBrush?> strokes)
    {
        if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children)
            {
                Walk(child, strokes);
            }
        }
        else if (drawing is GeometryDrawing { Pen: { } pen })
        {
            strokes.Add(pen.Brush);
        }
    }

    private static Color StrokeColor(ArcGlyph glyph) => TestHelpers.ColorOf(Strokes(glyph).ShouldHaveSingleItem());

    [AvaloniaFact]
    public async Task A_Glyph_With_No_Foreground_Above_It_Draws_In_The_Theme_Text_Color()
    {
        // A host whose text color resolves to nothing, like a bare overlay or a custom popup.
        var glyph = new ArcGlyph { Data = ArcIcons.X, Size = 16 };
        var host = new Border { Child = glyph };
        TextElement.SetForeground(host, null);
        var window = TestHelpers.Show(host);
        try
        {
            glyph.Foreground.ShouldBeNull();
            var light = ArcTokens.GetColor(glyph, ArcDesignTokenKind.Foreground);
            light.A.ShouldBe((byte)255);
            StrokeColor(glyph).ShouldBe(light);

            // It follows the theme: on a dark theme the old black stroke was as invisible as no stroke at all.
            await TestHelpers.ApplyAsync(TestHelpers.Dark);
            var dark = ArcTokens.GetColor(glyph, ArcDesignTokenKind.Foreground);
            dark.ShouldNotBe(light);
            StrokeColor(glyph).ShouldBe(dark);
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Explicit_Or_Inherited_Foreground_Still_Wins()
    {
        var glyph = new ArcGlyph { Data = ArcIcons.X, Size = 16 };
        var host = new Border { Child = glyph };
        TextElement.SetForeground(host, Brushes.Blue);
        var window = TestHelpers.Show(host);
        try
        {
            StrokeColor(glyph).ShouldBe(Colors.Blue);

            glyph.Foreground = Brushes.Red;
            StrokeColor(glyph).ShouldBe(Colors.Red);

            // Back to nothing above it: the theme's color; then an inherited one again.
            glyph.ClearValue(ArcGlyph.ForegroundProperty);
            TextElement.SetForeground(host, null);
            Dispatcher.UIThread.RunJobs();
            StrokeColor(glyph).ShouldBe(ArcTokens.GetColor(glyph, ArcDesignTokenKind.Foreground));
            TextElement.SetForeground(host, Brushes.Green);
            StrokeColor(glyph).ShouldBe(Colors.Green);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Glyph_Outside_Any_Tree_Still_Draws()
    {
        // Nothing above it sets a Foreground: null, not TextElement's black (which vanished on a dark theme). With no theme
        // in reach at all it draws with the last-resort brush, never with nothing.
        var glyph = new ArcGlyph { Data = ArcIcons.X, Size = 16 };
        glyph.Foreground.ShouldBeNull();
        new TKCode.Arc.Icons.Lucide.LucideIcon().Foreground.ShouldBeNull();
        StrokeColor(glyph).A.ShouldBe((byte)255);
    }
}
