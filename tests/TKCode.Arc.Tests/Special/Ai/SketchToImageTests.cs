using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary><see cref="SketchToImage"/>: the timeline, the prepared scene, the player, stages, events, keyboard and motion.</summary>
public class SketchToImageTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static Window Open(SketchToImage card, ArcMotionLevel level = ArcMotionLevel.Off, double width = 560)
    {
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 700;
        Pump();
        return window;
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Run(ManualFrameClock clock, double milliseconds)
    {
        var frames = (int)Math.Round(milliseconds / Frame.TotalMilliseconds);
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(Frame, Frame);
            Pump();
        }
    }

    // ---- The timeline (pure) ----

    [Fact]
    public void Stages_End_At_38_62_88_And_100_Percent()
    {
        SketchToImageTimeline.EndOf(SketchToImageStatus.Idle).ShouldBe(0);
        SketchToImageTimeline.EndOf(SketchToImageStatus.Sketching).ShouldBe(0.38);
        SketchToImageTimeline.EndOf(SketchToImageStatus.Blocking).ShouldBe(0.62);
        SketchToImageTimeline.EndOf(SketchToImageStatus.Refining).ShouldBe(0.88);
        SketchToImageTimeline.EndOf(SketchToImageStatus.Done).ShouldBe(1);
    }

    [Fact]
    public void A_Stage_Owns_The_End_Of_Its_Span()
    {
        SketchToImageTimeline.StageAt(0, heading: false).ShouldBe(SketchToImageStatus.Idle);
        SketchToImageTimeline.StageAt(0, heading: true).ShouldBe(SketchToImageStatus.Sketching);
        SketchToImageTimeline.StageAt(0.2, false).ShouldBe(SketchToImageStatus.Sketching);
        SketchToImageTimeline.StageAt(0.38, false).ShouldBe(SketchToImageStatus.Sketching);
        SketchToImageTimeline.StageAt(0.39, false).ShouldBe(SketchToImageStatus.Blocking);
        SketchToImageTimeline.StageAt(0.62, false).ShouldBe(SketchToImageStatus.Blocking);
        SketchToImageTimeline.StageAt(0.88, false).ShouldBe(SketchToImageStatus.Refining);
        SketchToImageTimeline.StageAt(0.95, false).ShouldBe(SketchToImageStatus.Refining);
        SketchToImageTimeline.StageAt(1, false).ShouldBe(SketchToImageStatus.Done);
    }

    [Fact]
    public void Layers_Dim_The_Pencil_Then_Bring_In_Soft_Passes_Grain_Light_And_The_Photo()
    {
        var sketch = SketchToImageTimeline.LayersAt(0.3);
        sketch.Lines.ShouldBe(1);
        sketch.SoftPass.ShouldBe(0);
        sketch.Photo.ShouldBe(0);

        var blocking = SketchToImageTimeline.LayersAt(0.6);
        blocking.Lines.ShouldBe(0.38, 0.001);
        blocking.SoftPass.ShouldBe(0);

        var refined = SketchToImageTimeline.LayersAt(0.88);
        refined.Lines.ShouldBe(0);
        refined.SoftPass.ShouldBe(1);
        refined.DetailPass.ShouldBe(1);
        refined.Grain.ShouldBe(0.55, 0.001);
        refined.Light.ShouldBe(1);
        refined.Photo.ShouldBe(0);

        SketchToImageTimeline.LayersAt(1).Photo.ShouldBe(1);
    }

    // ---- The prepared scene (pure) ----

    [Fact]
    public void The_Alpine_Scene_Has_About_Twenty_Strokes_Timed_By_Length()
    {
        var prepared = SketchToImagePrepared.From(SketchToImageTestScene.Alpine);
        prepared.Strokes.Count.ShouldBeInRange(17, 24);
        prepared.Regions.Count.ShouldBe(7);
        prepared.Strokes[0].TravelStart.ShouldBe(0);
        prepared.Strokes[0].Start.ShouldBe(0);
        prepared.Strokes[^1].End.ShouldBe(SketchToImagePrepared.StrokesShare, 1e-9);
        prepared.Regions[^1].End.ShouldBe(SketchToImagePrepared.SweepsShare, 1e-9);
        for (var i = 1; i < prepared.Strokes.Count; i++)
        {
            var stroke = prepared.Strokes[i];
            stroke.TravelStart.ShouldBe(prepared.Strokes[i - 1].End, 1e-9);
            stroke.Start.ShouldBeGreaterThan(stroke.TravelStart);
        }
        // A long ridgeline gets more time than a short fold line: time follows length.
        var ridge = prepared.Strokes[0];
        var fold = prepared.Strokes[5];
        (ridge.End - ridge.Start).ShouldBeGreaterThan((fold.End - fold.Start) * 5);
        var perUnitRidge = (ridge.End - ridge.Start) / ridge.Path.Length;
        var perUnitFold = (fold.End - fold.Start) / fold.Path.Length;
        perUnitRidge.ShouldBe(perUnitFold, 1e-9);
    }

    [Fact]
    public void The_Brush_Sweeps_A_Rectangle_Back_And_Forth_Wider_Than_A_Band()
    {
        var sweep = SketchToImagePrepared.Sweep(new Rect(0, 100, 600, 100), out var width);
        // 100 high: three bands of 33.3, a brush 1.4 bands wide.
        sweep.Count.ShouldBe(6);
        width.ShouldBe(100.0 / 3 * 1.4, 1e-9);
        sweep.Start.X.ShouldBeLessThan(10);
        sweep.PointAt(596).X.ShouldBeGreaterThan(590);
        sweep.End.X.ShouldBeGreaterThan(590);
    }

    [Fact]
    public void A_Path_Finds_Points_Along_Its_Length()
    {
        var path = new SketchToImagePath([new Point(0, 0), new Point(10, 0), new Point(10, 10)]);
        path.Length.ShouldBe(20);
        path.PointAt(5).ShouldBe(new Point(5, 0));
        path.PointAt(15).ShouldBe(new Point(10, 5));
        path.PointAt(-1).ShouldBe(new Point(0, 0));
        path.PointAt(99).ShouldBe(new Point(10, 10));
    }

    // ---- The player (pure) ----

    [Fact]
    public void A_Controlled_Stage_Plays_To_Its_End_And_Holds()
    {
        var player = new SketchToImagePlayer { Duration = 10 };
        player.Begin(0.38, ArcMotionLevel.Full);
        player.T.ShouldBe(0);
        player.Phase.ShouldBe(SketchToImagePhase.Play);
        for (var i = 0; i < 120; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Full);
        }
        player.T.ShouldBe(0.2, 0.001);
        for (var i = 0; i < 600; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Full);
        }
        player.T.ShouldBe(0.38);
        player.IsMoving.ShouldBeFalse();
    }

    [Fact]
    public void Mounting_With_Done_Shows_The_Photo_At_Once()
    {
        var player = new SketchToImagePlayer();
        player.Begin(1, ArcMotionLevel.Full);
        player.T.ShouldBe(1);
        player.IsMoving.ShouldBeFalse();
    }

    [Fact]
    public void Moving_Back_Fades_To_Paper_In_520ms_Then_Plays_Again()
    {
        var player = new SketchToImagePlayer { Duration = 9 };
        player.Begin(1, ArcMotionLevel.Full);
        player.Retarget(0.38, ArcMotionLevel.Full);
        player.Phase.ShouldBe(SketchToImagePhase.FadeToPaper);
        for (var i = 0; i < 15; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Full);
        }
        player.T.ShouldBe(1);
        player.Fade.ShouldBe(1 - 15.0 / 60 / 0.52, 0.01);
        // The progress line drains with the canvas.
        player.Progress.ShouldBe(player.Fade, 1e-9);
        for (var i = 0; i < 20; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Full);
        }
        player.Phase.ShouldBe(SketchToImagePhase.Play);
        player.Fade.ShouldBe(1);
        player.T.ShouldBeLessThan(0.02);
    }

    [Fact]
    public void Moving_Back_To_Idle_Rests_On_Blank_Paper()
    {
        var player = new SketchToImagePlayer();
        player.Begin(0.62, ArcMotionLevel.Off);
        player.Retarget(0, ArcMotionLevel.Full);
        for (var i = 0; i < 60; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Full);
        }
        player.T.ShouldBe(0);
        player.Fade.ShouldBe(1);
        player.IsMoving.ShouldBeFalse();
    }

    [Fact]
    public void The_Loop_Holds_Two_Seconds_Fades_And_Starts_Again()
    {
        var player = new SketchToImagePlayer { Duration = 1 };
        var completed = 0;
        player.Completed += () => completed++;
        player.Begin(null, ArcMotionLevel.Full);
        for (var i = 0; i < 61; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Full);
        }
        player.T.ShouldBe(1);
        completed.ShouldBe(1);
        player.Phase.ShouldBe(SketchToImagePhase.Hold);
        for (var i = 0; i < 118; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Full);
        }
        player.Phase.ShouldBe(SketchToImagePhase.Hold);
        for (var i = 0; i < 5; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Full);
        }
        player.Phase.ShouldBe(SketchToImagePhase.FadeToPaper);
        for (var i = 0; i < 40; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Full);
        }
        player.Phase.ShouldBe(SketchToImagePhase.Play);
        player.T.ShouldBeLessThan(0.3);
    }

    [Fact]
    public void Without_Looping_The_Photo_Stays()
    {
        var player = new SketchToImagePlayer { Duration = 1, Loops = false };
        player.Begin(null, ArcMotionLevel.Full);
        for (var i = 0; i < 200; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Full);
        }
        player.T.ShouldBe(1);
        player.IsMoving.ShouldBeFalse();
    }

    [Fact]
    public void Reduced_Motion_Fades_Between_End_States_Without_Drawing()
    {
        var player = new SketchToImagePlayer();
        player.Begin(0.38, ArcMotionLevel.Reduced);
        player.T.ShouldBe(0.38);
        player.IsMoving.ShouldBeFalse();
        player.Retarget(0.62, ArcMotionLevel.Reduced);
        player.Phase.ShouldBe(SketchToImagePhase.ReducedOut);
        for (var i = 0; i < 6; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Reduced);
        }
        // Halfway out: the old end state, fading; nothing in between is drawn.
        player.T.ShouldBe(0.38);
        player.Fade.ShouldBeInRange(0.3, 0.5);
        for (var i = 0; i < 6; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Reduced);
        }
        player.T.ShouldBe(0.62);
        for (var i = 0; i < 12; i++)
        {
            player.Step(1.0 / 60, ArcMotionLevel.Reduced);
        }
        player.Fade.ShouldBe(1);
        player.IsMoving.ShouldBeFalse();
    }

    [Fact]
    public void Uncontrolled_Under_Reduced_Motion_Shows_The_Photo()
    {
        var player = new SketchToImagePlayer();
        player.Begin(null, ArcMotionLevel.Reduced);
        player.T.ShouldBe(1);
        player.IsMoving.ShouldBeFalse();
    }

    // ---- The control ----

    [AvaloniaFact]
    public void With_Motion_Off_It_Shows_The_Photo_With_Its_Prompt()
    {
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, Prompt = SketchToImageTestScene.Prompt };
        var window = Open(card);
        try
        {
            card.Stage.ShouldBe(SketchToImageStatus.Done);
            card.Position.ShouldBe(1);
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.PromptPart).Text.ShouldBe("Layered alpine ridges at golden hour, warm haze");
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.PercentPart).Text.ShouldBe("100%");
            card.StageLabel!.Text.ShouldBe("Done");
            card.StageLabel.IsShimmering.ShouldBeFalse();
            card.StageLabel.Glyph.ShouldBe(ArcIcons.Check);
            card.Canvas!.Photo.Opacity.ShouldBe(1);
            AutomationProperties.GetName(card.Canvas).ShouldBe("Layered mountain ridges under a warm evening sky");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Status_Sets_The_Stage_Label_Icon_And_Percent()
    {
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, Status = SketchToImageStatus.Sketching };
        var stages = new List<SketchToImageStatus>();
        card.StageChanged += (_, e) => stages.Add(e.Stage);
        var window = Open(card);
        try
        {
            card.Stage.ShouldBe(SketchToImageStatus.Sketching);
            card.Position.ShouldBe(0.38);
            card.StageLabel!.Text.ShouldBe("Sketching");
            card.StageLabel.Glyph.ShouldBe(ArcIcons.Pencil);
            card.StageLabel.IsShimmering.ShouldBeTrue();
            card.Classes.Contains(SketchToImage.BusyPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.PercentPart).Text.ShouldBe("38%");
            AutomationProperties.GetName(card.Canvas!).ShouldBe("Sketching: Layered mountain ridges under a warm evening sky");

            card.Status = SketchToImageStatus.Blocking;
            card.Stage.ShouldBe(SketchToImageStatus.Blocking);
            card.StageLabel!.Text.ShouldBe("Blocking in color");
            card.StageLabel.Glyph.ShouldBe(SketchToImage.BrushGlyph);
            card.Announcer!.Announcement.ShouldBe("Blocking in color. Simulated preview.");

            card.Status = SketchToImageStatus.Refining;
            card.StageLabel!.Glyph.ShouldBe(ArcIcons.Sparkles);
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.PercentPart).Text.ShouldBe("88%");
            // The first stage is reported when the card is shown, then once per change.
            stages.ShouldBe([SketchToImageStatus.Sketching, SketchToImageStatus.Blocking, SketchToImageStatus.Refining]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Completed_Is_Raised_When_The_Photo_Is_Revealed()
    {
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, Status = SketchToImageStatus.Refining };
        var completed = 0;
        card.Completed += (_, _) => completed++;
        var window = Open(card);
        try
        {
            completed.ShouldBe(0);
            card.Status = SketchToImageStatus.Done;
            completed.ShouldBe(1);
            card.Stage.ShouldBe(SketchToImageStatus.Done);
            card.Classes.Contains(SketchToImage.BusyPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Idle_Is_Blank_Paper()
    {
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, Status = SketchToImageStatus.Idle };
        var window = Open(card);
        try
        {
            card.Stage.ShouldBe(SketchToImageStatus.Idle);
            card.Position.ShouldBe(0);
            card.StageLabel!.Text.ShouldBe("Waiting");
            card.Canvas!.Photo.IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.PercentPart).Text.ShouldBe("0%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Progress_Takes_The_Place_Of_The_Stage_Ends()
    {
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, Progress = 0.5 };
        var window = Open(card);
        try
        {
            card.Position.ShouldBe(0.5);
            card.Stage.ShouldBe(SketchToImageStatus.Blocking);
            card.Progress = 2;
            card.Progress.ShouldBe(1);
            card.Stage.ShouldBe(SketchToImageStatus.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replay_Raises_ReplayRequested_From_The_Keyboard()
    {
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, Status = SketchToImageStatus.Done };
        var replays = 0;
        card.ReplayRequested += (_, _) => replays++;
        var window = Open(card);
        try
        {
            var replay = TestHelpers.Descendant<Avalonia.Controls.Button>(card, SketchToImage.ReplayPart);
            AutomationProperties.GetName(replay).ShouldBe("Replay the preview");
            replay.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            replays.ShouldBe(1);
            card.Replay();
            replays.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replay_And_Prompt_Can_Be_Hidden()
    {
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, ShowsReplay = false, Prompt = "" };
        var window = Open(card);
        try
        {
            TestHelpers.Descendant<Avalonia.Controls.Button>(card, SketchToImage.ReplayPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.PromptPart).IsVisible.ShouldBeFalse();
            card.Prompt = "A long prompt";
            var prompt = TestHelpers.Descendant<TextBlock>(card, SketchToImage.PromptPart);
            prompt.IsVisible.ShouldBeTrue();
            prompt.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
            Tooltip.GetTip(prompt).ShouldBe("A long prompt");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sizes_Cap_The_Width_And_Small_Or_Narrow_Shows_Only_Replays_Icon()
    {
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine };
        var window = Open(card, width: 900);
        try
        {
            card.Bounds.Width.ShouldBe(520, 0.5);
            card.Classes.Contains(SketchToImage.CompactPseudoClass).ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.ReplayTextPart).IsVisible.ShouldBeTrue();
            var canvas = card.Canvas!;
            canvas.Bounds.Height.ShouldBe(canvas.Bounds.Width * 2 / 3, 1);

            card.Size = ControlSize.Large;
            Pump();
            card.Bounds.Width.ShouldBe(720, 0.5);
            canvas.PencilWidth.ShouldBe(1.75);

            card.Size = ControlSize.Small;
            Pump();
            card.Bounds.Width.ShouldBe(340, 0.5);
            card.Classes.Contains(SketchToImage.CompactPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.ReplayTextPart).IsVisible.ShouldBeFalse();

            card.Size = ControlSize.Medium;
            window.Width = 380;
            Pump();
            Pump();
            card.Bounds.Width.ShouldBeLessThan(400);
            card.Classes.Contains(SketchToImage.CompactPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Neutral_Tone_Draws_In_The_Foreground_Color()
    {
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, Status = SketchToImageStatus.Sketching };
        var window = Open(card);
        try
        {
            var accent = TestHelpers.ColorOf(card.Canvas!.InkBrush);
            card.Tone = SketchToImageTone.Neutral;
            Pump();
            var neutral = TestHelpers.ColorOf(card.Canvas.InkBrush);
            neutral.ShouldBe(TestHelpers.ColorOf(card.Foreground));
            TestHelpers.ColorOf(card.StageLabel!.GlyphBrush).ShouldBe(neutral);
            TestHelpers.ColorOf(TestHelpers.Descendant<SketchToImageProgress>(card, SketchToImage.ProgressPart).Foreground).ShouldBe(neutral);
            _ = accent;
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Every_Text()
    {
        var card = new SketchToImage
        {
            Scene = SketchToImageTestScene.Alpine,
            Status = SketchToImageStatus.Sketching,
            Labels = new SketchToImageLabels { Sketching = "Esquisse", Replay = "Rejouer", Percent = p => p + " %" },
        };
        var window = Open(card);
        try
        {
            card.StageLabel!.Text.ShouldBe("Esquisse");
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.ReplayTextPart).Text.ShouldBe("Rejouer");
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.PercentPart).Text.ShouldBe("38 %");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Own_Scene_And_Image_Are_Drawn()
    {
        var scene = new SketchToImageScene
        {
            Alt = "A box",
            Lines = ["M10 10 H100", "M10 20 H100"],
            Regions = [new SketchToImageRegion { Brush = new Rect(0, 0, 600, 400), Shapes = [new SketchToImageShape("M0 0H600V400H0Z", Colors.Teal)] }],
        };
        var card = new SketchToImage { Scene = scene, Status = SketchToImageStatus.Done };
        var window = Open(card);
        try
        {
            card.Canvas!.Prepared!.Strokes.Count.ShouldBe(2);
            card.Canvas.Prepared.Regions.Count.ShouldBe(1);
            AutomationProperties.GetName(card.Canvas).ShouldBe("A box");
            var image = new DrawingImage(new GeometryDrawing { Brush = Brushes.Teal, Geometry = new RectangleGeometry(new Rect(0, 0, 3, 2)) });
            card.Image = image;
            card.Canvas.Image.ShouldBe(image);
            card.Canvas.Photo.Image.ShouldBe(image);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Scene_It_Waits_On_Blank_Paper()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var card = new SketchToImage();
        var stages = new List<SketchToImageStatus>();
        card.StageChanged += (_, e) => stages.Add(e.Stage);
        var window = Open(card, ArcMotionLevel.Full);
        try
        {
            card.IsEmpty.ShouldBeTrue();
            card.Stage.ShouldBe(SketchToImageStatus.Idle);
            card.Position.ShouldBe(0);
            card.Player.IsControlled.ShouldBeTrue();
            card.Player.IsMoving.ShouldBeFalse();
            card.Canvas!.Prepared!.Strokes.Count.ShouldBe(0);
            card.Canvas.Prepared.Regions.Count.ShouldBe(0);
            card.StageLabel!.Text.ShouldBe("Waiting");
            card.StageLabel.IsShimmering.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.PromptPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.PercentPart).Text.ShouldBe("0%");
            AutomationProperties.GetName(card.Canvas).ShouldBe("Waiting");
            card.Announcer!.Announcement.ShouldBeNullOrEmpty();
            Run(clock, 2000);
            card.Stage.ShouldBe(SketchToImageStatus.Idle);
            card.Position.ShouldBe(0);
            card.Replay();
            Run(clock, 1000);
            card.Position.ShouldBe(0);
            stages.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Scene_Arriving_Starts_The_Loop_And_Clearing_It_Fades_Back_To_Paper()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var card = new SketchToImage { Duration = TimeSpan.FromSeconds(1) };
        var window = Open(card, ArcMotionLevel.Full);
        try
        {
            card.Stage.ShouldBe(SketchToImageStatus.Idle);
            card.Scene = SketchToImageTestScene.Alpine;
            card.IsEmpty.ShouldBeFalse();
            card.Player.IsControlled.ShouldBeFalse();
            card.Player.IsMoving.ShouldBeTrue();
            Run(clock, 500);
            card.Stage.ShouldBe(SketchToImageStatus.Blocking);
            AutomationProperties.GetName(card.Canvas!).ShouldBe("Blocking in color: Layered mountain ridges under a warm evening sky");

            // Clearing the scene stops the loop: the canvas fades to blank paper and rests there.
            card.Scene = null;
            card.Player.IsControlled.ShouldBeTrue();
            Run(clock, 700);
            card.Position.ShouldBe(0);
            card.Stage.ShouldBe(SketchToImageStatus.Idle);
            card.Player.IsMoving.ShouldBeFalse();
            Run(clock, 3000);
            card.Position.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Status_Still_Drives_A_Card_With_No_Scene()
    {
        var card = new SketchToImage { Status = SketchToImageStatus.Blocking };
        var window = Open(card);
        try
        {
            card.Stage.ShouldBe(SketchToImageStatus.Blocking);
            card.Position.ShouldBe(0.62);
            TestHelpers.Descendant<TextBlock>(card, SketchToImage.PercentPart).Text.ShouldBe("62%");
            AutomationProperties.GetName(card.Canvas!).ShouldBe("Blocking in color");
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Motion ----

    [AvaloniaFact]
    public void On_Its_Own_It_Paints_From_Blank_Paper_Stage_By_Stage()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, Duration = TimeSpan.FromSeconds(1) };
        var stages = new List<SketchToImageStatus>();
        card.StageChanged += (_, e) => stages.Add(e.Stage);
        var window = Open(card, ArcMotionLevel.Full);
        try
        {
            card.Stage.ShouldBe(SketchToImageStatus.Sketching);
            card.Announcer!.Announcement.ShouldBe(new SketchToImageLabels().LoopingAnnouncement);
            Run(clock, 200);
            card.Position.ShouldBeInRange(0.15, 0.25);
            card.Canvas!.Ink.PenTip.ShouldNotBeNull();
            Run(clock, 300);
            card.Stage.ShouldBe(SketchToImageStatus.Blocking);
            Run(clock, 550);
            card.Stage.ShouldBe(SketchToImageStatus.Done);
            stages.ShouldBe([SketchToImageStatus.Sketching, SketchToImageStatus.Blocking, SketchToImageStatus.Refining, SketchToImageStatus.Done]);
            // Holds the photo for two seconds, fades to paper over 520ms and starts again.
            Run(clock, 1900);
            card.Position.ShouldBe(1);
            Run(clock, 700);
            card.Stage.ShouldBe(SketchToImageStatus.Sketching);
            card.Position.ShouldBeLessThan(0.2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Pen_Lifts_While_It_Travels_Between_Strokes()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, Status = SketchToImageStatus.Sketching, Duration = TimeSpan.FromSeconds(4) };
        var window = Open(card, ArcMotionLevel.Full);
        try
        {
            var prepared = card.Canvas!.Prepared!;
            var travel = prepared.Strokes[1];
            var mid = (travel.TravelStart + travel.Start) / 2 * SketchToImageTimeline.SketchEnd;
            // Drive the canvas straight to the middle of the first travel and draw it.
            card.Canvas.Show(mid, 1);
            Pump();
            var tip = card.Canvas.Ink.PenTip;
            tip.ShouldNotBeNull();
            tip!.Value.Lift.ShouldBeGreaterThan(0.9);
            tip.Value.Alpha.ShouldBeLessThan(0.5);

            var drawing = (travel.Start + travel.End) / 2 * SketchToImageTimeline.SketchEnd;
            card.Canvas.Show(drawing, 1);
            Pump();
            card.Canvas.Ink.PenTip!.Value.Lift.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Controlled_Card_Holds_At_The_Stage_End_And_Stops_Its_Clock()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine, Status = SketchToImageStatus.Sketching, Duration = TimeSpan.FromSeconds(1) };
        var window = Open(card, ArcMotionLevel.Full);
        try
        {
            Run(clock, 600);
            card.Position.ShouldBe(0.38);
            card.Player.IsMoving.ShouldBeFalse();
            card.StageLabel!.IsShimmering.ShouldBeTrue();
            Run(clock, 200);
            card.Position.ShouldBe(0.38);

            card.Status = SketchToImageStatus.Idle;
            Run(clock, 300);
            card.CanvasFade.ShouldBeLessThan(1);
            Run(clock, 400);
            card.Position.ShouldBe(0);
            card.Stage.ShouldBe(SketchToImageStatus.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Starts_On_The_Photo_And_Fades_On_Replay()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var card = new SketchToImage { Scene = SketchToImageTestScene.Alpine };
        var window = Open(card, ArcMotionLevel.Reduced);
        try
        {
            card.Position.ShouldBe(1);
            card.Stage.ShouldBe(SketchToImageStatus.Done);
            card.Replay();
            Run(clock, 100);
            card.CanvasFade.ShouldBeLessThan(1);
            card.Position.ShouldBe(1);
            Run(clock, 400);
            card.CanvasFade.ShouldBe(1);
            card.Player.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>
/// The tests' own invented scene (the one the control used to ship, now demo data in the Gallery): layered alpine ridges
/// at golden hour with about twenty pencil strokes and seven regions, so the stroke timing and the pen's travel can be
/// checked on a realistic drawing.
/// </summary>
internal static class SketchToImageTestScene
{
    /// <summary>The prompt the scene goes with.</summary>
    public const string Prompt = "Layered alpine ridges at golden hour, warm haze";

    private static SketchToImageScene? s_alpine;

    /// <summary>The scene, built on first use.</summary>
    public static SketchToImageScene Alpine => s_alpine ??= Build();

    private static readonly Point s_sun = new(420, 178);

    private static readonly Point[] s_far =
    [
        new(0, 232), new(40, 214), new(78, 222), new(118, 196), new(150, 206), new(190, 180), new(226, 198), new(262, 188),
        new(300, 206), new(336, 192), new(372, 214), new(410, 200), new(452, 216), new(490, 196), new(530, 210),
        new(566, 198), new(600, 212),
    ];

    private static readonly Point[] s_mid =
    [
        new(0, 268), new(52, 240), new(96, 252), new(140, 222), new(176, 238), new(214, 214), new(250, 234), new(292, 246),
        new(330, 226), new(372, 250), new(412, 236), new(456, 258), new(500, 232), new(548, 252), new(600, 238),
    ];

    private static readonly Point[] s_near =
    [
        new(0, 312), new(60, 286), new(104, 298), new(156, 270), new(196, 290), new(244, 278), new(290, 300), new(338, 280),
        new(384, 296), new(430, 274), new(478, 292), new(528, 280), new(600, 300),
    ];

    private const string ForegroundLine = "M0 352 C80 334 150 344 220 340 C300 336 360 326 440 334 C510 341 560 336 600 330";

    private static readonly (double X, double Base, double Height)[] s_pines =
    [
        (64, 350, 64), (98, 346, 44), (476, 340, 34), (520, 338, 58), (548, 336, 40),
    ];

    private static SketchToImageScene Build()
    {
        var sun = Circle(s_sun, 30);
        var lines = new List<string>
        {
            Polyline(s_far, close: false),
            Polyline(s_mid, close: false),
            Polyline(s_near, close: false),
            ForegroundLine,
            sun,
            "M190 180 L204 206",
            "M214 214 L232 246 L226 268",
            "M330 226 L346 256",
            "M156 270 L170 300 L164 322",
            "M430 274 L446 304",
        };
        foreach (var pine in s_pines)
        {
            lines.Add(Pine(pine.X, pine.Base, pine.Height));
        }
        lines.Add("M86 96 C116 86 146 92 172 88 C196 84 220 90 238 88");
        lines.Add("M356 70 C380 62 406 66 430 62");
        lines.Add("M250 120 Q256 114 262 120 Q268 114 274 120");
        lines.Add("M284 104 Q288 100 292 104 Q296 100 300 104");

        var regions = new List<SketchToImageRegion>
        {
            new()
            {
                Brush = new Rect(0, 0, 600, 250),
                Shapes =
                [
                    new("M0 0H600V400H0Z", Rgb(0xEDCBAE), Vertical((0, 0xFFA7B6D3), (0.5, 0xFFE9C3AA), (0.8, 0xFFF7D9B2), (1, 0xFFFBE3BC))),
                    new(Circle(s_sun, 120), null, Radial(0xC8FFEBC0, 0x00FFE6B0)),
                ],
            },
            new()
            {
                Brush = new Rect(384, 142, 72, 72),
                Shapes = [new(sun, Rgb(0xFBE3B0), Radial(0xFFFFF8E4, 0xFFFCD596))],
            },
            new()
            {
                Brush = new Rect(0, 172, 600, 100),
                Shapes = [new(Polyline(s_far, close: true), Rgb(0xD3B1B6), Vertical((0, 0xFFE2BFBF), (1, 0xFFC7A3B1)))],
            },
            new()
            {
                Brush = new Rect(0, 208, 600, 108),
                Shapes =
                [
                    new("M0 200H600V300H0Z", null, Vertical((0, 0x00FFF1E0), (0.6, 0x8CFFF1E0), (1, 0x00FFF1E0))),
                    new(Polyline(s_mid, close: true), Rgb(0xA08AA6), Vertical((0, 0xFFB69AB2), (1, 0xFF85718F))),
                    new(Snow(140, 222, 30.0 / 44, 16.0 / 36), null, Solid(0xD9E6D6E0)),
                    new(Snow(214, 214, 24.0 / 38, 20.0 / 36), null, Solid(0xD9E6D6E0)),
                    new(Snow(330, 226, 20.0 / 38, 24.0 / 42), null, Solid(0xD9E6D6E0)),
                    new(Snow(500, 232, 26.0 / 44, 20.0 / 48), null, Solid(0xD9E6D6E0)),
                ],
            },
            new()
            {
                Brush = new Rect(0, 264, 600, 96),
                Shapes =
                [
                    new("M0 250H600V330H0Z", null, Vertical((0, 0x00FFF1E0), (0.55, 0x60FFF1E0), (1, 0x00FFF1E0))),
                    new(Polyline(s_near, close: true), Rgb(0x675C7A), Vertical((0, 0xFF776A8C), (1, 0xFF4F4562))),
                ],
            },
            new()
            {
                Brush = new Rect(0, 322, 600, 78),
                Shapes = [new(ForegroundLine + " V400 H0 Z", Rgb(0x33464A), Vertical((0, 0xFF3E5557), (1, 0xFF22302F)))],
            },
            new()
            {
                Brush = new Rect(36, 276, 532, 76),
                Shapes = s_pines
                    .Select(p => new SketchToImageShape(Pine(p.X, p.Base, p.Height), Rgb(0x1D2B2A), Vertical((0, 0xFF263836), (1, 0xFF172221))))
                    .ToArray(),
            },
        };

        return new SketchToImageScene
        {
            Alt = "Layered mountain ridges under a warm evening sky",
            Lines = lines,
            Regions = regions,
            Light = s_sun,
        };
    }

    private static Color Rgb(uint rgb) => Color.FromUInt32(0xFF000000 | rgb);

    private static IBrush Solid(uint argb) => new ImmutableSolidColorBrush(Color.FromUInt32(argb));

    private static IBrush Vertical(params (double Offset, uint Argb)[] stops) =>
        new ImmutableLinearGradientBrush(
            stops.Select(s => new ImmutableGradientStop(s.Offset, Color.FromUInt32(s.Argb))).ToArray(),
            startPoint: new RelativePoint(0.5, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(0.5, 1, RelativeUnit.Relative));

    private static IBrush Radial(uint inner, uint outer) =>
        new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromUInt32(inner)), new ImmutableGradientStop(1, Color.FromUInt32(outer))],
            radius: 0.5);

    private static string Polyline(Point[] points, bool close)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < points.Length; i++)
        {
            builder.Append(i == 0 ? "M" : " L").Append(Num(points[i].X)).Append(' ').Append(Num(points[i].Y));
        }
        if (close)
        {
            builder.Append(" V400 H0 Z");
        }
        return builder.ToString();
    }

    private static string Circle(Point center, double radius) =>
        $"M{Num(center.X - radius)} {Num(center.Y)} A{Num(radius)} {Num(radius)} 0 1 0 {Num(center.X + radius)} {Num(center.Y)} " +
        $"A{Num(radius)} {Num(radius)} 0 1 0 {Num(center.X - radius)} {Num(center.Y)} Z";

    /// <summary>A three-tier pine on a short trunk, symmetric about <paramref name="x"/>.</summary>
    private static string Pine(double x, double baseY, double height)
    {
        var top = baseY - height;
        var w = height * 0.38;
        (double Dx, double Y)[] right =
        [
            (w * 0.45, top + height * 0.33), (w * 0.22, top + height * 0.33), (w * 0.72, top + height * 0.64),
            (w * 0.45, top + height * 0.64), (w, baseY - height * 0.1), (w * 0.12, baseY - height * 0.1), (w * 0.12, baseY),
        ];
        var builder = new StringBuilder();
        builder.Append('M').Append(Num(x)).Append(' ').Append(Num(top));
        foreach (var (dx, y) in right)
        {
            builder.Append(" L").Append(Num(x + dx)).Append(' ').Append(Num(y));
        }
        for (var i = right.Length - 1; i >= 0; i--)
        {
            builder.Append(" L").Append(Num(x - right[i].Dx)).Append(' ').Append(Num(right[i].Y));
        }
        return builder.Append(" Z").ToString();
    }

    /// <summary>A snow cap hugging a peak whose slopes fall by <paramref name="left"/> and <paramref name="right"/> per unit.</summary>
    private static string Snow(double x, double y, double left, double right) =>
        $"M{Num(x)} {Num(y)} L{Num(x + 13)} {Num(y + 13 * right)} L{Num(x + 7)} {Num(y + 13 * right + 4)} " +
        $"L{Num(x + 1)} {Num(y + 6)} L{Num(x - 6)} {Num(y + 12 * left + 3)} L{Num(x - 12)} {Num(y + 12 * left)} Z";

    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
