using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

public class ExplodedViewTests
{
    private static ExplodedView Create(int layers = 5, double width = 745, double height = 860)
    {
        var view = new ExplodedView
        {
            Width = width,
            Height = height,
            Title = "Made of layers",
            Description = "Lift the screen apart.",
            StartCaption = new ExplodedCaption { Title = "A single screen", Body = "Scroll to lift it apart." },
            EndCaption = new ExplodedCaption { Title = "Together again", Body = "One calm screen." }
        };
        for (var i = 0; i < layers; i++)
        {
            view.Layers.Add(new ExplodedLayer
            {
                Name = $"Layer {i}",
                Note = $"Note {i}",
                Body = $"Body {i}",
                Kind = i == 0 ? ExplodedLayerKind.Plate : i == 1 ? ExplodedLayerKind.Grid : ExplodedLayerKind.Sheet,
                Content = new Border { Width = 280, Height = 620 }
            });
        }
        return view;
    }

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off, double width = 1200, double height = 1000)
    {
        var window = TestHelpers.Show(content, level);
        window.Width = width;
        window.Height = height;
        Pump();
        return window;
    }

    private static void Pump(int frames = 3)
    {
        for (var i = 0; i < frames; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void RunFrames(ManualFrameClock clock, int count)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static List<ExplodedViewStopButton> StopButtons(ExplodedView view) =>
        view.Stops.ShouldNotBeNull().Children.OfType<ExplodedViewStopButton>().ToList();

    [AvaloniaFact]
    public void A_Layer_Template_Is_Only_Built_With_The_Layers_Content()
    {
        // A host builds the template it has whenever its content changes; a template attached to empty content is built with null.
        var built = new List<string?>();
        var view = Create(layers: 0);
        for (var i = 0; i < 3; i++)
        {
            view.Layers.Add(new ExplodedLayer
            {
                Name = $"Layer {i}",
                Content = $"Screen {i}",
                ContentTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((text, _) =>
                {
                    built.Add(text);
                    return new Border { Width = 280, Height = 620, Tag = text };
                })
            });
        }
        var window = Show(view);
        try
        {
            view.Layers.RemoveAt(2);
            view.Layers[0].Content = "Changed";
            Pump();
            built.ShouldNotBeEmpty();
            built.ShouldAllBe(text => text != null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Defaults_Match_The_Arc_Preview()
    {
        var view = new ExplodedView();
        view.LayerSize.ShouldBe(new Size(280, 620));
        view.LayerCornerRadius.ShouldBe(40);
        view.ScrollMode.ShouldBe(ExplodedScrollMode.Container);
        view.Length.ShouldBe(4);
        view.Progress.ShouldBe(0);
        view.MaxTilt.ShouldBe(60);
        view.MaxTurn.ShouldBe(-40);
        view.LayerGap.ShouldBe(136);
        view.TallMaxTilt.ShouldBe(62);
        view.TallMaxTurn.ShouldBe(-20);
        view.TallLayerGap.ShouldBe(150);
        view.Perspective.ShouldBe(2400);
        view.DimmedLayerOpacity.ShouldBe(0.5);
        view.IsFramed.ShouldBeTrue();
        view.StopCount.ShouldBe(2);
        view.CurrentStop.ShouldBe(0);
        view.AssembledText.ShouldNotBeNullOrWhiteSpace();
        view.ReassembledText.ShouldNotBeNullOrWhiteSpace();
        view.Progress = 7;
        view.Progress.ShouldBe(1);
        view.Progress = double.NaN;
        view.Progress.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Layers_Build_The_Stage_Stops_Captions_And_The_Text_List()
    {
        var view = Create();
        var window = Show(view);
        try
        {
            var stage = view.Stage.ShouldNotBeNull();
            stage.Hosts.Count.ShouldBe(5);
            stage.Labels.Count.ShouldBe(5);
            stage.Hosts[0].Kind.ShouldBe(ExplodedLayerKind.Plate);
            stage.Hosts[0].Classes.ShouldContain(":plate");
            stage.Hosts[1].Classes.ShouldContain(":grid");
            stage.Hosts[2].Classes.ShouldContain(":sheet");
            stage.Labels[3].LabelName.ShouldBe("Layer 3");
            stage.Labels[3].Note.ShouldBe("Note 3");
            view.StopCount.ShouldBe(7);
            var stops = StopButtons(view);
            stops.Count.ShouldBe(7);
            AutomationProperties.GetName(stops[0]).ShouldBe(view.AssembledText);
            AutomationProperties.GetName(stops[3]).ShouldBe("Layer 2");
            AutomationProperties.GetName(stops[6]).ShouldBe(view.ReassembledText);
            stops[0].IsEnd.ShouldBeTrue();
            stops[3].IsEnd.ShouldBeFalse();
            stops[6].IsEnd.ShouldBeTrue();
            stops[0].IsCurrent.ShouldBeTrue();
            AutomationProperties.GetItemStatus(stops[0]).ShouldBe(view.CurrentStopText);
            view.Captions.ShouldNotBeNull().Views.Select(v => v.CaptionTitle).ShouldBe(
                ["A single screen", "Layer 0", "Layer 1", "Layer 2", "Layer 3", "Layer 4", "Together again"]);
            view.LayerList.ShouldNotBeNull().Children.OfType<TextBlock>().Select(t => t.Text).ShouldBe(
                ["Layer 0: Body 0", "Layer 1: Body 1", "Layer 2: Body 2", "Layer 3: Body 3", "Layer 4: Body 4"]);
            AutomationProperties.GetName(view.Stops!).ShouldBe(view.StopsLabel);

            // Adding a layer and renaming one follow at once.
            view.Layers.Add(new ExplodedLayer { Name = "Layer 5", Body = "Body 5" });
            view.Layers[2].Name = "Renamed";
            Pump();
            view.StopCount.ShouldBe(8);
            stage.Hosts.Count.ShouldBe(6);
            AutomationProperties.GetName(StopButtons(view)[3]).ShouldBe("Renamed");
            stage.Labels[2].LabelName.ShouldBe("Renamed");
            view.GetStopName(7).ShouldBe(view.ReassembledText);
            view.GetStopCaption(3).ShouldBe(("Renamed", "Body 2"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Frame_Scrolls_An_Empty_Track_Four_Frames_Tall_Under_A_Pinned_Stage()
    {
        var view = Create();
        var window = Show(view);
        try
        {
            var scroll = view.ScrollRegion.ShouldNotBeNull();
            scroll.Viewport.Height.ShouldBe(858, 0.5);
            scroll.Extent.Height.ShouldBe(3432, 0.5);
            view.Track.ShouldNotBeNull().StickyHeight.ShouldBe(858, 0.5);

            scroll.Offset = new Vector(0, 2574 * 0.43);
            Pump();
            view.Progress.ShouldBe(0.43, 0.001);
            view.CurrentStop.ShouldBe(2);
            view.Track!.StickyOffset.ShouldBe(scroll.Offset.Y, 0.01);
            // The pinned part stays in view: its top is where the viewport starts.
            view.LayoutPanel.ShouldNotBeNull().TranslatePoint(default, scroll)!.Value.Y.ShouldBe(0, 0.5);

            // Setting the progress scrolls the frame.
            view.Progress = 0.748;
            Pump();
            scroll.Offset.Y.ShouldBe(0.748 * 2574, 0.5);
            view.CurrentStop.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Wheel_Over_The_Stage_Scrolls_The_Frame()
    {
        var view = Create();
        var window = Show(view);
        try
        {
            var stage = view.Stage.ShouldNotBeNull();
            stage.IsHitTestVisible.ShouldBeFalse();
            var center = stage.TranslatePoint(new Point(stage.Bounds.Width / 2, stage.Bounds.Height / 2), window)!.Value;
            window.MouseWheel(center, new Vector(0, -1));
            Pump();
            // One notch is 100 px, as in a browser.
            view.ScrollRegion!.Offset.Y.ShouldBe(100, 0.01);
            view.Progress.ShouldBe(100 / 2574.0, 1e-4);
            window.MouseWheel(center, new Vector(0, 3));
            Pump();
            view.ScrollRegion.Offset.Y.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Dims_The_Other_Layers_And_Raises_StopChanged()
    {
        var view = Create();
        var events = new List<ExplodedStopChangedEventArgs>();
        view.StopChanged += (_, e) => events.Add(e);
        var window = Show(view);
        try
        {
            var stage = view.Stage!;
            for (var i = 0; i < 5; i++)
            {
                stage.Hosts[i].Opacity.ShouldBe(1);
            }

            view.Progress = 0.33;
            Pump();
            view.CurrentStop.ShouldBe(1);
            view.CurrentLayer.ShouldBe(view.Layers[0]);
            events.Count.ShouldBe(1);
            events[0].Stop.ShouldBe(1);
            events[0].PreviousStop.ShouldBe(0);
            events[0].Layer.ShouldBe(view.Layers[0]);
            events[0].LayerIndex.ShouldBe(0);
            stage.ActiveLayer.ShouldBe(0);
            stage.Hosts[0].IsActive.ShouldBeTrue();
            stage.Hosts[0].Opacity.ShouldBe(1);
            stage.Hosts[3].Opacity.ShouldBe(0.5);
            stage.Labels[0].Classes.ShouldContain(":active");
            StopButtons(view)[1].IsCurrent.ShouldBeTrue();
            StopButtons(view)[0].IsCurrent.ShouldBeFalse();
            view.LiveRegion.ShouldNotBeNull().LastAnnouncement.ShouldBe("Layer 0. Body 0");
            view.Captions!.Current.ShouldBe(1);

            view.DimmedLayerOpacity = 0.3;
            Pump();
            stage.Hosts[3].Opacity.ShouldBe(0.3);

            view.Progress = 0.9;
            Pump();
            events[^1].Stop.ShouldBe(6);
            events[^1].Layer.ShouldBeNull();
            events[^1].LayerIndex.ShouldBe(-1);
            stage.ActiveLayer.ShouldBe(-1);
            stage.Hosts.ShouldAllBe(h => h.Opacity == 1 && !h.IsActive);
            view.LiveRegion!.LastAnnouncement.ShouldBe("Together again. One calm screen.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Off)]
    [InlineData(ArcMotionLevel.Reduced)]
    public void Reduced_And_Off_Motion_Discretise_The_Scene(ArcMotionLevel level)
    {
        var view = Create();
        var window = Show(view, level);
        try
        {
            var stage = view.Stage!;
            view.Progress = 0.2;
            Pump();
            (stage.Tilt, stage.Spread).ShouldBe((0, 0));
            view.Classes.ShouldNotContain(":exploded");
            stage.Labels.ShouldAllBe(l => l.Opacity == 0);

            view.Progress = 0.3;
            Pump();
            (stage.Tilt, stage.Spread).ShouldBe((1, 1));
            view.Classes.ShouldContain(":exploded");
            stage.Labels.ShouldAllBe(l => l.Opacity == 1);
            view.SmoothedProgress.ShouldBe(0.3);
            view.IsFollowing.ShouldBeFalse();
            // Captions switch at once.
            view.Captions!.Views.Select(v => v.Opacity).ShouldBe([0, 1, 0, 0, 0, 0, 0]);

            view.Progress = 0.85;
            Pump();
            (stage.Tilt, stage.Spread).ShouldBe((0, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Continuous_Pose_Projects_Layers_And_Labels_Together()
    {
        var view = Create();
        view.AlwaysContinuous = true;
        var window = Show(view);
        try
        {
            var stage = view.Stage!;
            view.Progress = 0.2;
            Pump();
            stage.Spread.ShouldBe(ExplodedViewProgress.Spread(0.2), 1e-9);
            stage.Tilt.ShouldBe(ExplodedViewProgress.Tilt(0.2), 1e-9);
            stage.Labels[0].Draw.ShouldBe(ExplodedViewProgress.LabelDraw(0, 5, stage.Spread), 1e-9);
            stage.Labels[0].Opacity.ShouldBe(stage.Labels[0].Draw, 1e-9);
            stage.Labels[0].LeaderScale.ShouldBe(stage.Labels[0].Draw, 1e-9);
            stage.Labels[4].Draw.ShouldBe(0);
            stage.Hosts[0].OutlineOpacity.ShouldBe(0);
            stage.Hosts[2].OutlineOpacity.ShouldBe(0.9 * stage.Spread, 1e-9);

            view.Progress = 0.43;
            Pump();
            var size = stage.Bounds.Size;
            for (var i = 0; i < 5; i++)
            {
                // The label's dot sits on the projection of the layer's anchor (right edge, 44 px above the bottom).
                var anchor = ShowpieceProjection.Apply(stage.Hosts[i].Matrix, new Point(280, 620 - 44));
                stage.Labels[i].Anchor.X.ShouldBe(anchor.X, 0.01);
                stage.Labels[i].Anchor.Y.ShouldBe(anchor.Y, 0.01);
                stage.Labels[i].Opacity.ShouldBe(1);
                // Stack and labels stay inside the stage.
                foreach (var corner in new[] { new Point(0, 0), new Point(280, 0), new Point(280, 620), new Point(0, 620) })
                {
                    var p = ShowpieceProjection.Apply(stage.Hosts[i].Matrix, corner);
                    p.X.ShouldBeInRange(0, size.Width);
                    p.Y.ShouldBeInRange(0, size.Height);
                }
                (stage.Labels[i].Anchor.X + stage.Labels[i].DesiredSize.Width).ShouldBeLessThanOrEqualTo(size.Width);
            }
            // Labels stack up the right side, top layer highest.
            stage.Labels[4].Anchor.Y.ShouldBeLessThan(stage.Labels[0].Anchor.Y);
            stage.Composition.ShouldBe(ExplodedComposition.Wide);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Frame_Keys_Scroll_And_Keyboard_Focus_Shows_The_Ring()
    {
        var view = Create();
        var window = Show(view);
        try
        {
            var scroll = view.ScrollRegion!;
            scroll.Focusable.ShouldBeTrue();
            scroll.Focus(NavigationMethod.Tab);
            Pump();
            view.Classes.ShouldContain(":region-focus");
            AutomationProperties.GetName(scroll).ShouldStartWith("Made of layers.");

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            scroll.Offset.Y.ShouldBe(40, 0.01);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            Pump();
            scroll.Offset.Y.ShouldBe(40 + 818, 0.5);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Pump();
            scroll.Offset.Y.ShouldBe(40 + 818, 0.5);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Pump();
            scroll.Offset.Y.ShouldBe(2574, 0.5);
            view.Progress.ShouldBe(1, 0.001);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Pump();
            scroll.Offset.Y.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump();
            scroll.Offset.Y.ShouldBe(818, 0.5);

            // Focus moving into a stop button (inside the frame) hides the frame's ring; pointer focus never shows it.
            StopButtons(view)[2].Focus(NavigationMethod.Tab);
            Pump();
            view.Classes.ShouldNotContain(":region-focus");
            scroll.Focus(NavigationMethod.Pointer);
            Pump();
            view.Classes.ShouldNotContain(":region-focus");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Stop_Button_Scrolls_To_Its_Stop_And_Keeps_Focus()
    {
        var view = Create();
        var window = Show(view);
        try
        {
            var button = StopButtons(view)[3];
            button.Focus(NavigationMethod.Tab);
            Pump();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            view.Progress.ShouldBe(0.540, 0.001);
            view.ScrollRegion!.Offset.Y.ShouldBe(0.540 * 2574, 0.5);
            view.CurrentStop.ShouldBe(3);
            button.IsFocused.ShouldBeTrue();
            button.IsCurrent.ShouldBeTrue();
            // Focusing a button never scrolls the frame by itself.
            StopButtons(view)[0].Focus(NavigationMethod.Tab);
            Pump();
            view.ScrollRegion.Offset.Y.ShouldBe(0.540 * 2574, 0.5);

            view.GoToStop(99, animate: false);
            Pump();
            view.Progress.ShouldBe(1, 0.001);
            view.Reset();
            Pump();
            view.Progress.ShouldBe(0);
            view.CurrentStop.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void External_Mode_Takes_The_Progress_From_The_Host()
    {
        var view = Create();
        view.ScrollMode = ExplodedScrollMode.External;
        var window = Show(view);
        try
        {
            view.ScrollRegion!.Focusable.ShouldBeFalse();
            view.ScrollRegion.Extent.Height.ShouldBe(view.ScrollRegion.Viewport.Height, 0.5);
            view.GoToStop(4);
            Pump();
            view.Progress.ShouldBe(0.644, 1e-9);
            view.CurrentStop.ShouldBe(4);
            view.Progress = 0.1;
            Pump();
            view.CurrentStop.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Page_Mode_Pins_The_Stage_To_The_Page_Viewport()
    {
        var view = Create();
        view.Width = double.NaN;
        view.Height = double.NaN;
        view.ScrollMode = ExplodedScrollMode.Page;
        view.IsFramed = false;
        view.Length = 3;
        var page = new ScrollViewer
        {
            Height = 500,
            Content = new StackPanel { Children = { new Border { Height = 200 }, view, new Border { Height = 200 } } }
        };
        var window = Show(page, height: 600);
        try
        {
            Pump();
            view.PageScrollViewer.ShouldBe(page);
            view.Bounds.Height.ShouldBe(1500, 0.5);
            view.Track!.StickyHeight.ShouldBe(500, 0.5);
            view.Progress.ShouldBe(0);

            page.Offset = new Vector(0, 200 + 500);
            Pump();
            view.Progress.ShouldBe(0.5, 0.002);
            view.Track.StickyOffset.ShouldBe(500, 0.5);
            view.LayoutPanel!.TranslatePoint(default, page)!.Value.Y.ShouldBe(0, 0.5);

            view.GoToStop(6, animate: false);
            Pump();
            page.Offset.Y.ShouldBe(200 + 1000, 0.5);
            view.Progress.ShouldBe(1, 0.002);
            view.CurrentStop.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Tall_Container_Uses_The_Compact_Layout_And_The_Tall_Composition()
    {
        var view = Create(width: 390, height: 760);
        var window = Show(view);
        try
        {
            view.Classes.ShouldContain(":compact");
            view.Classes.ShouldContain(":tall-stage");
            view.Classes.ShouldNotContain(":wide");
            var stage = view.Stage!;
            stage.IsTall.ShouldBeTrue();
            stage.Composition.ShouldBe(new ExplodedComposition(62, -20, 150));
            stage.Labels.ShouldAllBe(l => !l.IsNoteVisible);
            var rects = view.LayoutPanel!.Rects;
            rects.Stops.Bottom.ShouldBeLessThan(rects.Caption.Top);
            var buttons = StopButtons(view);
            buttons[0].Bounds.Width.ShouldBe(rects.Stops.Width / 7, 0.01);
            buttons[0].Bounds.Width.ShouldBeGreaterThan(36);

            // Wider than tall: compact, but the wide composition.
            view.Height = 460;
            Pump();
            view.Classes.ShouldNotContain(":tall-stage");
            stage.Composition.ShouldBe(ExplodedComposition.Wide);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Wide_Container_Puts_The_Stage_Beside_The_Text()
    {
        var view = Create(width: 1100, height: 860);
        var window = Show(view, width: 1300);
        try
        {
            view.Classes.ShouldContain(":wide");
            view.Classes.ShouldNotContain(":compact");
            var rects = view.LayoutPanel!.Rects;
            rects.Stage.X.ShouldBe(380);
            rects.Stage.Height.ShouldBe(858, 0.5);
            rects.Caption.Bottom.ShouldBeLessThan(rects.Stops.Top);
            view.Stage!.Labels.ShouldAllBe(l => l.IsNoteVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Caption_Area_Is_As_Tall_As_The_Tallest_Caption()
    {
        var view = Create();
        view.Layers[3].Body = string.Join(" ", Enumerable.Repeat("A long caption that wraps onto more lines.", 6));
        var window = Show(view);
        try
        {
            var captions = view.Captions!;
            var tallest = captions.Views.Max(v => v.DesiredSize.Height);
            captions.Bounds.Height.ShouldBe(tallest, 0.5);
            tallest.ShouldBeGreaterThan(captions.Views.First().DesiredSize.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Stage_Is_A_Picture_Without_Focus_Stops_Or_Automation_Children()
    {
        var view = Create();
        view.Layers[3].Content = new Avalonia.Controls.Button { Content = "Inside a layer" };
        var window = Show(view);
        try
        {
            var stage = view.Stage!;
            KeyboardNavigation.GetTabNavigation(stage).ShouldBe(KeyboardNavigationMode.None);
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(stage);
            peer.GetChildren().ShouldBeEmpty();
            peer.IsControlElement().ShouldBeFalse();

            // Tab from the frame goes to the stop buttons, never into a layer.
            view.ScrollRegion!.Focus(NavigationMethod.Tab);
            Pump();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Pump();
            StopButtons(view)[0].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Frame_Can_Be_Turned_Off()
    {
        var view = Create();
        var window = Show(view);
        try
        {
            view.Classes.ShouldContain(":framed");
            view.ScrollRegion!.Viewport.Height.ShouldBe(858, 0.5);
            view.IsFramed = false;
            Pump();
            view.Classes.ShouldNotContain(":framed");
            view.ScrollRegion.Viewport.Height.ShouldBe(860, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Follows_On_The_Spring_And_Then_Rests()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var view = Create();
        var window = Show(view, ArcMotionLevel.Full);
        try
        {
            RunFrames(clock, 10);
            var baseline = MotionScheduler.ActiveCount;
            view.Progress = 0.06;
            RunFrames(clock, 120);
            view.SmoothedProgress.ShouldBe(0.06, 0.0006);

            // A jump of 0.14: half way in ≈132 ms, 90% in ≈392 ms, settled (snapped) well within a second.
            view.Progress = 0.20;
            RunFrames(clock, 9);
            var half = (view.SmoothedProgress - 0.06) / 0.14;
            half.ShouldBeInRange(0.42, 0.6);
            view.IsFollowing.ShouldBeTrue();
            RunFrames(clock, 16);
            ((view.SmoothedProgress - 0.06) / 0.14).ShouldBeInRange(0.86, 0.94);
            RunFrames(clock, 60);
            view.SmoothedProgress.ShouldBe(0.20);
            view.IsFollowing.ShouldBeFalse();
            // The continuous pose follows the smoothed value.
            view.Stage!.Spread.ShouldBe(ExplodedViewProgress.Spread(0.20), 1e-9);

            // Idle: nothing runs and no frame is requested.
            RunFrames(clock, 60);
            MotionScheduler.ActiveCount.ShouldBe(baseline);
            var requests = clock.RequestCount;
            RunFrames(clock, 30);
            clock.RequestCount.ShouldBe(requests);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Crossfades_The_Caption_And_Glides_To_A_Stop()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var view = Create();
        var window = Show(view, ArcMotionLevel.Full);
        try
        {
            RunFrames(clock, 10);
            view.GoToStop(2);
            view.IsScrollAnimating.ShouldBeTrue();
            RunFrames(clock, 3);
            view.ScrollRegion!.Offset.Y.ShouldBeInRange(1, 0.436 * 2574 - 1);
            RunFrames(clock, 60);
            view.IsScrollAnimating.ShouldBeFalse();
            view.ScrollRegion.Offset.Y.ShouldBe(0.436 * 2574, 0.5);
            // The scene follows on its spring and lands on the stop.
            RunFrames(clock, 60);
            view.CurrentStop.ShouldBe(2);

            // Crossfade: the new caption arrives over 480 ms while the old one leaves in 180 ms.
            view.Progress = ExplodedViewProgress.StopProgress(3, 5);
            var views = view.Captions!.Views.ToList();
            for (var i = 0; i < 40 && view.CurrentStop != 3; i++)
            {
                RunFrames(clock, 1);
            }
            view.CurrentStop.ShouldBe(3);
            RunFrames(clock, 2);
            // The exit curve starts slowly; the enter curve fast.
            views[3].Opacity.ShouldBeInRange(0.05, 0.99);
            views[2].Opacity.ShouldBeLessThan(1);
            RunFrames(clock, 12);
            views[2].Opacity.ShouldBe(0);
            views[3].Opacity.ShouldBeLessThan(1);
            RunFrames(clock, 30);
            views[3].Opacity.ShouldBe(1);

            // A wheel or key scroll takes over from a glide.
            view.GoToStop(6);
            RunFrames(clock, 2);
            view.ScrollRegion.Offset = new Vector(0, 100);
            view.IsScrollAnimating.ShouldBeFalse();
            RunFrames(clock, 10);
            view.ScrollRegion.Offset.Y.ShouldBe(100);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Theme_Transitions_Use_The_Measured_Curves_Only_At_Full_Motion()
    {
        var view = Create();
        var window = Show(view, ArcMotionLevel.Full);
        try
        {
            Pump();
            var host = view.Stage!.Hosts[2];
            var transitions = host.Transitions.ShouldNotBeNull();
            var opacity = transitions.OfType<ArcDoubleTransition>().ShouldHaveSingleItem();
            opacity.Duration.ShouldBe(TimeSpan.FromMilliseconds(480));
            opacity.Easing.ShouldBe(ArcEasings.Standard);
            var outline = transitions.OfType<ArcBrushTransition>().ShouldHaveSingleItem();
            outline.Duration.ShouldBe(TimeSpan.FromMilliseconds(240));
            var tick = StopButtons(view)[1].GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Tick");
            var scale = tick.Transitions.ShouldNotBeNull().OfType<ArcTransformOperationsTransition>().ShouldHaveSingleItem();
            scale.Duration.ShouldBe(TimeSpan.FromMilliseconds(580));
            scale.Easing.ShouldBe(ArcEasings.CssSpring);

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Pump();
            host.Transitions.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_To_Reduced_Motion_Settles_At_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var view = Create();
        var window = Show(view, ArcMotionLevel.Full);
        try
        {
            RunFrames(clock, 10);
            view.Progress = 0.5;
            RunFrames(clock, 2);
            view.IsFollowing.ShouldBeTrue();
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Pump();
            view.IsFollowing.ShouldBeFalse();
            view.SmoothedProgress.ShouldBe(0.5);
            (view.Stage!.Tilt, view.Stage.Spread).ShouldBe((1, 1));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrolling_Draws_The_Layers_From_Bitmaps_Without_Laying_Out_The_Stage_And_Rests_As_Vectors()
    {
        // Performance guard. Under a perspective matrix every vector primitive takes the GPU backend's slow paths, so while
        // the scene moves each layer (picture and outline) is drawn from a bitmap cache; the pose itself is transforms and
        // opacities only (no layout). At rest the layers are vectors again, except the pictures of dimmed layers (which fade
        // as one picture), and nothing is cached on the whole screen. The bitmaps are a GPU-only optimization (the software
        // rasterizer is slower with them), so the headless renderer keeps vectors unless the test forces the caches on.
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var view = Create();
        var window = Show(view, ArcMotionLevel.Full);
        try
        {
            RunFrames(clock, 10);
            var stage = view.Stage!;
            stage.AllowCache.ShouldBeFalse();
            view.Progress = 0.3;
            RunFrames(clock, 3);
            stage.IsMoving.ShouldBeTrue();
            stage.Hosts.ShouldAllBe(h => !h.IsCached && !h.IsOutlineCached);
            view.GoToStop(0, animate: false);
            RunFrames(clock, 150);

            view.LayerCacheOverride = true;
            stage.AllowCache.ShouldBeTrue();
            stage.IsMoving.ShouldBeFalse();
            foreach (var host in stage.Hosts)
            {
                host.CachedPart.ShouldNotBeNull().CacheMode.ShouldBeNull();
                host.Outline.ShouldNotBeNull().CacheMode.ShouldBeNull();
            }
            var measures = stage.MeasureCount;
            var arranges = stage.ArrangeCount;
            var scroll = view.ScrollRegion!;
            var scrollable = scroll.Extent.Height - scroll.Viewport.Height;
            scrollable.ShouldBeGreaterThan(0);

            // A scroll across the opening and two stop changes, one step per frame.
            for (var i = 1; i <= 40; i++)
            {
                scroll.Offset = new Vector(0, scrollable * 0.5 * i / 40);
                RunFrames(clock, 1);
                stage.IsMoving.ShouldBeTrue($"frame {i}");
            }
            foreach (var host in stage.Hosts)
            {
                host.IsCached.ShouldBeTrue();
                host.IsOutlineCached.ShouldBeTrue();
                host.CachedPart!.CacheMode.ShouldBeSameAs(stage.Cache);
                host.Outline!.CacheMode.ShouldBeSameAs(stage.Cache);
            }
            stage.Cache.RenderAtScale.ShouldBe(ExplodedViewStage.CacheScale(stage.Fit));
            stage.MeasureCount.ShouldBe(measures);
            stage.ArrangeCount.ShouldBe(arranges);

            // The spring settles; the bitmaps are kept a moment (the stop change's transitions), then the focused layer and
            // every outline are vectors; the dimmed layers keep their pictures.
            var settle = 0;
            while (view.IsFollowing && settle++ < 120)
            {
                RunFrames(clock, 1);
            }
            view.IsFollowing.ShouldBeFalse();
            view.CurrentStop.ShouldBe(3);
            stage.IsMoving.ShouldBeTrue();
            RunFrames(clock, (int)Math.Floor(ExplodedView.CacheHold.TotalMilliseconds / 16) - 2);
            stage.IsMoving.ShouldBeTrue();
            RunFrames(clock, 4);
            stage.IsMoving.ShouldBeFalse();
            for (var i = 0; i < stage.Hosts.Count; i++)
            {
                var host = stage.Hosts[i];
                host.IsCached.ShouldBe(i != 2, $"layer {i}");
                host.CachedPart!.CacheMode.ShouldBe(i == 2 ? null : stage.Cache);
                host.IsOutlineCached.ShouldBeFalse();
                host.Outline!.CacheMode.ShouldBeNull();
            }

            // The whole screen at rest: no layer is dimmed, nothing is cached.
            view.GoToStop(0, animate: false);
            RunFrames(clock, 120);
            stage.IsMoving.ShouldBeFalse();
            foreach (var host in stage.Hosts)
            {
                host.CachedPart!.CacheMode.ShouldBeNull();
                host.Outline!.CacheMode.ShouldBeNull();
            }
            stage.MeasureCount.ShouldBe(measures);
            stage.ArrangeCount.ShouldBe(arranges);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Caches_Only_Dimmed_Pictures_And_The_Per_Frame_Pose_Allocates_Little()
    {
        var view = Create();
        view.LayerCacheOverride = true;
        var window = Show(view, ArcMotionLevel.Reduced);
        try
        {
            var stage = view.Stage!;
            view.Progress = 0.5;
            Pump();
            stage.IsMoving.ShouldBeFalse();
            stage.ActiveLayer.ShouldBe(2);
            for (var i = 0; i < stage.Hosts.Count; i++)
            {
                stage.Hosts[i].IsCached.ShouldBe(i != 2, $"layer {i}");
                stage.Hosts[i].IsOutlineCached.ShouldBeFalse();
            }
            view.DimmedLayerOpacity = 1;
            Pump();
            stage.Hosts.ShouldAllBe(h => !h.IsCached);

            // The pose written per frame: matrices, depths, outline fades, label places. Only Avalonia's own property
            // change notifications allocate (no pens, geometries or brushes per frame).
            for (var i = 0; i < 20; i++)
            {
                stage.SetPose(ExplodedViewProgress.Tilt(i / 100.0), ExplodedViewProgress.Spread(i / 100.0));
            }
            const int frames = 200;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < frames; i++)
            {
                var p = 0.05 + 0.3 * i / frames;
                stage.SetPose(ExplodedViewProgress.Tilt(p), ExplodedViewProgress.Spread(p));
            }
            var perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)frames;
            perFrame.ShouldBeLessThan(4096);
        }
        finally
        {
            window.Close();
        }
    }
}
