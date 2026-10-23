using System.Globalization;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// <see cref="ShowpieceText"/> draws what <see cref="TextLayout.Draw"/> draws, in the caller's brush (right-to-left text
/// included), and a steady scene of cached labels allocates next to nothing per frame.
/// </summary>
public class ShowpieceTextDrawTests
{
    private const string Hebrew = "שלום עולם";
    private const string Arabic = "مرحبا بالعالم";
    private const string Mixed = "Team שלום 42 עולם end";
    private const string RtlFirst = "שלום team עולם";

    private static readonly Typeface s_medium = new(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);

    [AvaloniaFact]
    public void Glyph_Runs_Are_The_Ones_TextLayout_Draw_Draws()
    {
        var cache = new ShowpieceTextCache();
        var texts = new[]
        {
            cache.Get("Platform", Typeface.Default, 13), cache.Get("周一 · 平台团队", s_medium, 11),
            cache.GetTrimmed("Emma Collins · 4 projects · 18 open tasks", Typeface.Default, 12, 90),
            cache.GetClamped("Emma Collins · 4 projects · 18 open tasks across the depot charging bays", s_medium, 13, 140, 2),
            cache.GetWrapped("Emma Collins · 4 projects · 18 open tasks", s_medium, 13, 140, 5),
            cache.Get(Hebrew, Typeface.Default, 13), cache.Get(Arabic, Typeface.Default, 13), cache.Get(Mixed, Typeface.Default, 13),
            cache.Get(RtlFirst, Typeface.Default, 13), cache.GetTrimmed(Mixed, Typeface.Default, 13, 70)
        };
        var origin = new Point(12.25, 40.5);
        foreach (var text in texts)
        {
            // The layout was built with a black brush: drawn in black, the two paths must record the same calls.
            var expected = ShowpieceTextRecording.Record(dc => text.Layout.Draw(dc, origin));
            var actual = ShowpieceTextRecording.Record(dc => text.Draw(dc, Brushes.Black, origin));
            expected.ShouldNotBeEmpty();
            actual.Count.ShouldBe(expected.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                actual[i].Run.ShouldBeSameAs(expected[i].Run);
                actual[i].Transform.ShouldBe(expected[i].Transform);
                actual[i].Brush.ShouldBeSameAs(expected[i].Brush);
            }
            // In another brush only the brush changes.
            var red = ShowpieceTextRecording.Record(dc => text.Draw(dc, Brushes.Red, origin));
            red.Select(r => r.Transform).ShouldBe(expected.Select(e => e.Transform));
            red.ShouldAllBe(r => ReferenceEquals(r.Brush, Brushes.Red));
        }
    }

    [AvaloniaFact]
    public void Right_To_Left_Runs_Draw_In_Visual_Order()
    {
        // No fallback is needed for right-to-left text: a laid-out line holds its runs in visual order (Avalonia reorders
        // them when it finishes the line) and every glyph run carries its bidi level, so drawing the runs left to right
        // matches TextLayout.Draw and the FormattedText the scenes used before.
        var cache = new ShowpieceTextCache();
        foreach (var source in new[] { Hebrew, Arabic, Mixed, RtlFirst })
        {
            var text = cache.Get(source, Typeface.Default, 13);
            var runs = ShowpieceTextRecording.Record(dc => text.Draw(dc, Brushes.Black, default));
            runs.ShouldContain(r => r.Run.BiDiLevel % 2 == 1, source);
            ShowpieceTextRecording.Glyphs(dc => text.Draw(dc, Brushes.Black, default))
                .ShouldBe(ShowpieceTextRecording.Glyphs(dc => dc.DrawText(ShowpieceTextCacheTests.Formatted(source, Typeface.Default, 13), default)), source);
        }
        // Mixed text: left-to-right, then the Hebrew word (right to left), then left-to-right again.
        var mixed = ShowpieceTextRecording.Record(dc => cache.Get(Mixed, Typeface.Default, 13).Draw(dc, Brushes.Black, default));
        mixed.Select(r => r.Run.BiDiLevel % 2).ShouldContain(0);
        mixed.Select(r => r.Run.BiDiLevel % 2).ShouldContain(1);
    }

    [AvaloniaFact]
    public void Draw_At_Baseline_Puts_The_First_Baseline_There()
    {
        var text = ShowpieceText.Lay("Platform", Typeface.Default, 13);
        text.Baseline.ShouldBe(text.Lines[0].Baseline, 1e-9);
        var top = ShowpieceTextRecording.Record(dc => text.Draw(dc, Brushes.Black, new Point(5, 20)));
        var baseline = ShowpieceTextRecording.Record(dc => text.DrawAtBaseline(dc, Brushes.Black, 5, 20 + text.Baseline));
        baseline.Select(r => r.Transform).ShouldBe(top.Select(r => r.Transform));
    }

    [AvaloniaFact]
    public void A_Steady_Frame_Of_60_Fading_Labels_Allocates_Under_16KB()
    {
        var stage = new LabelStage();
        var window = TestHelpers.Show(stage);
        try
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize(820, 620));
            using var context = bitmap.CreateDrawingContext();
            for (var frame = 0; frame < 20; frame++)
            {
                stage.Frame = frame;
                stage.Render(context);
            }
            var misses = stage.Cache.Misses;
            misses.ShouldBeGreaterThanOrEqualTo(LabelStage.Count);
            const int Frames = 60;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var frame = 0; frame < Frames; frame++)
            {
                stage.Frame = frame;
                stage.Render(context);
            }
            var perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)Frames;
            perFrame.ShouldBeLessThan(16 * 1024);
            // Every color and fade level drawn from the same 60 layouts.
            stage.Cache.Misses.ShouldBe(misses);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>60 labels in 4 colors that move and fade every frame, like a scene's axis and node labels.</summary>
    private sealed class LabelStage : ShowpieceStage
    {
        public const int Count = 60;
        private static readonly string[] s_words = ["Platform", "Payments", "Lisbon", "Search", "Onboarding", "Mobile", "Billing", "周一", "平台团队", "Atlas"];
        private static readonly Color[] s_colors = [Colors.Black, Colors.SlateGray, Colors.SteelBlue, Colors.Tomato];
        private readonly string[] _labels = new string[Count];

        public LabelStage()
        {
            Width = 820;
            Height = 620;
            for (var i = 0; i < Count; i++)
            {
                _labels[i] = s_words[i % s_words.Length] + " " + (i * 7 % 41 + 3).ToString(CultureInfo.InvariantCulture);
            }
        }

        public int Frame { get; set; }

        public ShowpieceTextCache Cache => TextCache;

        protected override void RenderStage(DrawingContext context)
        {
            for (var i = 0; i < Count; i++)
            {
                var text = i % 3 == 0 ? TextCache.GetTrimmed(_labels[i], s_medium, 13, 60) : TextCache.Get(_labels[i], Typeface.Default, 13);
                var alpha = 0.25 + 0.75 * ((i + Frame) % 12) / 11.0;
                var x = 20 + i % 4 * 190 + 12 * Math.Sin((Frame + i * 7) / 23.0);
                var y = 20 + i / 4 * 37;
                text.Draw(context, TextCache.Brush(s_colors[(i + Frame / 10) % 4], alpha), new Point(x, y));
            }
        }
    }
}

/// <summary>Records what a drawing does (glyph runs with their transform and brush) through a <see cref="DrawingGroup"/>.</summary>
internal static class ShowpieceTextRecording
{
    public readonly record struct Drawn(Matrix Transform, IBrush? Brush, GlyphRun Run);

    public static List<Drawn> Record(Action<DrawingContext> draw)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            draw(context);
        }
        var drawn = new List<Drawn>();
        Walk(group, Matrix.Identity, drawn);
        return drawn;
    }

    /// <summary>Each glyph run as text (position, size, bidi level, glyph indices and advances) for comparing layouts built apart.</summary>
    public static List<string> Glyphs(Action<DrawingContext> draw) =>
        Record(draw).Select(d =>
            FormattableString.Invariant($"{d.Transform.M31:0.###},{d.Transform.M32:0.###} {d.Run.FontRenderingEmSize} L{d.Run.BiDiLevel} ") +
            string.Join(' ', d.Run.GlyphInfos.Select(g => FormattableString.Invariant($"{g.GlyphIndex}:{g.GlyphAdvance:0.###}"))))
            .ToList();

    private static void Walk(Drawing drawing, Matrix transform, List<Drawn> drawn)
    {
        switch (drawing)
        {
            case DrawingGroup group:
                var inner = group.Transform is { } t ? t.Value * transform : transform;
                foreach (var child in group.Children)
                {
                    Walk(child, inner, drawn);
                }
                break;
            case GlyphRunDrawing glyphs when glyphs.GlyphRun is not null:
                drawn.Add(new Drawn(transform, glyphs.Foreground, glyphs.GlyphRun));
                break;
        }
    }
}
