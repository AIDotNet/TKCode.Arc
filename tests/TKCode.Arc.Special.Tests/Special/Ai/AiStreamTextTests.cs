using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The shared streaming text of the AI components and the per-chunk fades it draws with.</summary>
public class AiStreamTextTests
{
    private const string Words = "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen";

    [AvaloniaFact]
    public void Appended_Chunks_Fade_In_At_Full_Motion_Then_Draw_Whole()
    {
        var text = new AiStreamText { Width = 240, FontSize = 14 };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            text.Append("one two ", fade: true);
            text.Append("three four ", fade: true);
            text.Text.ShouldBe("one two three four ");
            text.Revealed.ShouldBe(text.Length);
            text.RevealCount.ShouldBe(2);
            text.FadingCount.ShouldBe(2);
            text.DrawsWhole.ShouldBeFalse();
            TestHelpers.PumpUntil(() => text.FadingCount == 0).ShouldBeTrue();
            text.DrawsWhole.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Without_Full_Motion_Chunks_Show_At_Once(ArcMotionLevel level)
    {
        var text = new AiStreamText { Width = 240 };
        var window = TestHelpers.Show(text, level);
        try
        {
            text.Append("one two ", fade: true);
            text.FadingCount.ShouldBe(0);
            text.DrawsWhole.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Chunk_Shows_At_Once_Until_The_Text_Is_On_Screen()
    {
        var text = new AiStreamText();
        text.Append("not yet shown", fade: true);
        text.FadingCount.ShouldBe(0);
        text.Revealed.ShouldBe(text.Length);
    }

    [AvaloniaFact]
    public void Set_Text_Shows_It_Whole_And_Ends_The_Fades()
    {
        var text = new AiStreamText { Width = 240 };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            text.Append("streamed ", fade: true);
            text.FadingCount.ShouldBe(1);
            text.SetText("a cached result");
            text.FadingCount.ShouldBe(0);
            text.Text.ShouldBe("a cached result");
            text.RevealCount.ShouldBe(0);
            text.DrawsWhole.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reserved_Text_Holds_Its_Final_Size_And_Draws_Only_What_Is_Revealed()
    {
        var text = new AiStreamText { Width = 260, FontSize = 14, LineHeight = 20 };
        var window = TestHelpers.Show(text);
        try
        {
            text.Reserve(Words);
            Dispatcher.UIThread.RunJobs();
            var full = text.Bounds.Height;
            full.ShouldBeGreaterThan(20);
            text.Revealed.ShouldBe(0);
            text.RevealedText.ShouldBeEmpty();

            text.RevealTo(8, fade: false);
            Dispatcher.UIThread.RunJobs();
            text.Bounds.Height.ShouldBe(full);
            text.RevealedText.ShouldBe("one two ");
            text.RevealCount.ShouldBe(1);

            // Only the first line is drawn, and only up to the revealed end.
            var layout = text.CurrentLayout!;
            layout.TextLines.Count.ShouldBeGreaterThan(1);
            layout.TextLines[0].Length.ShouldBeGreaterThan(8);
            var runs = Record(text);
            runs.ShouldNotBeEmpty();
            var end = layout.HitTestTextPosition(8).X;
            runs.ShouldAllBe(r => r.Clip != null && r.Clip.Value.Right <= end + 0.01 && r.Opacity == 1);

            text.RevealAll();
            text.RevealCount.ShouldBe(2);
            text.DrawsWhole.ShouldBeTrue();
            Record(text).ShouldAllBe(r => r.Clip == null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Fading_Chunk_Draws_Its_Own_Glyphs_At_Its_Opacity_Without_A_Cover()
    {
        // A long fade, so a frame in its middle is easy to catch.
        var host = new FadeHost { Width = 300, Height = 60 };
        host.Fades.Duration = TimeSpan.FromSeconds(2);
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            var layout = host.Layout;
            host.Fades.Add(4, 8).ShouldBeTrue();

            // Just born: the chunk is fully transparent (not drawn), the text before it settled, nothing after it.
            var born = RecordFades(host, layout, 8);
            var split = layout.HitTestTextPosition(4).X;
            born.ShouldAllBe(r => r.Opacity == 1);
            born.ShouldAllBe(r => r.Clip!.Value.Right <= split + 0.01);
            born.ShouldNotBeEmpty();

            TestHelpers.PumpUntil(() => host.Fades.Snapshot.Any(f => f.Progress is > 0.2 and < 0.8)).ShouldBeTrue();
            var progress = host.Fades.Snapshot.Single().Progress;
            var mid = RecordFades(host, layout, 8);
            mid.ShouldContain(r => r.Opacity < 0.999 && r.Clip!.Value.Left >= split - 0.01);
            // The text itself fades: no shape is painted over it.
            mid.Count(r => r.Opacity < 0.999).ShouldBeGreaterThan(0);
            mid.Where(r => r.Opacity < 0.999).ShouldAllBe(r => Math.Abs(r.Opacity - progress) < 0.001);
            CountShapes(host, layout, 8).ShouldBe(0);

            TestHelpers.PumpUntil(() => host.Fades.Count == 0, 8000).ShouldBeTrue();
            RecordFades(host, layout, 8).ShouldAllBe(r => r.Opacity == 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fades_Finish_At_Once_When_Motion_Is_Lowered()
    {
        var host = new FadeHost { Width = 300, Height = 60 };
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            host.Fades.Add(0, 5).ShouldBeTrue();
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Dispatcher.UIThread.RunJobs();
            host.Fades.Count.ShouldBe(0);
            host.Fades.Add(0, 5).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Truncate_Drops_Or_Cuts_The_Fades_Past_The_Kept_Text()
    {
        var host = new FadeHost { Width = 300, Height = 60 };
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            host.Fades.Add(0, 4);
            host.Fades.Add(4, 10);
            host.Fades.Truncate(6);
            host.Fades.Snapshot.Select(f => (f.Start, f.End)).ShouldBe([(0, 4), (4, 6)]);
            host.Fades.Overlaps(6, 9).ShouldBeFalse();
            host.Fades.Overlaps(5, 9).ShouldBeTrue();
            host.Fades.Truncate(2);
            host.Fades.Snapshot.Select(f => (f.Start, f.End)).ShouldBe([(0, 2)]);
            host.Fades.Clear();
            host.Fades.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Fade_Keeps_Each_Components_Duration_And_Curve()
    {
        var host = new FadeHost();
        host.Fades.Duration.ShouldBe(TimeSpan.FromMilliseconds(140));
        host.Fades.Easing.ShouldBe(ArcEasings.Standard);
        new PiiRedactionStreamText().FadeDuration.ShouldBe(ArcMotionPresets.Fast);
        new PiiRedactionStreamText().FadeEasing.ShouldBeNull();
        new AiWorkflowStreamText("a b").FadeEasing.ShouldBeNull();
        new AgentHandoffStreamText().FadeEasing.ShouldBeNull();
        new TldrToggleStreamText().FadeEasing.ShouldBe(ArcEasings.Standard);
        new ReplyDraftStreamText().FadeDuration.ShouldBe(TimeSpan.FromMilliseconds(140));
        new MeetingSummaryLineText().FadeDuration.ShouldBe(TimeSpan.FromMilliseconds(140));
    }

    [AvaloniaFact]
    public void Sizing_Options_Fit_The_Text_Hold_A_Line_Or_Collapse()
    {
        var fit = new AiStreamText { SizesToText = true, FontSize = 14 };
        fit.SetText("short");
        fit.Measure(new Size(300, double.PositiveInfinity));
        fit.DesiredSize.Width.ShouldBeLessThan(300);
        fit.DesiredSize.Width.ShouldBe(Math.Ceiling(fit.DesiredSize.Width));

        var fill = new AiStreamText { FontSize = 14 };
        fill.SetText("short");
        fill.Measure(new Size(300, double.PositiveInfinity));
        fill.DesiredSize.Width.ShouldBe(300);

        var held = new AiStreamText { HoldsOneLine = true, FontSize = 10, LineHeight = 30 };
        held.Measure(new Size(300, double.PositiveInfinity));
        held.DesiredSize.Height.ShouldBe(30);

        var collapsed = new AiStreamText { CollapsesWhenEmpty = true, FontSize = 14 };
        collapsed.Measure(new Size(300, double.PositiveInfinity));
        collapsed.DesiredSize.ShouldBe(default);
    }

    [AvaloniaFact]
    public void Workflow_Stream_Reveals_Five_Words_A_Chunk_And_Reduced_Motion_Keeps_The_Pace_Without_Fades()
    {
        var stream = new AiWorkflowStreamText(Words) { Width = 400 };
        stream.ChunkCount.ShouldBe(3);
        stream.Revealed.ShouldBe(stream.Length);
        var window = TestHelpers.Show(stream, ArcMotionLevel.Reduced);
        try
        {
            stream.Play();
            stream.Revealed.ShouldBe(0);
            TestHelpers.PumpUntil(() => stream.RevealedChunks == 3).ShouldBeTrue();
            stream.FadingCount.ShouldBe(0);
            stream.Revealed.ShouldBe(stream.Length);
            TestHelpers.PumpUntil(() => !stream.IsStreaming).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    // --- recording -----------------------------------------------------------------------------------------------------

    private readonly record struct DrawnRun(double Opacity, Rect? Clip);

    private static List<DrawnRun> Record(AiStreamText text)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            text.Render(context);
        }
        return Collect(group);
    }

    private static List<DrawnRun> RecordFades(FadeHost host, TextLayout layout, int revealed)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            host.Fades.Draw(context, layout, default, revealed);
        }
        return Collect(group);
    }

    private static int CountShapes(FadeHost host, TextLayout layout, int revealed)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            host.Fades.Draw(context, layout, default, revealed);
        }
        return Flatten(group).Count(d => d is GeometryDrawing);
    }

    private static List<DrawnRun> Collect(DrawingGroup group)
    {
        var runs = new List<DrawnRun>();
        Walk(group, 1, null, runs);
        return runs;
    }

    private static void Walk(Drawing drawing, double opacity, Rect? clip, List<DrawnRun> runs)
    {
        if (drawing is GlyphRunDrawing)
        {
            runs.Add(new DrawnRun(Math.Round(opacity, 6), clip));
            return;
        }
        if (drawing is DrawingGroup group)
        {
            var inner = opacity * group.Opacity;
            var innerClip = group.ClipGeometry is RectangleGeometry rect ? rect.Rect : clip;
            foreach (var child in group.Children)
            {
                Walk(child, inner, innerClip, runs);
            }
        }
    }

    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children.SelectMany(Flatten))
            {
                yield return child;
            }
        }
    }

    /// <summary>A control that owns a text layout and the fades drawn over it.</summary>
    private sealed class FadeHost : Control
    {
        public FadeHost()
        {
            Fades = new AiTextFades(this, InvalidateVisual);
            Layout = new TextLayout("aaa bbb ccc ddd eee", Typeface.Default, 14, Brushes.Black, maxWidth: 300);
        }

        public AiTextFades Fades { get; }

        public TextLayout Layout { get; }
    }
}
