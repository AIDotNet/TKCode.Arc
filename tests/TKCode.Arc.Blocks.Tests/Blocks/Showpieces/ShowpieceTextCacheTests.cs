using System.Globalization;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary><see cref="ShowpieceTextCache"/> and <see cref="ShowpieceIcons"/>.</summary>
public class ShowpieceTextCacheTests
{
    private const string Long = "Emma Collins · 4 projects · 18 open tasks across the depot charging bays";

    [Fact]
    public void Font_Sizes_And_Alphas_Are_Quantized()
    {
        ShowpieceTextCache.QuantizeFontSize(13.1).ShouldBe(13);
        ShowpieceTextCache.QuantizeFontSize(13.24).ShouldBe(13);
        ShowpieceTextCache.QuantizeFontSize(13.26).ShouldBe(13.5);
        ShowpieceTextCache.QuantizeFontSize(0.1).ShouldBe(0.5);
        ShowpieceTextCache.AlphaLevel(0).ShouldBe(0);
        ShowpieceTextCache.AlphaLevel(1).ShouldBe(15);
        ShowpieceTextCache.AlphaLevel(0.5).ShouldBe(8);
        ShowpieceTextCache.QuantizeAlpha(0.42).ShouldBe(6 / 15.0, 1e-12);
        // 16 levels in all.
        Enumerable.Range(0, 1001).Select(i => ShowpieceTextCache.AlphaLevel(i / 1000.0)).Distinct().Count().ShouldBe(16);
    }

    [AvaloniaFact]
    public void Texts_And_Brushes_Are_Reused()
    {
        var cache = new ShowpieceTextCache();
        var a = cache.Get("Platform", Typeface.Default, 13.1);
        cache.Get("Platform", Typeface.Default, 13.2).ShouldBeSameAs(a);
        cache.Get("Platform", Typeface.Default, 13.3).ShouldNotBeSameAs(a);
        cache.Get("Platform", new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium), 13.1).ShouldNotBeSameAs(a);
        cache.Misses.ShouldBe(3);
        var brush = cache.Brush(Colors.Black, 0.5);
        cache.Brush(Colors.Black, 0.51).ShouldBeSameAs(brush);
        brush.Color.A.ShouldBe((byte)Math.Round(255 * 8 / 15.0));
        cache.Brush(Colors.Black, 0.7).ShouldNotBeSameAs(brush);
    }

    [AvaloniaFact]
    public void A_Text_That_Fits_Is_One_Layout_At_Every_Width()
    {
        // Captions on cards that grow and shrink ask for a new width every frame; while the text fits, every width
        // returns the plain line (FormattedText drew the same line: nothing to cut or wrap).
        var cache = new ShowpieceTextCache();
        var plain = cache.Get("Platform", Typeface.Default, 13);
        for (var width = Math.Ceiling(plain.Width); width < 400; width += 0.7)
        {
            cache.GetTrimmed("Platform", Typeface.Default, 13, width).ShouldBeSameAs(plain);
            cache.GetWrapped("Platform", Typeface.Default, 13, width, 2).ShouldBeSameAs(plain);
            cache.GetClamped("Platform", Typeface.Default, 13, width, 2).ShouldBeSameAs(plain);
        }
        cache.Misses.ShouldBe(1);
        cache.Count.ShouldBe(1);
        // Narrower than the text: cut.
        var cut = cache.GetTrimmed("Platform", Typeface.Default, 13, plain.Width - 4);
        cut.ShouldNotBeSameAs(plain);
        cut.Lines[0].HasCollapsed.ShouldBeTrue();
        // A text with a line break is never the plain layout (it would be two lines).
        var twoLines = cache.Get("Plat\nform", Typeface.Default, 13);
        twoLines.Lines.Count.ShouldBe(2);
        cache.GetTrimmed("Plat\nform", Typeface.Default, 13, 400).ShouldNotBeSameAs(twoLines);
    }

    [AvaloniaFact]
    public void One_Text_Serves_Every_Color_And_Fade()
    {
        // Colors are not part of the key: the brush comes with each draw, so a label in a new color or fade level is
        // neither laid out again nor cached twice.
        var cache = new ShowpieceTextCache();
        var text = cache.Get("Platform", Typeface.Default, 13);
        foreach (var color in new[] { Colors.Black, Colors.White, Colors.Tomato })
        {
            for (var alpha = 0.0; alpha <= 1; alpha += 0.1)
            {
                cache.Get("Platform", Typeface.Default, 13).ShouldBeSameAs(text);
                var drawn = ShowpieceTextRecording.Record(dc => text.Draw(dc, cache.Brush(color, alpha), default));
                drawn.ShouldNotBeEmpty();
                drawn.ShouldAllBe(g => ReferenceEquals(g.Brush, cache.Brush(color, alpha)));
            }
        }
        cache.Misses.ShouldBe(1);
        cache.Count.ShouldBe(1);
        // 3 colors × 11 fades fall on 3 × 11 quantized brushes (0.0 … 1.0 in tenths hit distinct 1/15 levels).
        cache.BrushCount.ShouldBe(3 * 11);
    }

    [AvaloniaFact]
    public void Long_Text_Is_Cut_To_One_Line_Or_Wrapped_To_Two()
    {
        var cache = new ShowpieceTextCache();
        var line = cache.Get("Platform", Typeface.Default, 13).Height;
        var full = cache.Get(Long, Typeface.Default, 13);
        full.Width.ShouldBeGreaterThan(200);
        full.Lines.Count.ShouldBe(1);

        var trimmed = cache.GetTrimmed(Long, Typeface.Default, 13, 120.7);
        trimmed.Width.ShouldBeLessThanOrEqualTo(120.5);
        trimmed.Height.ShouldBe(line, line * 0.25);
        trimmed.Lines.Count.ShouldBe(1);
        trimmed.Lines[0].HasCollapsed.ShouldBeTrue();
        cache.GetTrimmed(Long, Typeface.Default, 13, 120.2).ShouldBeSameAs(trimmed);

        var wrapped = cache.GetWrapped(Long, Typeface.Default, 13, 150);
        wrapped.Width.ShouldBeLessThanOrEqualTo(150.5);
        wrapped.Lines.Count.ShouldBe(2);
        wrapped.Lines[1].HasCollapsed.ShouldBeTrue();
        wrapped.Height.ShouldBeGreaterThan(line * 1.5);
        wrapped.Height.ShouldBeLessThan(line * 2.5);
        wrapped.Baseline.ShouldBe(full.Baseline, 1e-9);
    }

    [AvaloniaFact]
    public void Sizes_Match_The_FormattedText_The_Cache_Used_To_Return()
    {
        // The cases the blocks draw: plain labels (terrain axes and pills, LayoutMorph values, SemanticZoom labels),
        // one line cut at a width (captions), and text wrapped to two or three lines that fits them (cover titles).
        var cache = new ShowpieceTextCache();
        var medium = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);
        foreach (var (text, typeface, size) in new[]
                 {
                     ("Mon", Typeface.Default, 11.0), ("12a", medium, 11), ("$2.51M", Typeface.Default, 16), (Long, medium, 13),
                     ("周一", Typeface.Default, 11), ("平台团队 · 4 个项目", medium, 13), ("Ends with a space ", Typeface.Default, 12)
                 })
        {
            var formatted = Formatted(text, typeface, size);
            var laid = cache.Get(text, typeface, size);
            laid.Width.ShouldBe(formatted.Width, 1e-6, text);
            laid.WidthIncludingTrailingWhitespace.ShouldBe(formatted.WidthIncludingTrailingWhitespace, 1e-6, text);
            laid.Height.ShouldBe(formatted.Height, 1e-6, text);
            laid.Baseline.ShouldBe(formatted.Baseline, 1e-6, text);
            foreach (var width in new[] { 40.0, 96, 150, 1000 })
            {
                var cut = Formatted(text, typeface, size, (int)width, 1);
                var trimmed = cache.GetTrimmed(text, typeface, size, width);
                trimmed.Width.ShouldBe(cut.Width, 1e-6, $"{text} @ {width}");
                trimmed.WidthIncludingTrailingWhitespace.ShouldBe(cut.WidthIncludingTrailingWhitespace, 1e-6, $"{text} @ {width}");
                trimmed.Height.ShouldBe(cut.Height, 1e-6, $"{text} @ {width}");
                // Same glyphs, the ellipsis included.
                ShowpieceTextRecording.Glyphs(dc => trimmed.Draw(dc, Brushes.Black, default))
                    .ShouldBe(ShowpieceTextRecording.Glyphs(dc => dc.DrawText(cut, default)), $"{text} @ {width}");
            }
        }
        foreach (var width in new[] { 150.0, 300 })
        {
            // Wrapped within its lines: the same breaks, widths and height as before.
            var lines = cache.GetWrapped(Long, Typeface.Default, 13, width, 20).Lines.Count;
            lines.ShouldBeGreaterThan(1);
            var wrapped = cache.GetWrapped(Long, Typeface.Default, 13, width, lines);
            wrapped.Lines[^1].HasCollapsed.ShouldBeFalse();
            var before = Formatted(Long, Typeface.Default, 13, (int)width, lines);
            wrapped.Width.ShouldBe(before.Width, 1e-6);
            wrapped.Height.ShouldBe(before.Height, 1e-6);
            ShowpieceTextRecording.Glyphs(dc => wrapped.Draw(dc, Brushes.Black, default))
                .ShouldBe(ShowpieceTextRecording.Glyphs(dc => dc.DrawText(before, default)));
        }
    }

    [AvaloniaFact]
    public void Text_Left_Over_Fills_The_Last_Line_Up_To_The_Width_As_Before()
    {
        // More text than lines: the last line is laid out again from its start without wrapping and cut at the width,
        // glyph for glyph the FormattedText the cache used to keep (cover titles).
        var cache = new ShowpieceTextCache();
        foreach (var (width, lines) in new[] { (150, 2), (110, 2), (220, 3), (150, 1) })
        {
            var full = cache.GetWrapped(Long, Typeface.Default, 13, width, 20);
            full.Lines.Count.ShouldBeGreaterThan(lines);
            var wrapped = cache.GetWrapped(Long, Typeface.Default, 13, width, lines);
            var before = Formatted(Long, Typeface.Default, 13, width, lines);
            wrapped.Lines.Count.ShouldBe(lines);
            wrapped.Lines[^1].HasCollapsed.ShouldBeTrue();
            for (var i = 0; i < lines - 1; i++)
            {
                wrapped.Lines[i].Length.ShouldBe(full.Lines[i].Length);
            }
            wrapped.Width.ShouldBe(before.Width, 1e-6);
            wrapped.WidthIncludingTrailingWhitespace.ShouldBe(before.WidthIncludingTrailingWhitespace, 1e-6);
            wrapped.Height.ShouldBe(before.Height, 1e-6);
            wrapped.Baseline.ShouldBe(before.Baseline, 1e-6);
            ShowpieceTextRecording.Glyphs(dc => wrapped.Draw(dc, Brushes.Black, new Point(3.5, 7)))
                .ShouldBe(ShowpieceTextRecording.Glyphs(dc => dc.DrawText(before, new Point(3.5, 7))), $"{width} × {lines}");
        }
        // A word wider than the line is cut on its own line, as FormattedText did.
        const string Word = "Supercalifragilisticexpialidocious shelf";
        var cut = cache.GetWrapped(Word, Typeface.Default, 13, 60, 2);
        ShowpieceTextRecording.Glyphs(dc => cut.Draw(dc, Brushes.Black, default))
            .ShouldBe(ShowpieceTextRecording.Glyphs(dc => dc.DrawText(Formatted(Word, Typeface.Default, 13, 60, 2), default)));
    }

    [AvaloniaFact]
    public void Clamped_Text_Keeps_Its_Last_Break_And_Ends_In_An_Ellipsis()
    {
        // SemanticZoom's wrapping: the last allowed line keeps the break it had and ends in an ellipsis (a line clamp),
        // exactly the TextLayout its private cache built.
        var cache = new ShowpieceTextCache();
        var full = cache.GetWrapped(Long, Typeface.Default, 13, 150, 20);
        var clamped = cache.GetClamped(Long, Typeface.Default, 13, 150, 2);
        cache.GetWrapped(Long, Typeface.Default, 13, 150, 2).ShouldNotBeSameAs(clamped);
        clamped.Lines.Count.ShouldBe(2);
        clamped.Lines[0].Length.ShouldBe(full.Lines[0].Length);
        clamped.Lines[1].Length.ShouldBe(full.Lines[1].Length);
        clamped.Lines[1].HasCollapsed.ShouldBeTrue();
        var before = new TextLayout(Long, Typeface.Default, 13, Brushes.Black, TextAlignment.Left, TextWrapping.Wrap,
            TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, 150, double.PositiveInfinity, double.NaN, 0, 2);
        clamped.Height.ShouldBe(before.Height, 1e-9);
        clamped.WidthIncludingTrailingWhitespace.ShouldBe(before.WidthIncludingTrailingWhitespace, 1e-9);
        ShowpieceTextRecording.Glyphs(dc => clamped.Draw(dc, Brushes.Black, default))
            .ShouldBe(ShowpieceTextRecording.Glyphs(dc => before.Draw(dc, default)));
    }

    [AvaloniaFact]
    public void Clear_And_Capacity_Bound_The_Cache()
    {
        var cache = new ShowpieceTextCache(capacity: 16);
        var first = cache.Get("Label 0", Typeface.Default, 12);
        for (var i = 1; i < 16; i++)
        {
            cache.Get("Label " + i, Typeface.Default, 12);
        }
        cache.Count.ShouldBe(16);
        cache.Get("Label 0", Typeface.Default, 12).ShouldBeSameAs(first);
        // The 17th text starts the cache over; the dropped texts can still be drawn (a caller may hold one this frame).
        var next = cache.Get("Label 16", Typeface.Default, 12);
        cache.Count.ShouldBe(1);
        cache.Misses.ShouldBe(17);
        ShowpieceTextRecording.Record(dc => first.Draw(dc, Brushes.Black, default)).ShouldNotBeEmpty();
        cache.Get("Label 0", Typeface.Default, 12).ShouldNotBeSameAs(first);
        cache.Misses.ShouldBe(18);
        for (var i = 0; i < 40; i++)
        {
            cache.Get("Label " + i, Typeface.Default, 12);
            cache.Count.ShouldBeLessThanOrEqualTo(16);
        }
        next.Lines.Count.ShouldBe(1);

        cache.Brush(Colors.Red);
        cache.Clear();
        cache.Count.ShouldBe(0);
        cache.BrushCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Lucide_Glyphs_Fit_The_24px_Box()
    {
        var glyphs = new[]
        {
            ShowpieceIcons.LayoutDashboard, ShowpieceIcons.GalleryHorizontalEnd, ShowpieceIcons.Dna,
            ShowpieceIcons.ArrowDownRight, ShowpieceIcons.Map
        };
        foreach (var glyph in glyphs)
        {
            var bounds = glyph.Bounds;
            bounds.Width.ShouldBeGreaterThan(4);
            bounds.Height.ShouldBeGreaterThan(4);
            bounds.Left.ShouldBeGreaterThanOrEqualTo(0);
            bounds.Top.ShouldBeGreaterThanOrEqualTo(0);
            bounds.Right.ShouldBeLessThanOrEqualTo(24);
            bounds.Bottom.ShouldBeLessThanOrEqualTo(24);
        }
        // Parsed once.
        ShowpieceIcons.Dna.ShouldBeSameAs(ShowpieceIcons.Dna);
        var dashboard = ShowpieceIcons.LayoutDashboard.Bounds;
        dashboard.Left.ShouldBe(3, 1e-6);
        dashboard.Top.ShouldBe(3, 1e-6);
        dashboard.Right.ShouldBe(21, 1e-6);
        dashboard.Bottom.ShouldBe(21, 1e-6);
    }

    /// <summary>The FormattedText the cache used to build (plain, or cut / wrapped at a width with an ellipsis).</summary>
    internal static FormattedText Formatted(string text, Typeface typeface, double size, int maxWidth = -1, int lines = 1)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface,
            ShowpieceTextCache.QuantizeFontSize(size), Brushes.Black);
        if (maxWidth >= 0)
        {
            formatted.MaxTextWidth = maxWidth;
            formatted.MaxLineCount = lines;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }
        return formatted;
    }
}
