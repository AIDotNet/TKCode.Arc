using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Shouldly;

namespace TKCode.Arc.Tests.Video;

/// <summary>VideoCaptionLayer: active set, stacking, insets and sizes (design/components/video/video.md sections 7 and 9).</summary>
public class CaptionsLayerTests
{
    private const double Width = 640;
    private const double Height = 360;

    [AvaloniaFact]
    public void Active_set_changes_only_at_cue_boundaries()
    {
        var (layer, track) = Show(Cue(1, 4, "A"), Cue(2, 3, "B"), Cue(5, 6, "C"));
        var raised = 0;
        layer.ActiveCuesChanged += (_, _) => raised++;

        layer.ActiveCues.ShouldBeEmpty();
        layer.Position = TimeSpan.FromSeconds(1.5);
        Texts(layer).ShouldBe(["A"]);
        raised.ShouldBe(1);
        layer.Position = TimeSpan.FromSeconds(1.9);
        raised.ShouldBe(1);
        layer.Position = TimeSpan.FromSeconds(2);
        Texts(layer).ShouldBe(["A", "B"]);
        layer.Position = TimeSpan.FromSeconds(3);
        Texts(layer).ShouldBe(["A"]);
        layer.Position = TimeSpan.FromSeconds(4);
        layer.ActiveCues.ShouldBeEmpty();
        layer.Position = TimeSpan.FromSeconds(5.5);
        Texts(layer).ShouldBe(["C"]);
        raised.ShouldBe(5);
        layer.Position = TimeSpan.FromSeconds(5.6);
        raised.ShouldBe(5);

        // Seeking back finds cues again; reloading the track updates the set.
        layer.Position = TimeSpan.FromSeconds(2.5);
        Texts(layer).ShouldBe(["A", "B"]);
        track.SetCues([Cue(2, 3, "New")]);
        Texts(layer).ShouldBe(["New"]);
        raised.ShouldBe(7);
    }

    [AvaloniaFact]
    public void Long_cue_is_found_behind_many_short_ones()
    {
        var cues = new List<VideoCue> { Cue(0, 1000, "Long") };
        for (var i = 0; i < 500; i++)
        {
            cues.Add(Cue(i, i + 0.5, $"S{i}"));
        }
        var (layer, _) = Show([.. cues]);
        layer.Position = TimeSpan.FromSeconds(400.7);
        Texts(layer).ShouldBe(["Long"]);
        layer.Position = TimeSpan.FromSeconds(400.2);
        Texts(layer).ShouldBe(["Long", "S400"]);
    }

    [AvaloniaFact]
    public void Blank_cues_are_not_active()
    {
        var (layer, _) = Show(Cue(0, 2, "  "), Cue(0, 2, "Text"));
        layer.Position = TimeSpan.FromSeconds(1);
        Texts(layer).ShouldBe(["Text"]);
    }

    [AvaloniaFact]
    public void Automatic_cues_stack_upward_in_start_order()
    {
        var (layer, _) = Show(Cue(0, 5, "First"), Cue(1, 5, "Second"), Cue(2, 5, "Third"));
        layer.Position = TimeSpan.FromSeconds(3);
        var boxes = layer.CueBoxes;
        boxes.Count.ShouldBe(3);
        var margin = Math.Round(layer.CaptionFontSize * VideoCaptionLayer.BottomMarginRatio);
        boxes[0].Bottom.ShouldBe(Height - margin, 0.01);
        boxes[1].Bottom.ShouldBeLessThanOrEqualTo(boxes[0].Top);
        boxes[2].Bottom.ShouldBeLessThanOrEqualTo(boxes[1].Top);
        (boxes[0].Top - boxes[1].Bottom).ShouldBe(VideoCaptionLayer.StackGap, 0.01);
        foreach (var box in boxes)
        {
            (box.Center.X).ShouldBe(Width / 2, 1);
        }
    }

    [AvaloniaFact]
    public void Bottom_inset_lifts_automatic_and_bottom_anchored_cues_only()
    {
        var (layer, _) = Show(Cue(0, 5, "Auto"), Cue(0, 5, "Top") with { Line = 0 }, Cue(0, 5, "Last row") with { Line = -1, Position = 10, Align = VideoCueAlign.Left, Size = 30 });
        layer.Position = TimeSpan.FromSeconds(1);
        var before = layer.CueBoxes.ToArray();
        layer.BottomInset = 56;
        var after = layer.CueBoxes.ToArray();
        (before[0].Bottom - after[0].Bottom).ShouldBe(56, 0.01);
        after[1].ShouldBe(before[1]);
        (before[2].Bottom - after[2].Bottom).ShouldBe(56, 0.01);
        after[0].Width.ShouldBe(before[0].Width);
    }

    [AvaloniaFact]
    public void Percent_line_puts_the_box_top_there()
    {
        var (layer, _) = Show(Cue(0, 5, "Ten percent") with { Line = 10, LineIsPercent = true });
        layer.Position = TimeSpan.FromSeconds(1);
        layer.CueBoxes.Single().Top.ShouldBe(Height * 0.10, 0.01);
    }

    [AvaloniaFact]
    public void Numbered_lines_snap_to_rows_and_avoid_overlap()
    {
        var (layer, _) = Show(
            Cue(0, 5, "Row zero") with { Line = 0 },
            Cue(0, 5, "Row zero again") with { Line = 0 },
            Cue(0, 5, "Row three") with { Line = 3 },
            Cue(0, 5, "Bottom row") with { Line = -1 });
        layer.Position = TimeSpan.FromSeconds(1);
        var boxes = layer.CueBoxes;
        var row = boxes[0].Height;
        boxes[0].Top.ShouldBe(0);
        // The second "line:0" cue moves down past the first.
        boxes[1].Top.ShouldBe(boxes[0].Bottom + VideoCaptionLayer.StackGap, 0.01);
        boxes[2].Top.ShouldBe(3 * row, 0.01);
        var margin = Math.Round(layer.CaptionFontSize * VideoCaptionLayer.BottomMarginRatio);
        boxes[3].Bottom.ShouldBe(Height - margin, 0.01);
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                boxes[i].Intersects(boxes[j]).ShouldBeFalse($"boxes {i} and {j} overlap");
            }
        }
    }

    [AvaloniaFact]
    public void Position_size_and_align_place_the_box_horizontally()
    {
        var (layer, _) = Show(
            Cue(0, 5, "Left") with { Align = VideoCueAlign.Left, Line = 0 },
            Cue(0, 5, "Right") with { Align = VideoCueAlign.Right, Line = 2 },
            Cue(0, 5, "At 25%") with { Position = 25, Size = 20, Align = VideoCueAlign.Start, Line = 4 });
        layer.Position = TimeSpan.FromSeconds(1);
        var boxes = layer.CueBoxes;
        boxes[0].Left.ShouldBe(0, 0.01);
        boxes[1].Right.ShouldBe(Width, 0.01);
        boxes[2].Left.ShouldBe(Width * 0.25, 0.01);
        boxes[2].Width.ShouldBeLessThanOrEqualTo(Width * 0.20 + 0.01);

        VideoCaptionLayer.Region(Cue(0, 1, "x") with { Position = 80, Size = 100 }, 100).ShouldBe((60d, 40d));
        VideoCaptionLayer.Region(Cue(0, 1, "x") with { Position = 30, Size = 100, Align = VideoCueAlign.End }, 100).ShouldBe((0d, 30d));
        VideoCaptionLayer.Region(Cue(0, 1, "x") with { Position = 30, Size = 100, Align = VideoCueAlign.Start }, 100, rightToLeft: true).ShouldBe((0d, 30d));
        VideoCaptionLayer.Region(Cue(0, 1, "x") with { Align = VideoCueAlign.Start }, 100).ShouldBe((0d, 100d));
        VideoCaptionLayer.Region(Cue(0, 1, "x") with { Align = VideoCueAlign.End, Size = 40 }, 100).ShouldBe((60d, 40d));
        VideoCaptionLayer.Region(Cue(0, 1, "x") with { Align = VideoCueAlign.Start, Size = 40 }, 100, rightToLeft: true).ShouldBe((60d, 40d));
        VideoCaptionLayer.Region(Cue(0, 1, "x") with { Size = 40 }, 100).ShouldBe((30d, 40d));
    }

    [AvaloniaFact]
    public void Wrapped_text_stays_inside_the_picture()
    {
        var (layer, _) = Show(Cue(0, 5, string.Join(' ', Enumerable.Repeat("caption words that wrap", 12))));
        layer.Position = TimeSpan.FromSeconds(1);
        var box = layer.CueBoxes.Single();
        box.Left.ShouldBeGreaterThanOrEqualTo(0);
        box.Right.ShouldBeLessThanOrEqualTo(Width + 0.01);
        box.Height.ShouldBeGreaterThan(layer.CaptionFontSize * 2);
    }

    [AvaloniaFact]
    public void Font_size_follows_the_picture_height_and_scale_within_limits()
    {
        VideoCaptionLayer.FontSizeFor(400, 1).ShouldBe(18, 0.001);
        VideoCaptionLayer.FontSizeFor(400, 2).ShouldBe(36, 0.001);
        VideoCaptionLayer.FontSizeFor(400, 0.75).ShouldBe(13.5, 0.001);
        VideoCaptionLayer.FontSizeFor(100, 1).ShouldBe(12);
        VideoCaptionLayer.FontSizeFor(2160, 1).ShouldBe(40);
        VideoCaptionLayer.FontSizeFor(720, 2).ShouldBe(40);
        VideoCaptionLayer.FontSizeFor(400, double.NaN).ShouldBe(18, 0.001);

        var (layer, _) = Show(Cue(0, 5, "Scaled"));
        layer.Position = TimeSpan.FromSeconds(1);
        var small = layer.CueBoxes.Single();
        layer.CaptionScale = 2;
        layer.CaptionFontSize.ShouldBe(32.4, 0.001);
        var large = layer.CueBoxes.Single();
        large.Height.ShouldBeGreaterThan(small.Height * 1.6);
    }

    [AvaloniaFact]
    public void Chapters_and_descriptions_draw_nothing()
    {
        var (layer, track) = Show(Cue(0, 5, "Something"));
        layer.Position = TimeSpan.FromSeconds(1);
        layer.CueBoxes.Count.ShouldBe(1);

        track.Kind = VideoTextTrackKind.Descriptions;
        Texts(layer).ShouldBe(["Something"]);
        layer.CueBoxes.ShouldBeEmpty();

        track.Kind = VideoTextTrackKind.Chapters;
        layer.ActiveCues.ShouldBeEmpty();
        layer.CueBoxes.ShouldBeEmpty();

        track.Kind = VideoTextTrackKind.Subtitles;
        layer.CueBoxes.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Text_track_loads_and_the_layer_follows()
    {
        var track = new VideoTextTrack { Text = "WEBVTT\n\n00:01.000 --> 00:02.000\n<v Ann>Hi <b>there</b>" };
        var layer = new VideoCaptionLayer { Track = track, Width = Width, Height = Height };
        TestHelpers.Show(layer);
        layer.Position = TimeSpan.FromSeconds(1.5);
        layer.ActiveCues.ShouldBeEmpty();
        track.LoadAsync().IsCompleted.ShouldBeTrue();
        Texts(layer).ShouldBe(["Hi there"]);
        layer.CueBoxes.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Detached_layer_lets_go_of_the_track()
    {
        var track = new VideoTextTrack();
        track.SetCues([Cue(0, 5, "A")]);
        var weak = TestHelpers.ShowAndClose(() => new VideoCaptionLayer { Track = track, Position = TimeSpan.FromSeconds(1) });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        GC.KeepAlive(track);
    }

    [Fact]
    public void Base_direction_comes_from_the_first_strong_character()
    {
        CaptionText.IsRightToLeft("مرحبا بالعالم").ShouldBeTrue();
        CaptionText.IsRightToLeft("123 - שלום").ShouldBeTrue();
        CaptionText.IsRightToLeft("(Hello) مرحبا").ShouldBeFalse();
        CaptionText.IsRightToLeft("‏abc").ShouldBeTrue();
        CaptionText.IsRightToLeft("你好").ShouldBeFalse();
        CaptionText.IsRightToLeft("...").ShouldBeFalse();
    }

    private static (VideoCaptionLayer Layer, VideoTextTrack Track) Show(params VideoCue[] cues)
    {
        var track = new VideoTextTrack();
        track.SetCues(cues);
        var layer = new VideoCaptionLayer
        {
            Track = track,
            Width = Width,
            Height = Height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        TestHelpers.Show(layer);
        layer.Bounds.Size.ShouldBe(new Size(Width, Height));
        return (layer, track);
    }

    private static VideoCue Cue(double start, double end, string text) => CaptionsParserTests.Cue(start, end, text);

    private static string[] Texts(VideoCaptionLayer layer) => layer.ActiveCues.Select(static c => c.Text).ToArray();
}
